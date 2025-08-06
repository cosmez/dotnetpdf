using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Services;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core;

/// <summary>
/// Main class providing comprehensive PDF processing functionality
/// </summary>
public class PdfProcessor
{
    private readonly PdfTextExtractionService _textService;
    private readonly PdfBookmarkService _bookmarkService;
    private readonly PdfInformationService _informationService;
    private readonly PdfAttachmentService _attachmentService;
    private readonly PdfPageObjectService _pageObjectService;
    private readonly PdfFormFieldService _formFieldService;
    private readonly PdfWatermarkService _watermarkService;
    private readonly PdfEditService _editService;
    private readonly PdfRenderService _renderService;

    /// <summary>
    /// Initializes a new instance of the PdfProcessor class
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory for logging. If null, uses NullLoggerFactory (no logging)</param>
    public PdfProcessor(ILoggerFactory? loggerFactory = null)
    {
        // Use provided logger factory or create a null logger factory as fallback
        var factory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        
        _textService = new PdfTextExtractionService(factory.CreateLogger<PdfTextExtractionService>());
        _bookmarkService = new PdfBookmarkService(factory.CreateLogger<PdfBookmarkService>());
        _informationService = new PdfInformationService(factory.CreateLogger<PdfInformationService>());
        _attachmentService = new PdfAttachmentService(factory.CreateLogger<PdfAttachmentService>());
        _pageObjectService = new PdfPageObjectService(factory.CreateLogger<PdfPageObjectService>());
        _formFieldService = new PdfFormFieldService(factory.CreateLogger<PdfFormFieldService>());
        _watermarkService = new PdfWatermarkService(factory.CreateLogger<PdfWatermarkService>());
        _editService = new PdfEditService(factory);
        _renderService = new PdfRenderService(factory);
    }

    #region Text Extraction

    /// <summary>
    /// Extracts text from all pages or specified pages of a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="pageRange">Optional list of page numbers to extract from. If null, extracts from all pages</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PDfPageText objects containing text from each page</returns>
    public List<PDfPageText>? GetPdfText(string inputFilename, List<int>? pageRange = null, string password = "", IProgress<PdfTextProgress>? progress = null)
    {
        return _textService.GetPdfText(inputFilename, pageRange, password, progress);
    }

    #endregion

    #region Bookmarks

    /// <summary>
    /// Extracts all bookmarks from a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PDfBookmark objects containing bookmark information</returns>
    public List<PDfBookmark>? GetPdfBookmarks(string inputFilename, string password = "", IProgress<PdfBookmarkProgress>? progress = null)
    {
        return _bookmarkService.GetPdfBookmarks(inputFilename, password, progress);
    }

    #endregion

    #region Document Information

    /// <summary>
    /// Extracts metadata and information from a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <returns>PdfInfo object containing document metadata</returns>
    public PdfInfo? GetPdfInformation(string inputFilename, string password = "")
    {
        return _informationService.GetPdfInformation(inputFilename, password);
    }

    #endregion

    #region Attachments

    /// <summary>
    /// Gets a list of attachments from a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PDF attachments with metadata</returns>
    public List<PdfAttachment>? GetPdfAttachments(string inputFilename, string password = "", IProgress<PdfProgress>? progress = null)
    {
        return _attachmentService.GetPdfAttachments(inputFilename, password, progress);
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
        return _attachmentService.ExtractAttachment(inputFilename, attachmentIndex, outputPath, password, progress);
    }

    #endregion

    #region Page Objects

    /// <summary>
    /// Lists all page objects on specified pages of a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="pageRange">Optional list of page numbers to analyze. If null, analyzes all pages</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PdfPageObjectInfo objects containing object information</returns>
    public List<PdfPageObjectInfo> ListPageObjects(string inputFilename, List<int>? pageRange = null, string password = "", IProgress<PdfProgress>? progress = null)
    {
        return _pageObjectService.ListPageObjects(inputFilename, pageRange, password, progress);
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
        return _pageObjectService.RemovePageObject(inputFilename, outputFilename, pageNumber, objectIndex, password);
    }

    /// <summary>
    /// Async version of RemovePageObject
    /// </summary>
    /// <param name="inputFilename">Path to the input PDF file</param>
    /// <param name="outputFilename">Path where the modified PDF will be saved</param>
    /// <param name="pageNumber">Page number (1-based) from which to remove the object</param>
    /// <param name="objectIndex">Index of the object to remove (0-based)</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <returns>True if the object was successfully removed, false otherwise</returns>
    public async Task<bool> RemovePageObjectAsync(string inputFilename, string outputFilename, int pageNumber, int objectIndex, string password = "")
    {
        return await _pageObjectService.RemovePageObjectAsync(inputFilename, outputFilename, pageNumber, objectIndex, password);
    }

    #endregion

