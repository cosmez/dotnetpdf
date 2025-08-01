using System.Text;
using PDFiumCore;
using static PDFiumCore.fpdf_doc;
using static PDFiumCore.fpdfview;
using static PDFiumCore.fpdf_text;
using static PDFiumCore.fpdf_attachment;
using Microsoft.Extensions.Logging;
// ReSharper disable StringLiteralTypo

namespace dotnet.pdf;

public partial class Pdfium
{
    private static readonly ILogger Logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<Pdfium>();

    static string GetPdfActionType(uint type) => type switch
    {
        1 => "GOTO",
        2 => "REMOTEGOTO",
        3 => "URI",
        4 => "LAUNCH",
        5 => "EMBEDDEDGOTO",
        _ => "UNSUPPORTED"
    };
    
    static string GetUtf16String<T>(T element,
        Func<T, IntPtr, uint, uint> stringMethod)
    {
        uint length = stringMethod(element, IntPtr.Zero, 0);
        if (length == 0) return string.Empty;
        unsafe
        {
            Span<byte> txt = new byte[length];
            fixed (byte* ptrr = &txt[0])
            {
                var endOfString = stringMethod(element, (IntPtr)ptrr, length);
                endOfString -= 2;
                return Encoding.Unicode.GetString(ptrr, endOfString > 0 ? (int)endOfString : 0);
            }
        }
    }
    
    static string GetUtf16String<T>(T element, string field,
        Func<T, string, IntPtr, uint, uint> stringMethod)
    {
        uint length = stringMethod(element, field, IntPtr.Zero, 0);
        if (length == 0) return string.Empty;
        unsafe
        {
            Span<byte> txt = new byte[length];
            fixed (byte* ptrr = &txt[0])
            {
                var endOfString = stringMethod(element, field, (IntPtr)ptrr, length);
                endOfString -= 2;
                return Encoding.Unicode.GetString(ptrr, endOfString > 0 ? (int)endOfString : 0);
            }
        }
    }

