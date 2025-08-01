using System.Runtime.InteropServices;
using BitMiracle.LibTiff.Classic;
using dotnet.pdf.LibTiff;
using PDFiumCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdfview;
using static PDFiumCore.fpdf_save;
using static PDFiumCore.fpdf_ppo;

namespace dotnet.pdf;

public partial class Pdfium
{
    private static readonly Object PdfiumLock = new Object();

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
    /// <param name="outputScriptNames"></param>
    /// <exception cref="System.Exception">Thrown if failed to create the document.</exception>
    public static void SplitPdf(string inputFilename, string outputLocation, string password = "",
        string? outputName = "{original}-{page}", List<int>? pageRange = null,
        bool useBookmarks = false, IProgress<PdfSplitProgress>? progress = null,
        Dictionary<int, string>? outputScriptNames = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputLocation))
            throw new ArgumentException("Output location cannot be null or empty", nameof(outputLocation));

        lock (PdfiumLock)
        {
            try
            {
                if (!File.Exists(inputFilename))
                    throw new FileNotFoundException($"Input file not found: {inputFilename}");

                if (!inputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Input file must be a PDF file", nameof(inputFilename));

                var documentT = fpdfview.FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");

                try
                {
                    string namePart = IoUtils.RemoveInvalidChars(Path.GetFileNameWithoutExtension(inputFilename));

                    Dictionary<int, string> pageNames = new();
                    if (useBookmarks)
                    {
                        var bookmarks = GetBookmarks(documentT);
                        foreach (var bookmark in bookmarks)
                        {
                            pageNames[bookmark.Page] = bookmark.Title;
                        }
                    }

                    var numberOfPages = fpdfview.FPDF_GetPageCount(documentT);
                    
                    for (int i = 0; i < numberOfPages; i++)
                    {
                        if (pageRange != null && !pageRange.Contains(i + 1)) continue;
                        
                        string pageNamePart;
                        if (pageNames.TryGetValue(i, out var name))
                        {
                            string bookMarkName = IoUtils.RemoveInvalidChars(name);
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

                        progress?.Report(new PdfSplitProgress(i, numberOfPages, fullFilename));

                        var newDocumentT = fpdf_edit.FPDF_CreateNewDocument();
                        if (newDocumentT == null)
                            throw new InvalidOperationException("Failed to create new PDF document");

                        try
                        {
                            var inputPage = fpdfview.FPDF_LoadPage(documentT, i);
                            if (inputPage == null)
                                throw new InvalidOperationException($"Failed to load page {i + 1}");

                            try
                            {
                                fpdf_ppo.FPDF_ImportPages(newDocumentT, documentT, $"{i + 1}", 0);

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

                                var success = fpdf_save.FPDF_SaveAsCopy(newDocumentT, fileWrite, 0);
                                if (success == 0)
                                {
                                    throw new InvalidOperationException("Failed to save PDF document");
                                }
                            }
                            finally
                            {
                                fpdfview.FPDF_ClosePage(inputPage);
                            }
                        }
                        finally
                        {
                            FPDF_CloseDocument(newDocumentT);
                        }
                    }
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
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
    public static void MergeFiles(string outputFilename, List<string> inputFilenames, string password = "",
        bool deleteOriginal = false, IProgress<PdfProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));
        
        if (inputFilenames == null || inputFilenames.Count == 0)
            throw new ArgumentException("Input filenames list cannot be null or empty", nameof(inputFilenames));

        lock (PdfiumLock)
        {
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
                    
                    foreach (var inputFilename in inputFilenames)
                    {
                        progress?.Report(new(idx++, inputFilenames.Count));
                        
                        if (!File.Exists(inputFilename)) 
                        {
                            Console.WriteLine($"Warning: File not found, skipping: {inputFilename}");
                            continue;
                        }

                        var mDocumentT = FPDF_LoadDocument(inputFilename, password);
                        if (mDocumentT == null)
                        {
                            Console.WriteLine($"Warning: Failed to load PDF, skipping: {inputFilename}");
                            continue;
                        }

                        try
                        {
                            var numberOfPages = FPDF_GetPageCount(mDocumentT);
                            fpdf_ppo.FPDF_ImportPages(newDocumentT, mDocumentT, $"1-{numberOfPages}", currentPageIndex);
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
                }
                finally
                {
                    FPDF_CloseDocument(newDocumentT);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error merging PDF files: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Converts an image file to a PDF document.
    /// </summary>
    /// <param name="filename">The path of the image file to be converted.</param>
    /// <param name="outputFilename">The optional output path for the converted PDF document. If not provided, a default filename will be used.</param>
    /// <param name="progress">An optional progress object to report the conversion progress.</param>
    public static void ConvertImageToPdf(string filename, string? outputFilename = null, IProgress<bool>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be null or empty", nameof(filename));

        if (!File.Exists(filename))
            throw new FileNotFoundException($"Input file not found: {filename}");

        lock (PdfiumLock)
        {
            try
            {
                string pdfFilename = outputFilename ?? Path.Combine(
                    Path.GetDirectoryName(filename) ?? string.Empty,
                    Path.GetFileNameWithoutExtension(filename) + ".pdf");

                string? parentDir = Path.GetDirectoryName(pdfFilename);
                if (parentDir != null && !Directory.Exists(parentDir)) 
                    Directory.CreateDirectory(parentDir);
                
                using var image = Image.Load(filename);
                int width = image.Width;
                int height = image.Height;

                var newDocumentT = FPDF_CreateNewDocument();
                if (newDocumentT == null)
                    throw new InvalidOperationException("Failed to create new PDF document");

                try
                {
                    var pageT = FPDFPageNew(newDocumentT, 0, width, height);
                    var pdfImageT = FPDFPageObjNewImageObj(newDocumentT);
                    int pixelFormat = 2; //#define FPDFBitmap_BGR 2
                    
                    unsafe
                    {
                        Configuration customConfig = Configuration.Default.Clone();
                        customConfig.PreferContiguousImageBuffers = true;

                        FpdfBitmapT? bitmapT;
                        if (filename.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                            filename.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                        {
                            pixelFormat = 1; //#define FPDFBitmap_Gray 1
                            using var pixelData = image.CloneAs<L8>(customConfig);
                            int bytesPerPixel = pixelData.PixelType.BitsPerPixel / 8;
                            int stride = pixelData.Width * bytesPerPixel;

                            if (!pixelData.DangerousTryGetSinglePixelMemory(out Memory<L8> memory))
                            {
                                throw new InvalidOperationException(
                                    "This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");
                            }

                            using var pinHandle = memory.Pin();
                            void* ptr = pinHandle.Pointer;
                            bitmapT = FPDFBitmapCreateEx(pixelData.Width, pixelData.Height, pixelFormat,
                                (IntPtr)ptr, stride);

                            FPDFImageObjSetBitmap(pageT, 1, pdfImageT, bitmapT);
                            FPDFImageObjSetMatrix(pdfImageT, image.Width, 0, 0, image.Height, 0, 0);
                            FPDFPageInsertObject(pageT, pdfImageT);
                            FPDFPageGenerateContent(pageT);

                            FPDF_ClosePage(pageT);
                            FPDFBitmapDestroy(bitmapT);
                        }
                        else
                        {
                            using var pixelData = image.CloneAs<Bgr24>(customConfig);
                            int bytesPerPixel = pixelData.PixelType.BitsPerPixel / 8;
                            int stride = pixelData.Width * bytesPerPixel;

                            if (!pixelData.DangerousTryGetSinglePixelMemory(out Memory<Bgr24> memory))
                                throw new InvalidOperationException(
                                    "This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");

                            using var pinHandle = memory.Pin();
                            void* ptr = pinHandle.Pointer;
                            bitmapT = FPDFBitmapCreateEx(pixelData.Width, pixelData.Height, pixelFormat,
                                (IntPtr)ptr, stride);

                            FPDFImageObjSetBitmap(pageT, 1, pdfImageT, bitmapT);
                            FPDFImageObjSetMatrix(pdfImageT, image.Width, 0, 0, image.Height, 0, 0);
                            FPDFPageInsertObject(pageT, pdfImageT);
                            FPDFPageGenerateContent(pageT);

                            FPDF_ClosePage(pageT);
                            FPDFBitmapDestroy(bitmapT);
                        }
                    }

                    GC.Collect();

                    byte[]? pdfBuffer = null;
                    using var filestream = File.OpenWrite(pdfFilename);
                    var fileWrite = new FPDF_FILEWRITE_();
                    fileWrite.WriteBlock = (ignore, data, size) =>
                    {
                        if (pdfBuffer == null || pdfBuffer.Length < size)
                            pdfBuffer = new byte[size];

                        Marshal.Copy(data, pdfBuffer, 0, (int)size);
                        filestream?.Write(pdfBuffer, 0, (int)size);
                        return 1;
                    };

                    var success = FPDF_SaveAsCopy(newDocumentT, fileWrite, 0);
                    if (success == 0)
                    {
                        throw new InvalidOperationException("Failed to save PDF document");
                    }
                }
                finally
                {
                    FPDF_CloseDocument(newDocumentT);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error converting image to PDF: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Converts a PDF file to image(s) and saves the image(s) to the specified output location.
    /// </summary>
    /// <param name="inputFilename">The path to the input PDF file.</param>
    /// <param name="outputLocation">The path to the output location where the image(s) will be saved.</param>
    /// <param name="encoder">The image encoder to use for saving the image(s).</param>
    /// <param name="dpi">The DPI (dots per inch) for rendering the PDF. Default is 200.</param>
    /// <param name="pageRange">The range of pages to convert. Default is null (convert all pages).</param>
    /// <param name="password">The password required to open the PDF. Default is an empty string.</param>
    /// <param name="outputName">The name format of the output image(s). Use "{original}" to include the original PDF file name,
    /// and "{page}" to include the page number. If null, a default name format will be used. Default is null.</param>
    /// <param name="progress">The progress object used to report the conversion progress. Default is null (no progress tracking).</param>
    public static void ConvertPdfToImage(string inputFilename, string outputLocation, IImageEncoder encoder,
        int dpi = 200, List<int>? pageRange = null, string password = "", string? outputName = null,
        IProgress<PdfProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputLocation))
            throw new ArgumentException("Output location cannot be null or empty", nameof(outputLocation));
        
        if (encoder == null)
            throw new ArgumentNullException(nameof(encoder));

        if (dpi <= 0 || dpi > 2400)
            throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be between 1 and 2400");

        lock (PdfiumLock)
        {
            try
            {
                if (!File.Exists(inputFilename))
                    throw new FileNotFoundException($"Input file not found: {inputFilename}");

                if (!inputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Input file must be a PDF file", nameof(inputFilename));

                var documentT = fpdfview.FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");

                try
                {
                    string namePart = IoUtils.RemoveInvalidChars(Path.GetFileNameWithoutExtension(inputFilename));
                    if (!Directory.Exists(outputLocation)) 
                        Directory.CreateDirectory(outputLocation);

                    // Calculate the scale based on desired DPI
                    float scale = dpi / 72.0f; // PDFium default DPI is 72

                    var numberOfPages = fpdfview.FPDF_GetPageCount(documentT);
                    for (int i = 0; i < numberOfPages; i++)
                    {
                        if (pageRange != null && !pageRange.Contains(i + 1)) continue;
                        
                        string extension = IoUtils.GetExtension(encoder);
                        string pageNamePart = $"{namePart}-{i + 1:D3}{extension}";
                        if (!string.IsNullOrEmpty(outputName))
                        {
                            pageNamePart = outputName.Replace("{original}", namePart)
                                .Replace("{page}", $"{i + 1:D3}");
                            if (!pageNamePart.Contains('.'))
                            {
                                pageNamePart = pageNamePart + extension;    
                            }
                        }
                        
                        progress?.Report(new PdfProgress(i, numberOfPages));

                        string fullOutputFilename = Path.Combine(outputLocation, pageNamePart);
                        uint color = uint.MaxValue; // background color

                        var page = fpdfview.FPDF_LoadPage(documentT, i);
                        if (page == null)
                            throw new InvalidOperationException($"Failed to load page {i + 1}");

                        try
                        {
                            // Get the page size and calculate the width and height at the specified DPI
                            double width = 0, height = 0;
                            fpdfview.FPDF_GetPageSizeByIndex(documentT, i, ref width, ref height);

                            int scaledWidth = (int)(width * scale);
                            int scaledHeight = (int)(height * scale);

                            var bitmap = fpdfview.FPDFBitmapCreateEx(scaledWidth, scaledHeight,
                                (int)FPDFBitmapFormat.BGRA, IntPtr.Zero, 0);

                            if (bitmap == null) 
                                throw new InvalidOperationException("Failed to create bitmap");

                            try
                            {
                                fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, scaledWidth, scaledHeight, color);
                                fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, scaledWidth, scaledHeight, 0,
                                    (int)RenderFlags.RenderAnnotations);
                                
                                var scan0 = fpdfview.FPDFBitmapGetBuffer(bitmap);
                                var stride = fpdfview.FPDFBitmapGetStride(bitmap);

                                unsafe
                                {
                                    var memoryMgr = new UnmanagedMemoryManager<byte>((byte*)scan0, (int)(stride * scaledHeight));
                                    using var imgData = Image.WrapMemory<Bgra32>(Configuration.Default, memoryMgr.Memory, scaledWidth, scaledHeight);
                                    imgData.Save(fullOutputFilename, encoder);
                                }
                            }
                            finally
                            {
                                fpdfview.FPDFBitmapDestroy(bitmap);
                            }
                        }
                        finally
                        {
                            fpdfview.FPDF_ClosePage(page);
                        }
                    }
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error converting PDF to images: {ex.Message}", ex);
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
    public static void RotatePdfPages(string inputFilename, string outputFilename, int rotation, 
        List<int>? pageRange = null, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (rotation != 90 && rotation != 180 && rotation != 270)
            throw new ArgumentException("Rotation must be 90, 180, or 270 degrees", nameof(rotation));

        lock (PdfiumLock)
        {
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");

                try
                {
                    var numberOfPages = FPDF_GetPageCount(documentT);
                    
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
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
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
    public static void RemovePdfPages(string inputFilename, string outputFilename, List<int> pagesToRemove, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (pagesToRemove == null || !pagesToRemove.Any())
            throw new ArgumentException("Pages to remove cannot be null or empty", nameof(pagesToRemove));

        lock (PdfiumLock)
        {
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");

                try
                {
                    var numberOfPages = FPDF_GetPageCount(documentT);
                    
                    // Sort pages in descending order to remove from end to beginning
                    var sortedPages = pagesToRemove.Where(p => p >= 1 && p <= numberOfPages)
                                                  .OrderByDescending(p => p)
                                                  .ToList();

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
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
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
    public static void InsertBlankPages(string inputFilename, string outputFilename, Dictionary<int, int> insertPositions,
        double pageWidth = 612, double pageHeight = 792, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (insertPositions == null || !insertPositions.Any())
            throw new ArgumentException("Insert positions cannot be null or empty", nameof(insertPositions));

        lock (PdfiumLock)
        {
            try
            {
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");

                try
                {
                    // Sort positions in descending order to insert from end to beginning
                    var sortedPositions = insertPositions.OrderByDescending(kvp => kvp.Key).ToList();

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
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
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
    public static void ReorderPdfPages(string inputFilename, string outputFilename, List<int> newPageOrder, string password = "")
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputFilename))
            throw new ArgumentException("Output filename cannot be null or empty", nameof(outputFilename));

        if (newPageOrder == null || !newPageOrder.Any())
            throw new ArgumentException("New page order cannot be null or empty", nameof(newPageOrder));

        lock (PdfiumLock)
        {
            try
            {
                var sourceDocT = FPDF_LoadDocument(inputFilename, password);
                if (sourceDocT == null)
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");

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
                throw new InvalidOperationException($"Error reordering PDF pages: {ex.Message}", ex);
            }
        }
    }
}