    #region Form Fields

    /// <summary>
    /// Lists all form fields in a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PdfFormFieldInfo objects containing form field information</returns>
    public List<PdfFormFieldInfo> ListFormFields(string inputFilename, string password = "", IProgress<PdfProgress>? progress = null)
    {
        return _formFieldService.ListFormFields(inputFilename, password, progress);
    }

    #endregion

    #region Watermarks

    /// <summary>
    /// Adds a text or image watermark to all pages of a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the input PDF file</param>
    /// <param name="outputFilename">Path to the output PDF file</param>
    /// <param name="options">Watermark configuration options</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    public void AddWatermark(string inputFilename, string outputFilename, WatermarkOptions options, string password = "", IProgress<PdfProgress>? progress = null)
    {
        _watermarkService.AddWatermark(inputFilename, outputFilename, options, password, progress);
    }

    #endregion

    #region PDF Editing Operations

    /// <summary>
    /// Splits a PDF document into multiple individual pages.
    /// </summary>
    /// <param name="inputFilename">The path of the input PDF file.</param>
    /// <param name="outputLocation">The location where the output PDF pages will be saved.</param>
    /// <param name="password">The optional password to unlock the input PDF file.</param>
    /// <param name="outputName">The optional output file name pattern. Use "{original}" to include the original file name and "{page}" to include the page number. Defaults to "{original}-{page}".</param>
    /// <param name="pageRange">The optional list of page numbers or page range to include in the output. If not specified, all pages are included.</param>
    /// <param name="useBookmarks">Indicates whether to use bookmarks (if available) to generate output file names.</param>
    /// <param name="progress">The optional progress reporting mechanism.</param>
    /// <param name="outputScriptNames">Optional custom names for specific pages</param>
    public void SplitPdf(string inputFilename, string outputLocation, string password = "",
        string? outputName = "{original}-{page}", List<int>? pageRange = null,
        bool useBookmarks = false, IProgress<PdfSplitProgress>? progress = null,
        Dictionary<int, string>? outputScriptNames = null)
    {
        _editService.SplitPdf(inputFilename, outputLocation, password, outputName, pageRange, useBookmarks, progress, outputScriptNames);
    }

    /// <summary>
    /// Splits a PDF document into multiple individual pages asynchronously.
    /// </summary>
    public async Task SplitPdfAsync(string inputFilename, string outputLocation, string password = "",
        string? outputName = "{original}-{page}", List<int>? pageRange = null,
        bool useBookmarks = false, IProgress<PdfSplitProgress>? progress = null,
        Dictionary<int, string>? outputScriptNames = null)
    {
        await _editService.SplitPdfAsync(inputFilename, outputLocation, password, outputName, pageRange, useBookmarks, progress, outputScriptNames);
    }

    /// <summary>
    /// Merges multiple PDF files into a single PDF file.
    /// </summary>
    /// <param name="outputFilename">The filename of the output PDF file.</param>
    /// <param name="inputFilenames">A list of input PDF filenames to be merged.</param>
    /// <param name="password">Optional password to open the input PDF files.</param>
    /// <param name="deleteOriginal">Determines whether to delete the original input files after merging. Default is false.</param>
    /// <param name="progress">Optional progress reporter for tracking the merge progress.</param>
    public void MergeFiles(string outputFilename, List<string> inputFilenames, string password = "",
        bool deleteOriginal = false, IProgress<PdfProgress>? progress = null)
    {
        _editService.MergeFiles(outputFilename, inputFilenames, password, deleteOriginal, progress);
    }

    /// <summary>
    /// Merges multiple PDF files into a single PDF file asynchronously.
    /// </summary>
    public async Task MergeFilesAsync(string outputFilename, List<string> inputFilenames, string password = "",
        bool deleteOriginal = false, IProgress<PdfProgress>? progress = null)
    {
        await _editService.MergeFilesAsync(outputFilename, inputFilenames, password, deleteOriginal, progress);
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
        _editService.RotatePdfPages(inputFilename, outputFilename, rotation, pageRange, password);
    }

    /// <summary>
    /// Rotates specific pages in a PDF document asynchronously.
    /// </summary>
    public async Task RotatePdfPagesAsync(string inputFilename, string outputFilename, int rotation,
        List<int>? pageRange = null, string password = "")
    {
        await _editService.RotatePdfPagesAsync(inputFilename, outputFilename, rotation, pageRange, password);
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
        _editService.RemovePdfPages(inputFilename, outputFilename, pagesToRemove, password);
    }

    /// <summary>
    /// Removes specific pages from a PDF document asynchronously.
    /// </summary>
    public async Task RemovePdfPagesAsync(string inputFilename, string outputFilename, List<int> pagesToRemove, string password = "")
    {
        await _editService.RemovePdfPagesAsync(inputFilename, outputFilename, pagesToRemove, password);
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
        _editService.InsertBlankPages(inputFilename, outputFilename, insertPositions, pageWidth, pageHeight, password);
    }

