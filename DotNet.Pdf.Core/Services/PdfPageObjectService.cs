using System.Runtime.InteropServices;
using PDFiumCore;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdf_text;
using static PDFiumCore.fpdfview;
using static PDFiumCore.fpdf_save;
using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public class PdfPageObjectService : BasePdfService
{
    public PdfPageObjectService(ILogger<PdfPageObjectService> logger) : base(logger, null)
    {
    }

    /// <summary>
    /// Lists all page objects on specified pages of a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="pageRange">Optional list of page numbers to analyze. If null, analyzes all pages</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PdfPageObjectInfo objects containing object information</returns>
    public List<PdfPageObjectInfo> ListPageObjects(string inputFilename, List<int>? pageRange, string password = "", IProgress<PdfProgress>? progress = null)
    {
        var objectInfos = new List<PdfPageObjectInfo>();
        
        if (!IsValidPdfFile(inputFilename))
            return objectInfos;

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                string errorMessage = GetLastPdfiumError();
                Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                return objectInfos;
            }

            try
            {
                var numberOfPages = FPDF_GetPageCount(documentT);
                Logger.LogInformation("Analyzing page objects in {PageCount} pages", numberOfPages);

                for (int pageI = 0; pageI < numberOfPages; pageI++)
                {
                    if (pageRange != null && !pageRange.Contains(pageI + 1)) continue;

                    progress?.Report(new PdfProgress(pageI + 1, numberOfPages));

                    var pageT = FPDF_LoadPage(documentT, pageI);
                    var pageTextT = fpdf_text.FPDFTextLoadPage(pageT);
                    if (pageT == null)
                    {
                        Logger.LogWarning("Failed to load page {PageNumber}", pageI + 1);
                        continue;
                    }
                    if (pageTextT == null)
                    {
                        Logger.LogWarning("Failed to load text page for {PageNumber}", pageI + 1);
                        FPDF_ClosePage(pageT);
                        continue;
                    }

                    try
                    {
                        int objectCount = FPDFPageCountObjects(pageT);
                        Logger.LogDebug("Found {ObjectCount} objects on page {PageNumber}", objectCount, pageI + 1);

                        for (int i = 0; i < objectCount; i++)
                        {
                            var pageObject = FPDFPageGetObject(pageT, i);
                            if (pageObject == null) continue;

                            float left = 0, bottom = 0, right = 0, top = 0;
                            FPDFPageObjGetBounds(pageObject, ref left, ref bottom, ref right, ref top);

                            var objectType = FPDFPageObjGetType(pageObject);
                            var typeString = GetPageObjectType(objectType);

                            var objectInfo = new PdfPageObjectInfo
                            {
                                Type = typeString,
                                Left = left,
                                Bottom = bottom,
                                Right = right,
                                Top = top,
                                Page = pageI + 1,
                                Index = i,
                                ObjectId = (uint)(i + 1), // Use index + 1 as object ID for display purposes
                                TextContent = objectType == 1 ? ExtractTextFromObject(pageTextT, pageObject) : null // 1 = FPDF_PAGEOBJ_TEXT
                            };
                            objectInfos.Add(objectInfo);
                        }
                    }
                    finally
                    {
                        FPDF_ClosePage(pageT);
                        fpdf_text.FPDFTextClosePage(pageTextT);
                    }
                }

                Logger.LogInformation("Found {TotalObjects} objects across all analyzed pages", objectInfos.Count);
                progress?.Report(new PdfProgress(numberOfPages, numberOfPages));
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
        
        return objectInfos;
    }

    /// <summary>
    /// Removes a page object from a specific page at the given index
    /// </summary>
    /// <param name="inputFilename">Path to the input PDF file</param>
    /// <param name="outputFilename">Path where the modified PDF will be saved</param>
    /// <param name="pageNumber">Page number (1-based) from which to remove the object</param>
    /// <param name="objectIndex">Index of the object to remove (0-based)</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <returns>True if the object was successfully removed, false otherwise</returns>
    public bool RemovePageObject(string inputFilename, string outputFilename, int pageNumber, int objectIndex, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (pageNumber < 1)
            throw new ArgumentException("Page number must be 1 or greater", nameof(pageNumber));

        if (objectIndex < 0)
            throw new ArgumentException("Object index must be 0 or greater", nameof(objectIndex));

        if (!IsValidPdfFile(inputFilename))
            return false;

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                string errorMessage = GetLastPdfiumError();
                Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                return false;
            }

            try
            {
                var numberOfPages = FPDF_GetPageCount(documentT);
                if (pageNumber > numberOfPages)
                {
                    Logger.LogError("Page number {PageNumber} exceeds document page count {PageCount}", pageNumber, numberOfPages);
                    return false;
                }

                var pageT = FPDF_LoadPage(documentT, pageNumber - 1); // Convert to 0-based index
                if (pageT == null)
                {
                    Logger.LogError("Failed to load page {PageNumber}", pageNumber);
                    return false;
                }

                try
                {
                    int objectCount = FPDFPageCountObjects(pageT);
                    if (objectIndex >= objectCount)
                    {
                        Logger.LogError("Object index {ObjectIndex} exceeds object count {ObjectCount} on page {PageNumber}", 
                            objectIndex, objectCount, pageNumber);
                        return false;
                    }

                    var pageObject = FPDFPageGetObject(pageT, objectIndex);
                    if (pageObject == null)
                    {
                        Logger.LogError("Failed to get object at index {ObjectIndex} on page {PageNumber}", objectIndex, pageNumber);
                        return false;
                    }

                    // Remove the object from the page
                    var success = FPDFPageRemoveObject(pageT, pageObject);
                    if (success == 0)
                    {
                        Logger.LogError("Failed to remove object at index {ObjectIndex} on page {PageNumber}", objectIndex, pageNumber);
                        return false;
                    }

                    // Mark the page as modified
                    FPDFPageGenerateContent(pageT);

                    Logger.LogInformation("Successfully removed object at index {ObjectIndex} from page {PageNumber}", objectIndex, pageNumber);

                    // Save the modified document
                    byte[]? buffer = null;
                    FileStream? filestream = null;

                    try
                    {
                        // Create output directory if it doesn't exist
                        var outputDir = Path.GetDirectoryName(outputFilename);
                        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        filestream = new FileStream(outputFilename, FileMode.Create, FileAccess.Write);

                        var fileWrite = new FPDF_FILEWRITE_();
                        fileWrite.WriteBlock = (ignore, data, size) =>
                        {
                            try
                            {
                                if (buffer == null || buffer.Length < size)
                                    buffer = new byte[size];

                                Marshal.Copy(data, buffer, 0, (int)size);
                                filestream.Write(buffer, 0, (int)size);
                                filestream.Flush();
                                return 1;
                            }
                            catch (Exception writeEx)
                            {
                                Logger.LogError(writeEx, "Error writing PDF data: {Message}", writeEx.Message);
                                return 0;
                            }
                        };

                        var saveResult = FPDF_SaveAsCopy(documentT, fileWrite, 0);
                        if (saveResult == 0)
                        {
                            Logger.LogError("Failed to save the modified PDF document");
                            return false;
                        }

                        Logger.LogInformation("Successfully saved modified PDF to {OutputFile}", outputFilename);
                        return true;
                    }
                    finally
                    {
                        filestream?.Dispose();
                    }
                }
                finally
                {
                    FPDF_ClosePage(pageT);
                }
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    /// <summary>
    /// Async version of RemovePageObject
    /// </summary>
    public async Task<bool> RemovePageObjectAsync(string inputFilename, string outputFilename, int pageNumber, int objectIndex, string password = "")
    {
        return await Task.Run(() => RemovePageObject(inputFilename, outputFilename, pageNumber, objectIndex, password));
    }

    /// <summary>
    /// Extracts text content from a text page object
    /// </summary>
    /// <param name="pageObject">The page object to extract text from</param>
    /// <returns>The text content, limited to 30 characters, or null if extraction fails</returns>
    private string? ExtractTextFromObject(FpdfTextpageT fpdfTextpageT, FpdfPageobjectT pageObject)
    {
        try
        {
            unsafe
            {
                float left = 0, bottom = 0, right = 0, top = 0;
                if (FPDFPageObjGetBounds(pageObject, ref left, ref bottom, ref right, ref top) > 0)
                {
                    int charCount = fpdf_text.FPDFTextGetBoundedText(fpdfTextpageT, left, bottom, right, top, ref *(ushort*)IntPtr.Zero, 0);
                    string result = GetUtf16TextString(charCount, ptr => 
                    {
                        return (uint)FPDFTextGetBoundedText(fpdfTextpageT, left, bottom, right, top, ref *(ushort*)ptr.ToPointer(), charCount);
                    });
                    return result;
                }
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogDebug("Failed to extract text from object: {Error}", ex.Message);
            return null;
        }
    }

}
