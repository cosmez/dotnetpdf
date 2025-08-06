using System.Text;
using PDFiumCore;
using static PDFiumCore.fpdf_attachment;
using static PDFiumCore.fpdfview;
using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public class PdfAttachmentService : BasePdfService
{
    public PdfAttachmentService(ILogger<PdfAttachmentService> logger) : base(logger, null)
    {
    }

    /// <summary>
    /// Gets a list of attachments from a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PDF attachments with metadata</returns>
    public List<PdfAttachment>? GetPdfAttachments(string inputFilename, string password = "", IProgress<PdfProgress>? progress = null)
    {
        if (!IsValidPdfFile(inputFilename))
            return null;

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                Logger.LogError("Failed to load PDF document: {Filename}", inputFilename);
                return null;
            }

            try
            {
                var attachments = new List<PdfAttachment>();
                var attachmentCount = FPDFDocGetAttachmentCount(documentT);
                
                Logger.LogInformation("Found {Count} attachments in PDF: {Filename}", attachmentCount, inputFilename);

                for (int i = 0; i < attachmentCount; i++)
                {
                    var attachmentHandle = FPDFDocGetAttachment(documentT, i);
                    if (attachmentHandle != null)
                    {
                        var attachment = ExtractAttachmentInfo(attachmentHandle);
                        if (attachment != null)
                        {
                            attachments.Add(attachment);
                        }
                    }

                    // Report progress
                    progress?.Report(new PdfProgress(i + 1, attachmentCount));
                }

                return attachments;
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    /// <summary>
    /// Extracts a specific attachment from a PDF document and saves it to disk
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="attachmentIndex">Index of the attachment to extract (0-based)</param>
    /// <param name="outputPath">Path where the attachment will be saved</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>True if extraction was successful, false otherwise</returns>
    public bool ExtractAttachment(string inputFilename, int attachmentIndex, string outputPath, string password = "", IProgress<PdfProgress>? progress = null)
    {
        if (!IsValidPdfFile(inputFilename))
            return false;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Logger.LogError("Output path cannot be null or empty");
            return false;
        }

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                Logger.LogError("Failed to load PDF document: {Filename}", inputFilename);
                return false;
            }

            try
            {
                var attachmentCount = FPDFDocGetAttachmentCount(documentT);
                
                if (attachmentIndex < 0 || attachmentIndex >= attachmentCount)
                {
                    Logger.LogError("Attachment index {Index} is out of range. Document has {Count} attachments", 
                        attachmentIndex, attachmentCount);
                    return false;
                }

                var attachmentHandle = FPDFDocGetAttachment(documentT, attachmentIndex);
                if (attachmentHandle == null)
                {
                    Logger.LogError("Failed to get attachment handle for index {Index}", attachmentIndex);
                    return false;
                }

                // Get the file data size first
                uint dataSize = 0;
                var hasData = FPDFAttachmentGetFile(attachmentHandle, IntPtr.Zero, 0, ref dataSize);
                
                if (hasData == 0 || dataSize == 0)
                {
                    Logger.LogWarning("Attachment at index {Index} has no data or is empty", attachmentIndex);
                    return false;
                }

                // Report progress - start of extraction
                progress?.Report(new PdfProgress(0, 100));

                // Allocate buffer and get the actual data
                var buffer = new byte[dataSize];
                unsafe
                {
                    fixed (byte* bufferPtr = buffer)
                    {
                        var success = FPDFAttachmentGetFile(attachmentHandle, (IntPtr)bufferPtr, dataSize, ref dataSize);
                        if (success == 0)
                        {
                            Logger.LogError("Failed to retrieve attachment data for index {Index}", attachmentIndex);
                            return false;
                        }
                    }
                }

                // Report progress - data retrieved
                progress?.Report(new PdfProgress(50, 100));

                // Write the data to file
                try
                {
                    var directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    File.WriteAllBytes(outputPath, buffer);

                    // Report progress - complete
                    progress?.Report(new PdfProgress(100, 100));
                    Logger.LogInformation("Successfully extracted attachment to {OutputPath}", outputPath);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to write attachment data to {OutputPath}", outputPath);
                    return false;
                }
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    /// <summary>
    /// Extracts attachment information from an attachment handle
    /// </summary>
    /// <param name="attachmentHandle">PDFium attachment handle</param>
    /// <returns>PdfAttachment object with metadata</returns>
    private PdfAttachment? ExtractAttachmentInfo(FpdfAttachmentT attachmentHandle)
    {
        try
        {
            var attachment = new PdfAttachment
            {
                Name = GetAttachmentName(attachmentHandle),
                Description = GetAttachmentStringValue(attachmentHandle, "Desc"),
                MimeType = GetAttachmentStringValue(attachmentHandle, "Subtype")
            };

            // Get file size
            uint dataSize = 0;
            var hasData = FPDFAttachmentGetFile(attachmentHandle, IntPtr.Zero, 0, ref dataSize);
            if (hasData != 0)
            {
                attachment.Size = (int)dataSize;
            }

            // Try to parse creation and modification dates
            var creationDateStr = GetAttachmentStringValue(attachmentHandle, "CreationDate");
            var modDateStr = GetAttachmentStringValue(attachmentHandle, "ModDate");
            
            if (!string.IsNullOrEmpty(creationDateStr))
            {
                attachment.CreationDate = ParsePdfDate(creationDateStr);
            }
            
            if (!string.IsNullOrEmpty(modDateStr))
            {
                attachment.ModificationDate = ParsePdfDate(modDateStr);
            }

            // Store all available metadata
            var metadataKeys = new[] { "Desc", "Subtype", "CreationDate", "ModDate", "Size", "CheckSum" };
            foreach (var key in metadataKeys)
            {
                if (FPDFAttachmentHasKey(attachmentHandle, key) != 0)
                {
                    var value = GetAttachmentStringValue(attachmentHandle, key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        attachment.Metadata[key] = value;
                    }
                }
            }

            return attachment;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to extract attachment information");
            return null;
        }
    }

    /// <summary>
    /// Gets the name of an attachment
    /// </summary>
    /// <param name="attachmentHandle">PDFium attachment handle</param>
    /// <returns>Attachment name</returns>
    private string GetAttachmentName(FpdfAttachmentT attachmentHandle)
    {
        try
        {
            // First call to get the required buffer size
            uint nameLength = 0;
            unsafe
            {
                ushort dummy = 0;
                nameLength = FPDFAttachmentGetName(attachmentHandle, ref dummy, 0);
            }
            
            if (nameLength <= 2) return string.Empty;

            unsafe
            {
                var nameBuffer = new ushort[nameLength / 2];
                fixed (ushort* namePtr = nameBuffer)
                {
                    FPDFAttachmentGetName(attachmentHandle, ref *namePtr, nameLength);
                    return new string((char*)namePtr, 0, (int)(nameLength / 2) - 1);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get attachment name");
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a string value from an attachment by key
    /// </summary>
    /// <param name="attachmentHandle">PDFium attachment handle</param>
    /// <param name="key">Metadata key</param>
    /// <returns>String value</returns>
    private string GetAttachmentStringValue(FpdfAttachmentT attachmentHandle, string key)
    {
        try
        {
            uint valueLength = 0;
            unsafe
            {
                ushort dummy = 0;
                valueLength = FPDFAttachmentGetStringValue(attachmentHandle, key, ref dummy, 0);
            }
            
            if (valueLength <= 2) return string.Empty;

            unsafe
            {
                var valueBuffer = new ushort[valueLength / 2];
                fixed (ushort* valuePtr = valueBuffer)
                {
                    FPDFAttachmentGetStringValue(attachmentHandle, key, ref *valuePtr, valueLength);
                    return new string((char*)valuePtr, 0, (int)(valueLength / 2) - 1);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to get attachment string value for key: {Key}", key);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses a PDF date string into a DateTime object
    /// </summary>
    /// <param name="pdfDate">PDF date string (format: D:YYYYMMDDHHmmSSOHH'mm')</param>
    /// <returns>Parsed DateTime or null if parsing fails</returns>
    private DateTime? ParsePdfDate(string pdfDate)
    {
        try
        {
            if (string.IsNullOrEmpty(pdfDate) || !pdfDate.StartsWith("D:"))
                return null;

            var dateStr = pdfDate.Substring(2); // Remove "D:" prefix
            
            // Basic format: YYYYMMDDHHMMSS
            if (dateStr.Length >= 14)
            {
                var year = int.Parse(dateStr.Substring(0, 4));
                var month = int.Parse(dateStr.Substring(4, 2));
                var day = int.Parse(dateStr.Substring(6, 2));
                var hour = int.Parse(dateStr.Substring(8, 2));
                var minute = int.Parse(dateStr.Substring(10, 2));
                var second = int.Parse(dateStr.Substring(12, 2));

                return new DateTime(year, month, day, hour, minute, second);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to parse PDF date: {Date}", pdfDate);
        }

        return null;
    }

    /// <summary>
}