    /// <summary>
    /// Inserts blank pages into a PDF document at specified positions asynchronously.
    /// </summary>
    public async Task InsertBlankPagesAsync(string inputFilename, string outputFilename, Dictionary<int, int> insertPositions,
        double pageWidth = 612, double pageHeight = 792, string password = "")
    {
        await _editService.InsertBlankPagesAsync(inputFilename, outputFilename, insertPositions, pageWidth, pageHeight, password);
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
        _editService.ReorderPdfPages(inputFilename, outputFilename, newPageOrder, password);
    }

    /// <summary>
    /// Reorders pages in a PDF document according to the specified page order asynchronously.
    /// </summary>
    public async Task ReorderPdfPagesAsync(string inputFilename, string outputFilename, List<int> newPageOrder, string password = "")
    {
        await _editService.ReorderPdfPagesAsync(inputFilename, outputFilename, newPageOrder, password);
    }

    #endregion

    #region PDF Rendering Operations

    /// <summary>
    /// Converts an image file to a PDF document.
    /// </summary>
    /// <param name="filename">The path of the image file to be converted.</param>
    /// <param name="outputFilename">The optional output path for the converted PDF document. If not provided, a default filename will be used.</param>
    /// <param name="progress">An optional progress object to report the conversion progress.</param>
    public void ConvertImageToPdf(string filename, string? outputFilename = null, IProgress<bool>? progress = null)
    {
        _renderService.ConvertImageToPdf(filename, outputFilename, progress);
    }

    /// <summary>
    /// Converts an image file to a PDF document asynchronously.
    /// </summary>
    public async Task ConvertImageToPdfAsync(string filename, string? outputFilename = null, IProgress<bool>? progress = null)
    {
        await _renderService.ConvertImageToPdfAsync(filename, outputFilename, progress);
    }

    /// <summary>
    /// Converts a PDF file to image(s) and saves the image(s) to the specified output location.
    /// </summary>
    /// <param name="inputFilename">The path to the input PDF file.</param>
    /// <param name="outputLocation">The path to the output location where the image(s) will be saved.</param>
    /// <param name="imageFormat">The image format to use for saving the image(s). Default is "png".</param>
    /// <param name="dpi">The DPI (dots per inch) for rendering the PDF. Default is 200.</param>
    /// <param name="pageRange">The range of pages to convert. Default is null (convert all pages).</param>
    /// <param name="password">The password required to open the PDF. Default is an empty string.</param>
    /// <param name="outputName">The name format of the output image(s). Use "{original}" to include the original PDF file name, and "{page}" to include the page number.</param>
    /// <param name="progress">The progress object used to report the conversion progress.</param>
    public void ConvertPdfToImage(string inputFilename, string outputLocation, string imageFormat = "png",
        int dpi = 200, List<int>? pageRange = null, string password = "", string? outputName = null,
        IProgress<PdfProgress>? progress = null)
    {
        _renderService.ConvertPdfToImage(inputFilename, outputLocation, imageFormat, dpi, pageRange, password, outputName, progress);
    }

    /// <summary>
    /// Converts a PDF file to image(s) and saves the image(s) to the specified output location asynchronously.
    /// </summary>
    public async Task ConvertPdfToImageAsync(string inputFilename, string outputLocation, string imageFormat = "png",
        int dpi = 200, List<int>? pageRange = null, string password = "", string? outputName = null,
        IProgress<PdfProgress>? progress = null)
    {
        await _renderService.ConvertPdfToImageAsync(inputFilename, outputLocation, imageFormat, dpi, pageRange, password, outputName, progress);
    }

    #endregion

    #region PDF Security Operations

    /// <summary>
    /// Unlocks a password-protected PDF by loading it with the password and saving it without security restrictions
    /// </summary>
    /// <param name="inputFilename">The path of the input password-protected PDF file</param>
    /// <param name="outputFilename">The path where the unlocked PDF will be saved</param>
    /// <param name="password">The password required to open the input PDF</param>
    public void UnlockPdf(string inputFilename, string outputFilename, string password)
    {
        _editService.UnlockPdfAsync(inputFilename, outputFilename, password).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Unlocks a password-protected PDF by loading it with the password and saving it without security restrictions asynchronously
    /// </summary>
    /// <param name="inputFilename">The path of the input password-protected PDF file</param>
    /// <param name="outputFilename">The path where the unlocked PDF will be saved</param>
    /// <param name="password">The password required to open the input PDF</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task UnlockPdfAsync(string inputFilename, string outputFilename, string password)
    {
        await _editService.UnlockPdfAsync(inputFilename, outputFilename, password);
    }

    #endregion
}
