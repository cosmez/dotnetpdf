using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PDFiumCore;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdfview;
using static PDFiumCore.fpdf_save;
using static PDFiumCore.fpdf_ppo;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

/// <summary>
/// Service for PDF editing operations like splitting, merging, rotating, and page manipulation
/// </summary>
public class PdfEditService : BasePdfService
{
    /// <summary>
    /// Initializes a new instance of the PdfEditService
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory for logging operations</param>
    public PdfEditService(ILoggerFactory? loggerFactory = null) 
        : base((loggerFactory?.CreateLogger<PdfEditService>()) ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PdfEditService>.Instance, loggerFactory)
    {
    }

    /// <summary>
    /// Splits a PDF document into multiple individual pages.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputLocation">The location where the output PDF pages will be saved.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <param name="outputName">
    ///     The optional output file name pattern. Use "{original}" to include the original file name
    ///     and "{page}" to include the page number. Defaults to "{original}-{page}".
    /// </param>
    /// <param name="pageRange">
    ///     The optional list of page numbers or page range to include in the output. If not specified, all pages are included.
    /// </param>
    /// <param name="useBookmarks">
    ///     Indicates whether to use bookmarks (if available) to generate output file names.
    ///     If true, the bookmark titles will be used as the file names for corresponding pages.
    ///     If a bookmark has the same page number as another bookmark, the latter one will override the former.
    ///     Defaults to false.
    /// </param>
    /// <param name="progress">The optional progress reporting mechanism.</param>
    /// <param name="outputScriptNames">Optional custom names for specific pages</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task SplitPdfAsync(string inputFilename, string outputLocation, string password = "",
        string? outputName = "{original}-{page}", List<int>? pageRange = null,
        bool useBookmarks = false, IProgress<PdfSplitProgress>? progress = null,
        Dictionary<int, string>? outputScriptNames = null)
    {
        await Task.Run(() => SplitPdf(inputFilename, outputLocation, password, outputName, pageRange, useBookmarks, progress, outputScriptNames));
    }

    /// <summary>
    /// Splits a PDF document into multiple individual pages.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputLocation">The location where the output PDF pages will be saved.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <param name="outputName">
    ///     The optional output file name pattern. Use "{original}" to include the original file name
    ///     and "{page}" to include the page number. Defaults to "{original}-{page}".
    /// </param>
    /// <param name="pageRange">
    ///     The optional list of page numbers or page range to include in the output. If not specified, all pages are included.
    /// </param>
    /// <param name="useBookmarks">
    ///     Indicates whether to use bookmarks (if available) to generate output file names.
    ///     If true, the bookmark titles will be used as the file names for corresponding pages.
    ///     If a bookmark has the same page number as another bookmark, the latter one will override the former.
    ///     Defaults to false.
    /// </param>
    /// <param name="progress">The optional progress reporting mechanism.</param>
    /// <param name="outputScriptNames">Optional custom names for specific pages</param>
    /// <exception cref="System.Exception">Thrown if failed to create the document.</exception>
    public void SplitPdf(string inputFilename, string outputLocation, string password = "",
        string? outputName = "{original}-{page}", List<int>? pageRange = null,
        bool useBookmarks = false, IProgress<PdfSplitProgress>? progress = null,
        Dictionary<int, string>? outputScriptNames = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputLocation))
            throw new ArgumentException("Output location cannot be null or empty", nameof(outputLocation));

        if (!IsValidPdfFile(inputFilename))
            return;

        lock (PdfiumLock)
        {
            InitLibrary();
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    string errorMessage = GetLastPdfiumError();
                    Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                    throw new InvalidOperationException($"Failed to load PDF document: {errorMessage}");
                }

                try
                {
                    string namePart = RemoveInvalidChars(Path.GetFileNameWithoutExtension(inputFilename));

                    Dictionary<int, string> pageNames = new();
                    if (useBookmarks)
                    {
                        try
                        {
                            var bookmarkService = new PdfBookmarkService(LoggerFactory?.CreateLogger<PdfBookmarkService>() ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PdfBookmarkService>.Instance);
                            var bookmarks = bookmarkService.GetPdfBookmarks(inputFilename, password);
                            if (bookmarks != null)
                            {
                                foreach (var bookmark in bookmarks.Where(b => b.Page > 0))
                                {
                                    pageNames[bookmark.Page] = bookmark.Title;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning("Failed to extract bookmarks: {Error}", ex.Message);
                        }
                    }

                    var numberOfPages = FPDF_GetPageCount(documentT);
                    Logger.LogInformation("Splitting PDF into {PageCount} pages", numberOfPages);

                    for (int i = 0; i < numberOfPages; i++)
                    {
                        if (pageRange != null && !pageRange.Contains(i + 1)) continue;

                        string pageNamePart;
                        if (pageNames.TryGetValue(i + 1, out var name))
                        {
                            string bookMarkName = RemoveInvalidChars(name);
                            bookMarkName = (bookMarkName.Length > 100) ? bookMarkName[..100] : bookMarkName;
                            pageNamePart = bookMarkName;
                        }
                        else
                        {
                            pageNamePart = $"{namePart}-{i + 1:D3}";
                            if (!string.IsNullOrEmpty(outputName))
                            {
                                pageNamePart = outputName.Replace("{original}", namePart)
                                    .Replace("{page}", $"{i + 1:D3}");
                            }
                        }

                        if (outputScriptNames is not null && outputScriptNames.ContainsKey(i + 1))
                        {
                            pageNamePart = outputScriptNames[i + 1];
                        }

                        string pdfFilename = $"{pageNamePart}.pdf";

                        if (!Directory.Exists(outputLocation))
                            Directory.CreateDirectory(outputLocation);

                        string fullFilename = Path.Combine(outputLocation, pdfFilename);

                        progress?.Report(new PdfSplitProgress(i + 1, numberOfPages, fullFilename));

                        var newDocumentT = FPDF_CreateNewDocument();
                        if (newDocumentT == null)
                            throw new InvalidOperationException("Failed to create new PDF document");

                        try
                        {
                            FPDF_ImportPages(newDocumentT, documentT, $"{i + 1}", 0);

                            byte[]? buffer = null;
                            using var filestream = File.OpenWrite(fullFilename);

                            var fileWrite = new FPDF_FILEWRITE_();
                            fileWrite.WriteBlock = (ignore, data, size) =>
                            {
                                if (buffer == null || buffer.Length < size)
                                    buffer = new byte[size];

                                Marshal.Copy(data, buffer, 0, (int)size);
                                filestream?.Write(buffer, 0, (int)size);
                                return 1;
                            };

                            var success = FPDF_SaveAsCopy(newDocumentT, fileWrite, 0);
                            if (success == 0)
                            {
                                throw new InvalidOperationException($"Failed to save PDF page {i + 1}");
                            }
                        }
                        finally
                        {
                            FPDF_CloseDocument(newDocumentT);
                        }
                    }

                    progress?.Report(new PdfSplitProgress(numberOfPages, numberOfPages, ""));
                    Logger.LogInformation("Successfully split PDF into {PageCount} files", numberOfPages);
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error splitting PDF: {Message}", ex.Message);
                throw new InvalidOperationException($"Error splitting PDF: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Merges multiple PDF files into a single PDF file.
    /// </summary>
    /// <param name="outputFilename">The filename of the output PDF file.</param>
    /// <param name="inputFilenames">A list of input PDF filenames to be merged.</param>
    /// <param name="password">Optional password to open the input PDF files.</param>
    /// <param name="deleteOriginal">Determines whether to delete the original input files after merging. Default is <c>false</c>.</param>
    /// <param name="progress">Optional progress reporter for tracking the merge progress. Use <c>null</c> if not needed.</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task MergeFilesAsync(string outputFilename, List<string> inputFilenames, string password = "",
        bool deleteOriginal = false, IProgress<PdfProgress>? progress = null)
    {
        await Task.Run(() => MergeFiles(outputFilename, inputFilenames, password, deleteOriginal, progress));
    }

    /// <summary>
    /// Merges multiple PDF files into a single PDF file.
    /// </summary>
    /// <param name="outputFilename">The filename of the output PDF file.</param>
    /// <param name="inputFilenames">A list of input PDF filenames to be merged.</param>
    /// <param name="password">Optional password to open the input PDF files.</param>
    /// <param name="deleteOriginal">Determines whether to delete the original input files after merging. Default is <c>false</c>.</param>
    /// <param name="progress">Optional progress reporter for tracking the merge progress. Use <c>null</c> if not needed.</param>
    public void MergeFiles(string outputFilename, List<string> inputFilenames, string password = "",
        bool deleteOriginal = false, IProgress<PdfProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));
        
        if (inputFilenames == null || inputFilenames.Count == 0)
            throw new ArgumentException("Input filenames list cannot be null or empty", nameof(inputFilenames));

        lock (PdfiumLock)
        {
            InitLibrary();
            try
            {
                if (!outputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Output filename must have .pdf extension", nameof(outputFilename));

                var newDocumentT = FPDF_CreateNewDocument();
                if (newDocumentT == null)
                    throw new InvalidOperationException("Failed to create new PDF document");

                try
                {
                    int currentPageIndex = 0;
                    int idx = 0;

                    Logger.LogInformation("Merging {FileCount} PDF files", inputFilenames.Count);

                    foreach (var inputFilename in inputFilenames)
                    {
                        progress?.Report(new PdfProgress(idx++, inputFilenames.Count));

                        if (!File.Exists(inputFilename))
                        {
                            Logger.LogWarning("File not found, skipping: {Filename}", inputFilename);
                            continue;
                        }

                        var mDocumentT = FPDF_LoadDocument(inputFilename, password);
                        if (mDocumentT == null)
                        {
                            string errorMessage = GetLastPdfiumError();
                            Logger.LogWarning("Failed to load PDF, skipping: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                            continue;
                        }

                        try
                        {
                            var numberOfPages = FPDF_GetPageCount(mDocumentT);
                            FPDF_ImportPages(newDocumentT, mDocumentT, $"1-{numberOfPages}", currentPageIndex);
                            currentPageIndex += numberOfPages;

                            if (deleteOriginal)
                                File.Delete(inputFilename);
                        }
                        finally
                        {
                            FPDF_CloseDocument(mDocumentT);
                        }
                    }

                    byte[]? buffer = null;
                    using var filestream = File.OpenWrite(outputFilename);

                    var fileWrite = new FPDF_FILEWRITE_();
                    fileWrite.WriteBlock = (ignore, data, size) =>
                    {
                        if (buffer == null || buffer.Length < size)
                            buffer = new byte[size];

                        Marshal.Copy(data, buffer, 0, (int)size);
                        filestream?.Write(buffer, 0, (int)size);
                        return 1;
                    };

                    var success = FPDF_SaveAsCopy(newDocumentT, fileWrite, 0);
                    if (success == 0)
                    {
                        throw new InvalidOperationException("Failed to save merged PDF document");
                    }

                    progress?.Report(new PdfProgress(inputFilenames.Count, inputFilenames.Count));
                    Logger.LogInformation("Successfully merged {FileCount} files into {OutputFile}", inputFilenames.Count, outputFilename);
                }
                finally
                {
                    FPDF_CloseDocument(newDocumentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error merging PDF files: {Message}", ex.Message);
                throw new InvalidOperationException($"Error merging PDF files: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Rotates specific pages in a PDF document.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the rotated PDF will be saved.</param>
    /// <param name="rotation">The rotation angle (90, 180, 270 degrees).</param>
    /// <param name="pageRange">The list of page numbers to rotate. If null, all pages are rotated.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task RotatePdfPagesAsync(string inputFilename, string outputFilename, int rotation, 
        List<int>? pageRange = null, string password = "")
    {
        await Task.Run(() => RotatePdfPages(inputFilename, outputFilename, rotation, pageRange, password));
    }

    /// <summary>
    /// Rotates specific pages in a PDF document.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the rotated PDF will be saved.</param>
    /// <param name="rotation">The rotation angle (90, 180, 270 degrees).</param>
    /// <param name="pageRange">The list of page numbers to rotate. If null, all pages are rotated.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    public void RotatePdfPages(string inputFilename, string outputFilename, int rotation, 
        List<int>? pageRange = null, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentException("Rotation must be 90, 180, or 270 degrees", nameof(rotation));

        if (!IsValidPdfFile(inputFilename))
            return;

        lock (PdfiumLock)
        {
            InitLibrary();
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    string errorMessage = GetLastPdfiumError();
                    Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                    throw new InvalidOperationException($"Failed to load PDF document: {errorMessage}");
                }

                try
                {
                    var numberOfPages = FPDF_GetPageCount(documentT);
                    Logger.LogInformation("Rotating pages in PDF with {PageCount} pages", numberOfPages);

                    for (int i = 0; i < numberOfPages; i++)
                    {
                        if (pageRange != null && !pageRange.Contains(i + 1)) continue;

                        var pageT = FPDF_LoadPage(documentT, i);
                        if (pageT != null)
                        {
                            try
                            {
                                int rotationValue = rotation / 90;
                                FPDFPageSetRotation(pageT, rotationValue);
                            }
                            finally
                            {
                                FPDF_ClosePage(pageT);
                            }
                        }
                    }

                    // Save the modified document
                    byte[]? buffer = null;
                    using var filestream = File.OpenWrite(outputFilename);

                    var fileWrite = new FPDF_FILEWRITE_();
                    fileWrite.WriteBlock = (ignore, data, size) =>
                    {
                        if (buffer == null || buffer.Length < size)
                            buffer = new byte[size];

                        Marshal.Copy(data, buffer, 0, (int)size);
                        filestream.Write(buffer, 0, (int)size);
                        return 1;
                    };

                    var success = FPDF_SaveAsCopy(documentT, fileWrite, 0);
                    if (success == 0)
                        throw new InvalidOperationException("Failed to save the rotated PDF document.");

                    Logger.LogInformation("Successfully rotated pages and saved to {OutputFile}", outputFilename);
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error rotating PDF pages: {Message}", ex.Message);
                throw new InvalidOperationException($"Error rotating PDF pages: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Removes specific pages from a PDF document.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the modified PDF will be saved.</param>
    /// <param name="pagesToRemove">The list of page numbers to remove (1-based).</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task RemovePdfPagesAsync(string inputFilename, string outputFilename, List<int> pagesToRemove, string password = "")
    {
        await Task.Run(() => RemovePdfPages(inputFilename, outputFilename, pagesToRemove, password));
    }

    /// <summary>
    /// Removes specific pages from a PDF document.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the modified PDF will be saved.</param>
    /// <param name="pagesToRemove">The list of page numbers to remove (1-based).</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    public void RemovePdfPages(string inputFilename, string outputFilename, List<int> pagesToRemove, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (pagesToRemove == null || !pagesToRemove.Any())
            throw new ArgumentException("Pages to remove cannot be null or empty", nameof(pagesToRemove));

        if (!IsValidPdfFile(inputFilename))
            return;

        lock (PdfiumLock)
        {
            InitLibrary();
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    string errorMessage = GetLastPdfiumError();
                    Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                    throw new InvalidOperationException($"Failed to load PDF document: {errorMessage}");
                }

                try
                {
                    var numberOfPages = FPDF_GetPageCount(documentT);

                    // Sort pages in descending order to remove from end to beginning
                    var sortedPages = pagesToRemove.Where(p => p >= 1 && p <= numberOfPages)
                                                  .OrderByDescending(p => p)
                                                  .ToList();

                    Logger.LogInformation("Removing {PageCount} pages from PDF", sortedPages.Count);

                    foreach (var pageNum in sortedPages)
                    {
                        FPDFPageDelete(documentT, pageNum - 1); // Convert to 0-based index
                    }

                    // Save the modified document
                    byte[]? buffer = null;
                    using var filestream = File.OpenWrite(outputFilename);

                    var fileWrite = new FPDF_FILEWRITE_();
                    fileWrite.WriteBlock = (ignore, data, size) =>
                    {
                        if (buffer == null || buffer.Length < size)
                            buffer = new byte[size];

                        Marshal.Copy(data, buffer, 0, (int)size);
                        filestream.Write(buffer, 0, (int)size);
                        return 1;
                    };

                    var success = FPDF_SaveAsCopy(documentT, fileWrite, 0);
                    if (success == 0)
                        throw new InvalidOperationException("Failed to save the modified PDF document.");

                    Logger.LogInformation("Successfully removed pages and saved to {OutputFile}", outputFilename);
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error removing PDF pages: {Message}", ex.Message);
                throw new InvalidOperationException($"Error removing PDF pages: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Inserts blank pages into a PDF document at specified positions.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the modified PDF will be saved.</param>
    /// <param name="insertPositions">Dictionary where key is the position (1-based) and value is the number of blank pages to insert.</param>
    /// <param name="pageWidth">Width of the blank pages in points (default: 612 for Letter size).</param>
    /// <param name="pageHeight">Height of the blank pages in points (default: 792 for Letter size).</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task InsertBlankPagesAsync(string inputFilename, string outputFilename, Dictionary<int, int> insertPositions,
        double pageWidth = 612, double pageHeight = 792, string password = "")
    {
        await Task.Run(() => InsertBlankPages(inputFilename, outputFilename, insertPositions, pageWidth, pageHeight, password));
    }

    /// <summary>
    /// Inserts blank pages into a PDF document at specified positions.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the modified PDF will be saved.</param>
    /// <param name="insertPositions">Dictionary where key is the position (1-based) and value is the number of blank pages to insert.</param>
    /// <param name="pageWidth">Width of the blank pages in points (default: 612 for Letter size).</param>
    /// <param name="pageHeight">Height of the blank pages in points (default: 792 for Letter size).</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    public void InsertBlankPages(string inputFilename, string outputFilename, Dictionary<int, int> insertPositions,
        double pageWidth = 612, double pageHeight = 792, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (insertPositions == null || !insertPositions.Any())
            throw new ArgumentException("Insert positions cannot be null or empty", nameof(insertPositions));

        if (!IsValidPdfFile(inputFilename))
            return;

        lock (PdfiumLock)
        {
            InitLibrary();
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    string errorMessage = GetLastPdfiumError();
                    Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                    throw new InvalidOperationException($"Failed to load PDF document: {errorMessage}");
                }

                try
                {
                    // Sort positions in descending order to insert from end to beginning
                    var sortedPositions = insertPositions.OrderByDescending(kvp => kvp.Key).ToList();

                    int totalPagesToInsert = sortedPositions.Sum(p => p.Value);
                    Logger.LogInformation("Inserting {PageCount} blank pages at {PositionCount} positions",
                        totalPagesToInsert, sortedPositions.Count);

                    foreach (var position in sortedPositions)
                    {
                        int insertAtIndex = position.Key - 1; // Convert to 0-based index
                        int numberOfPages = position.Value;

                        for (int i = 0; i < numberOfPages; i++)
                        {
                            var newPageT = FPDFPageNew(documentT, insertAtIndex, pageWidth, pageHeight);
                            if (newPageT == null)
                                throw new InvalidOperationException($"Failed to create blank page at position {position.Key}");

                            FPDF_ClosePage(newPageT);
                        }
                    }

                    // Save the modified document
                    byte[]? buffer = null;
                    using var filestream = File.OpenWrite(outputFilename);

                    var fileWrite = new FPDF_FILEWRITE_();
                    fileWrite.WriteBlock = (ignore, data, size) =>
                    {
                        if (buffer == null || buffer.Length < size)
                            buffer = new byte[size];

                        Marshal.Copy(data, buffer, 0, (int)size);
                        filestream.Write(buffer, 0, (int)size);
                        return 1;
                    };

                    var success = FPDF_SaveAsCopy(documentT, fileWrite, 0);
                    if (success == 0)
                        throw new InvalidOperationException("Failed to save the modified PDF document.");

                    Logger.LogInformation("Successfully inserted blank pages and saved to {OutputFile}", outputFilename);
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error inserting blank pages: {Message}", ex.Message);
                throw new InvalidOperationException($"Error inserting blank pages: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Reorders pages in a PDF document according to the specified page order.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the reordered PDF will be saved.</param>
    /// <param name="newPageOrder">List of page numbers in the desired order (1-based). Must contain all pages exactly once.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task ReorderPdfPagesAsync(string inputFilename, string outputFilename, List<int> newPageOrder, string password = "")
    {
        await Task.Run(() => ReorderPdfPages(inputFilename, outputFilename, newPageOrder, password));
    }

    /// <summary>
    /// Reorders pages in a PDF document according to the specified page order.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputFilename">The path where the reordered PDF will be saved.</param>
    /// <param name="newPageOrder">List of page numbers in the desired order (1-based). Must contain all pages exactly once.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    public void ReorderPdfPages(string inputFilename, string outputFilename, List<int> newPageOrder, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (newPageOrder == null || !newPageOrder.Any())
            throw new ArgumentException("New page order cannot be null or empty", nameof(newPageOrder));

        if (!IsValidPdfFile(inputFilename))
            return;

        lock (PdfiumLock)
        {
            InitLibrary();
            try
            {
                var sourceDocT = FPDF_LoadDocument(inputFilename, password);
                if (sourceDocT == null)
                {
                    string errorMessage = GetLastPdfiumError();
                    Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                    throw new InvalidOperationException($"Failed to load PDF document: {errorMessage}");
                }

                try
                {
                    var numberOfPages = FPDF_GetPageCount(sourceDocT);

                    // Validate that newPageOrder contains all pages exactly once
                    if (newPageOrder.Count != numberOfPages ||
                        newPageOrder.Any(p => p < 1 || p > numberOfPages) ||
                        newPageOrder.Distinct().Count() != newPageOrder.Count)
                    {
                        throw new ArgumentException("New page order must contain all pages exactly once", nameof(newPageOrder));
                    }

                    Logger.LogInformation("Reordering {PageCount} pages in PDF", numberOfPages);

                    // Create a new document
                    var destDocT = FPDF_CreateNewDocument();
                    if (destDocT == null)
                        throw new InvalidOperationException("Failed to create new PDF document");

                    try
                    {
                        // Import pages in the new order
                        foreach (var pageNum in newPageOrder)
                        {
                            int sourcePageIndex = pageNum - 1; // Convert to 0-based index

                            var success = FPDF_ImportPages(destDocT, sourceDocT, $"{sourcePageIndex + 1}", 0);
                            if (success == 0)
                            {
                                throw new InvalidOperationException($"Failed to import page {pageNum}");
                            }
                        }

                        // Save the reordered document
                        byte[]? buffer = null;
                        using var filestream = File.OpenWrite(outputFilename);

                        var fileWrite = new FPDF_FILEWRITE_();
                        fileWrite.WriteBlock = (ignore, data, size) =>
                        {
                            if (buffer == null || buffer.Length < size)
                                buffer = new byte[size];

                            Marshal.Copy(data, buffer, 0, (int)size);
                            filestream.Write(buffer, 0, (int)size);
                            return 1;
                        };

                        var result = FPDF_SaveAsCopy(destDocT, fileWrite, 0);
                        if (result == 0)
                            throw new InvalidOperationException("Failed to save the reordered PDF document.");

                        Logger.LogInformation("Successfully reordered pages and saved to {OutputFile}", outputFilename);
                    }
                    finally
                    {
                        FPDF_CloseDocument(destDocT);
                    }
                }
                finally
                {
                    FPDF_CloseDocument(sourceDocT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error reordering PDF pages: {Message}", ex.Message);
                throw new InvalidOperationException($"Error reordering PDF pages: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Unlocks a password-protected PDF by loading it with the password and saving it without security restrictions
    /// </summary>
    /// <param name="inputFilename">The path of the input password-protected PDF file</param>
    /// <param name="outputFilename">The path where the unlocked PDF will be saved</param>
    /// <param name="password">The password required to open the input PDF</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task UnlockPdfAsync(string inputFilename, string outputFilename, string password)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        await Task.Run(() =>
        {
            Logger.LogInformation("Unlocking PDF: {InputFile} -> {OutputFile}", inputFilename, outputFilename);

            // Initialize PDFium library if not already initialized
            

            lock (PdfiumLock)
            {
                try
                {
                    InitLibrary();
                    Logger.LogInformation("Attempting to load PDF document with password");
                    var documentT = FPDF_LoadDocument(inputFilename, password);
                    if (documentT == null)
                    {
                        string errorMessage = GetLastPdfiumError();
                        Logger.LogError("Failed to load PDF document: {ErrorMessage}", errorMessage);
                        throw new InvalidOperationException($"Failed to load PDF document: {errorMessage}");
                    }

                    Logger.LogInformation("Successfully loaded PDF document, starting unlock process");

                    try
                    {
                        // Create output directory if it doesn't exist
                        var outputDir = Path.GetDirectoryName(outputFilename);
                        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                        {
                            Directory.CreateDirectory(outputDir);
                        }

                        // Save the document without security restrictions
                        byte[]? buffer = null;
                        FileStream? filestream = null;
                        
                        try
                        {
                            // Ensure we create a new file and truncate if it exists
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
                                    filestream.Flush(); // Ensure data is written immediately
                                    return 1;
                                }
                                catch (Exception writeEx)
                                {
                                    Logger.LogError(writeEx, "Error writing PDF data: {Message}", writeEx.Message);
                                    return 0; // Return 0 to indicate failure
                                }
                            };

                            Logger.LogInformation("Attempting to save unlocked PDF document");
                            // Try flag 0 first (removes all security)
                            var success = FPDF_SaveAsCopy(documentT, fileWrite, (int)PdfSaveFlags.FPDF_REMOVE_SECURITY);
                            if (success == 0)
                            {
                                // If flag 0 fails, try flag 1 (incremental update)
                                Logger.LogWarning($"FPDF_SaveAsCopy with flag {PdfSaveFlags.FPDF_REMOVE_SECURITY} failed, trying flag {PdfSaveFlags.FPDF_INCREMENTAL}");
                                filestream.Position = 0; // Reset stream position
                                filestream.SetLength(0); // Clear the file
                                success = FPDF_SaveAsCopy(documentT, fileWrite, (int)PdfSaveFlags.FPDF_INCREMENTAL);
                                
                                if (success == 0)
                                {
                                    Logger.LogError("All FPDF_SaveAsCopy attempts failed");
                                    throw new InvalidOperationException("Failed to save the unlocked PDF document using any available method.");
                                }
                            }

                            // Ensure all data is written to disk
                            filestream.Flush();
                            filestream.Close();
                            
                            Logger.LogInformation("Successfully unlocked PDF document");
                        }
                        finally
                        {
                            filestream?.Dispose();
                        }
                    }
                    finally
                    {
                        FPDF_CloseDocument(documentT);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error unlocking PDF: {Message}", ex.Message);
                    throw new InvalidOperationException($"Error unlocking PDF: {ex.Message}", ex);
                }
            }
        });
    }

    /// <summary>
    /// Removes invalid file name characters from a string
    /// </summary>
    /// <param name="input">Input string</param>
    /// <returns>Cleaned string</returns>
    private static string RemoveInvalidChars(string input)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            input = input.Replace(c, '_');
        }
        return input;
    }
}