    public static List<PDfPageText>? GetPdfText(string inputfilename,
        List<int>? pageRange, string password = "")
    {
        var result = new List<PDfPageText>();
        if (inputfilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputfilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputfilename, password);

                var numberOfPages = FPDF_GetPageCount(documentT);

                for (int i = 0; i < numberOfPages; i++)
                {
                    if (pageRange != null && !pageRange.Contains(i+1)) continue;
                    var pageT = FPDF_LoadPage(documentT, i);
                    var pageText = GetPageText(pageT);
                    int charCount = pageText.Characters;
                    pageText.Page = i + 1;
                    Console.WriteLine(pageText.Text);
                    result.Add(pageText);
                    FPDF_ClosePage(pageT);
                }

                FPDF_CloseDocument(documentT);
            }
            return result;
        }

        return null;
    }
    
    public static List<PDfBookmark>? GetPdfBookmarks(string inputfilename, string password = "")
    {
        if (inputfilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputfilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputfilename, password);
                var bookmarks = GetBookmarks(documentT);
                FPDF_CloseDocument(documentT);
                return bookmarks;
            }
        }

        return null;
    }
    
    public static PdfInfo? GetPdfInformation(string inputfilename, string password = "")
    {
        if (inputfilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputfilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputfilename, password);
                var pdfInfo = new PdfInfo();
                pdfInfo.Pages = FPDF_GetPageCount(documentT);
                pdfInfo.Author = GetMetaText(documentT, "Author");
                pdfInfo.CreationDate = GetMetaText(documentT, "CreationDate");
                pdfInfo.Creator = GetMetaText(documentT, "Creator");
                pdfInfo.Keywords = GetMetaText(documentT, "Keywords");
                pdfInfo.Producer = GetMetaText(documentT, "Procuder");
                pdfInfo.ModifiedDate = GetMetaText(documentT, "ModifiedDate");
                pdfInfo.Subject = GetMetaText(documentT, "Subject");
                pdfInfo.Title = GetMetaText(documentT, "Title");
                int version = 0;
                FPDF_GetFileVersion(documentT, ref version);
                pdfInfo.Version = version;
                pdfInfo.Trapped = GetMetaText(documentT, "Trapped");
                FPDF_CloseDocument(documentT);
                return pdfInfo;
            }
        }

        return null;
    }
    
    public static string GetMetaText(FpdfDocumentT document, string tag)
    {
        return GetUtf16String(document, tag, fpdf_doc.FPDF_GetMetaText);
        
    }
    
    /// <summary>
    /// Get all the document bookmarks
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    private static List<PDfBookmark> GetBookmarks(FpdfDocumentT document)
    {
        List<PDfBookmark> bookmarks = new();
        void RecurseBookmark(FpdfBookmarkT bookmark, int level)
        {
            // Get the title of the bookmark
            //FPDF_WIDESTRING title = nullptr;
            string title = GetUtf16String(bookmark, FPDFBookmarkGetTitle);
            var bMark = new PDfBookmark
            {
                Title = title,
                Level = level,
                Action = string.Empty
            };

            var actionT = FPDFBookmarkGetAction(bookmark);
            if (actionT != null)
            {
                var actionTypeId = FPDFActionGetType(actionT);
                var actionType = GetPdfActionType(actionTypeId); 

                bMark.Action = actionType;


                if (actionType is "GOTO" or "REMOTOGOTO")
                {
                    var destT = FPDFActionGetDest(document, actionT);
                    var pageIndex = FPDFDestGetDestPageIndex(document, destT);
                    bMark.Page = pageIndex;
                }
            }
            else
            {
                var destT = FPDFBookmarkGetDest(document, bookmark);
                int pageIndex = FPDFDestGetDestPageIndex(document, destT);
                bMark.Action = "Page";
                bMark.Page = pageIndex;
            }



            bookmarks.Add(bMark);

            if (!string.IsNullOrWhiteSpace(title))
            {

                var child = FPDFBookmarkGetFirstChild(document, bookmark);
                if (child != null)
                {
                    RecurseBookmark(child, level + 1);
                }

                var sibling = FPDFBookmarkGetNextSibling(document, bookmark);
                if (sibling != null)
                {
                    RecurseBookmark(sibling, level);
                }
            }
        }

        var rootBookmark = FPDFBookmarkGetFirstChild(document, null);

        if (rootBookmark != null)
        {
            RecurseBookmark(rootBookmark, 0);
        }

        return bookmarks;
    }
    
    private static PDfPageText GetPageText(FpdfPageT page)
    {
        var pageText = new PDfPageText();
        var pageT = FPDFTextLoadPage(page);
        pageText.Characters =  FPDFTextCountChars(pageT);
        pageText.Rects = FPDFTextCountRects(pageT, 0, -1);

        // Get the word count
        int wordCount = 0;

        for (int i = 0; i < pageText.Characters; i++)
        {
            var charCode = FPDFTextGetUnicode(pageT, i);
            if (charCode is ' ' or '\n' or '\t')
            {
                wordCount++;
            }
        }

        pageText.WordsCount = wordCount;

        unsafe
        {
            Span<byte> txt = new byte[pageText.Characters * 2 + 1];
            fixed (byte* ptrr = &txt[0])
            {
                FPDFTextGetText(pageT, 0, pageText.Characters, ref *(ushort*)ptrr);
                pageText.Text = Encoding.Unicode.GetString(txt);
            }
        }

        FPDFTextClosePage(pageT);

        return pageText;
    }

    /// <summary>
    /// Gets a list of attachments from a PDF document
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file</param>
    /// <param name="password">The optional password to unlock the input PDF file</param>
    /// <returns>List of PDF attachments with metadata</returns>
    public static List<PdfAttachment>? GetPdfAttachments(string inputFilename, string password = "")
    {
        if (inputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputFilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    Logger.LogError("Failed to load PDF document for attachment extraction");
                    return null;
                }

                try
                {
                    var attachments = new List<PdfAttachment>();
                    var attachmentCount = FPDFDocGetAttachmentCount(documentT);
                    
                    Logger.LogInformation("Found {Count} attachments in PDF", attachmentCount);

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
                    }

                    return attachments;
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts a specific attachment from a PDF document and saves it to disk
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file</param>
    /// <param name="attachmentIndex">The index of the attachment to extract (0-based)</param>
    /// <param name="outputPath">The path where the attachment will be saved</param>
    /// <param name="password">The optional password to unlock the input PDF file</param>
    /// <returns>True if extraction was successful, false otherwise</returns>
    public static bool ExtractAttachment(string inputFilename, int attachmentIndex, string outputPath, string password = "")
    {
        if (inputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputFilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    Logger.LogError("Failed to load PDF document for attachment extraction");
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

                    // Write the data to file
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "");
                        File.WriteAllBytes(outputPath, buffer);
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

        return false;
    }

    /// <summary>
    /// Extracts attachment information from an attachment handle
    /// </summary>
    private static PdfAttachment? ExtractAttachmentInfo(FpdfAttachmentT attachmentHandle)
    {
        try
        {
            var attachment = new PdfAttachment();

            // Get attachment name
            attachment.Name = GetAttachmentName(attachmentHandle);

            // Get file size
            uint dataSize = 0;
            var hasData = FPDFAttachmentGetFile(attachmentHandle, IntPtr.Zero, 0, ref dataSize);
            if (hasData != 0)
            {
                attachment.Size = (int)dataSize;
            }

            // Get common metadata
            attachment.Description = GetAttachmentStringValue(attachmentHandle, "Desc");
            attachment.MimeType = GetAttachmentStringValue(attachmentHandle, "Subtype");
            
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
    /// Gets a string value from an attachment's metadata
    /// </summary>
    private static string GetAttachmentStringValue(FpdfAttachmentT attachmentHandle, string key)
    {
        try
        {
            // First call to get the required buffer size
            uint valueLength = 0;
            unsafe
            {
                ushort dummy = 0;
                valueLength = FPDFAttachmentGetStringValue(attachmentHandle, key, ref dummy, 0);
            }
            
            if (valueLength <= 2) return string.Empty; // Empty or just null terminator

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
            Logger.LogWarning(ex, "Failed to get attachment string value for key {Key}", key);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses PDF date format (D:YYYYMMDDHHmmSSOHH'mm)
    /// </summary>
    private static DateTime? ParsePdfDate(string pdfDate)
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
    /// Gets the name of an attachment
    /// </summary>
    private static string GetAttachmentName(FpdfAttachmentT attachmentHandle)
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
}