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
    /// /
    public static void SplitPdf(string inputFilename, string outputLocation, string password = "",
        string? outputName = "{original}-{page}", List<int>? pageRange = null,
        bool useBookmarks = false, IProgress<PdfSplitProgress>? progress = null,
        Dictionary<int, string>? outputScriptNames = null)
    {
        lock (PdfiumLock)
        {
            if (!string.IsNullOrWhiteSpace(inputFilename) &&
                !string.IsNullOrWhiteSpace(outputLocation))
            {
                string filename = inputFilename;
                if (filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(filename))
                {
                    var documentT = fpdfview.FPDF_LoadDocument(inputFilename, password);

                    string namePart = IoUtils.RemoveInvalidChars(Path.GetFileNameWithoutExtension(filename));

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
                        //if range is provided, and page is not included, skip
                        if (pageRange != null && !pageRange.Contains(i+1)) continue;
                        
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
                            pageNamePart = outputScriptNames[i+1];
                        }


                        string pdfFilename = $"{pageNamePart}.pdf";


                        if (!Directory.Exists(outputLocation)) Directory.CreateDirectory(outputLocation);
                        string fullFilename = Path.Combine(outputLocation, pdfFilename);

                        progress?.Report(new PdfSplitProgress(i, numberOfPages, fullFilename));

                        var newDocumentT = fpdf_edit.FPDF_CreateNewDocument();
                        var inputPage = fpdfview.FPDF_LoadPage(documentT, i);


                        fpdf_ppo.FPDF_ImportPages(newDocumentT, documentT, $"{i + 1}", 0);

                        fpdfview.FPDF_ClosePage(inputPage);

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
                            throw new Exception("failed to create the document");
                        }

                        FPDF_CloseDocument(newDocumentT);
                    }


                    FPDF_CloseDocument(documentT);
                }
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
        bool deleteOriginal = false,
        IProgress<PdfProgress>? progress = null)
    {
        if (!string.IsNullOrWhiteSpace(outputFilename) && inputFilenames.Count > 0)
        {
            lock (PdfiumLock)
            {
                string filename = outputFilename;
                if (filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var newDocumentT = FPDF_CreateNewDocument();

                    int currentPageIndex = 0;
                    int idx = 0;
                    foreach (var inputFilename in inputFilenames)
                    {
                        progress?.Report(new(idx++, inputFilenames.Count));
                        if (!File.Exists(inputFilename)) continue;
                        var mDocumentT = FPDF_LoadDocument(inputFilename, password);

                        var numberOfPages = FPDF_GetPageCount(mDocumentT);

                        fpdf_ppo.FPDF_ImportPages(newDocumentT, mDocumentT, $"1-{numberOfPages}", currentPageIndex);
                        currentPageIndex += numberOfPages;
                        FPDF_CloseDocument(mDocumentT);

                        if (deleteOriginal) File.Delete(inputFilename);
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
                        throw new Exception("failed to create the document");
                    }


                    FPDF_CloseDocument(newDocumentT);
                }
            }
        }
    }


    /// <summary>
    /// Converts a JPEG image to a PDF document.
    /// </summary>
    /// <param name="filename">The path of the JPEG image file.</param>
    /// <param name="progress">An optional progress object to report the conversion progress.</param>
    private static void ConvertJpeg(string filename, IProgress<bool>? progress)
    {
        lock (PdfiumLock)
        {
            string pdfFilename = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty,
                Path.GetFileNameWithoutExtension(filename) + ".pdf");


            using var stream = File.OpenRead(filename);
            using var image = Image.Load<Bgra32>(stream);


            int width = image.Width;
            int height = image.Height;
            //int width = 2000;
            //int height = 3000;

            var newDocumentT = FPDF_CreateNewDocument();
            var pageT = FPDFPageNew(newDocumentT, 0, width, height);
            var pdfImageT = FPDFPageObjNewImageObj(newDocumentT);

            unsafe
            {
                FpdfBitmapT? bitmapT = null;

                int bytesPerPixel = (image.PixelType.BitsPerPixel / 8);
                int stride = width * bytesPerPixel;


                bitmapT = FPDFBitmapCreate(width, height, 0);

                IntPtr bufferPtr = FPDFBitmapGetBuffer(bitmapT);

                byte* buffer = (byte*)bufferPtr.ToPointer();

                image.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y);
                        for (int x = 0; x < pixelRow.Length; x++)
                        {
                            int offset = (y * stride) + (x * bytesPerPixel);
                            // Get a reference to the pixel at position x
                            ref var pixel = ref pixelRow[x];
                            buffer[offset] = pixel.B;
                            buffer[offset + 1] = pixel.G;
                            buffer[offset + 2] = pixel.R;
                            buffer[offset + 3] = pixel.A; //alpha channel unused
                        }
                    }
                });


                //get pointer to the image
                FPDFImageObjSetBitmap(pageT, 1, pdfImageT, bitmapT);
                FPDFImageObjSetMatrix(pdfImageT, width, 0, 0, height, 0, 0);
                FPDFPageInsertObject(pageT, pdfImageT);
                FPDFPageGenerateContent(pageT);


                FPDF_ClosePage(pageT);
                FPDFBitmapDestroy(bitmapT);
            }


            byte[]? pdfBuffer = null;
            using var filestream = System.IO.File.OpenWrite(pdfFilename);
            var fileWrite = new FPDF_FILEWRITE_();
            fileWrite.WriteBlock = (ignore, data, size) =>
            {
                if (pdfBuffer == null || pdfBuffer.Length < size)
                    pdfBuffer = new byte[size];

                Marshal.Copy(data, pdfBuffer, 0, (int)size);

                filestream?.Write(pdfBuffer, 0, (int)size);

                return 1;
            };


            _ = FPDF_SaveAsCopy(newDocumentT, fileWrite, 0);
            FPDF_CloseDocument(newDocumentT);

            Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            GC.Collect();
        }
    }

    /// <summary>
    /// Converts a TIFF file to a PDF file.
    /// </summary>
    /// <param name="filename">The path to the TIFF file to be converted.</param>
    /// <param name="progress">An optional <see cref="IProgress{T}"/> object to report progress of the conversion process.</param>
    private static void ConvertTiff(string filename, IProgress<bool>? progress = null)
    {
        T2P t2P = new T2P();
        using Tiff input = Tiff.Open(filename, "r");
        if (input == null)
        {
            Tiff.Error(Tiff2PdfConstants.TIFF2PDF_MODULE, $"Can't open input file {filename} for reading");
            return;
        }

        string pdfFilename = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty,
            Path.GetFileNameWithoutExtension(filename) + ".pdf");
        /*
         * Output
         */
        t2P.m_outputdisable = false;
        try
        {
            t2P.m_outputfile = File.Open(pdfFilename, FileMode.Create, FileAccess.Write);
        }
        catch (Exception e)
        {
            Tiff.Error(Tiff2PdfConstants.TIFF2PDF_MODULE, "Can't open output file {0} for writing. {1}", pdfFilename,
                e.Message);
            return;
        }

        using var output = Tiff.ClientOpen(pdfFilename, "w", t2P, t2P.m_stream);
        if (output == null)
        {
            t2P.m_outputfile.Dispose();
            Tiff.Error(input, Tiff2PdfConstants.TIFF2PDF_MODULE, "Can't initialize output descriptor");
            return;
        }

        /*
         * Validate
         */
        t2P.validate();

        object client = output.Clientdata();
        TiffStream stream = output.GetStream();
        stream.Seek(client, 0, SeekOrigin.Begin);

        /*
         * Write
         */
        t2P.write_pdf(input, output);
        if (t2P.m_error)
        {
            t2P.m_outputfile.Dispose();
            Tiff.Error(input, Tiff2PdfConstants.TIFF2PDF_MODULE, "An error occurred creating output PDF file");
            return;
        }

        t2P.m_outputfile.Dispose();
    }

    /// <summary>
    /// Converts an image file to a PDF document.
    /// </summary>
    /// <param name="filename">The path of the image file to be converted.</param>
    /// <param name="outputFilename">The optional output path for the converted PDF document. If not provided, a default filename will be used.</param>
    /// <param name="progress">An optional progress object to report the conversion progress.</param>
    public static void ConvertImageToPdf(string filename, string? outputFilename = null, IProgress<bool>? progress = null)
    {
        lock (PdfiumLock)
        {
            
            string pdfFilename = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty,
                Path.GetFileNameWithoutExtension(filename) + ".pdf");
            if (!string.IsNullOrWhiteSpace(outputFilename))
            {
                pdfFilename = outputFilename;
            }

            string? parentDir = Path.GetDirectoryName(pdfFilename);
            if (parentDir != null && !File.Exists(parentDir)) Directory.CreateDirectory(parentDir);
            
            using var image = Image.Load(filename);
            int width = image.Width;
            int height = image.Height;

            var newDocumentT = FPDF_CreateNewDocument();
            var pageT = FPDFPageNew(newDocumentT, 0, width, height);
            var pdfImageT = FPDFPageObjNewImageObj(newDocumentT);
            int pixelFormat = 2; //#define FPDFBitmap_BGR 2
            unsafe
            {
                Configuration customConfig = Configuration.Default.Clone();
                customConfig.PreferContiguousImageBuffers = true;

                FpdfBitmapT? bitmapT = null;
                if (filename.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase)) //if color image
                {
                    pixelFormat = 1; //#define FPDFBitmap_Gray 1
                    Image<L8> pixelData = image.CloneAs<L8>(customConfig);
                    //image.Dispose();
                    int bytesPerPixel = pixelData.PixelType.BitsPerPixel / 8;
                    int stride = pixelData.Width * bytesPerPixel;


                    if (!pixelData.DangerousTryGetSinglePixelMemory(out Memory<L8> memory))
                    {
                        throw new Exception(
                            "This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");
                    }

                    using var pinHandle = memory.Pin();
                    void* ptr = pinHandle.Pointer;
                    bitmapT = FPDFBitmapCreateEx(pixelData.Width, pixelData.Height, pixelFormat,
                        (IntPtr)ptr, stride);


                    //get pointer to the image
                    FPDFImageObjSetBitmap(pageT, 1, pdfImageT, bitmapT);
                    FPDFImageObjSetMatrix(pdfImageT, image.Width, 0, 0, image.Height, 0, 0);
                    FPDFPageInsertObject(pageT, pdfImageT);
                    FPDFPageGenerateContent(pageT);


                    FPDF_ClosePage(pageT);
                    FPDFBitmapDestroy(bitmapT);
                    //pinHandle.Dispose();

                    pixelData.Dispose();
                }
                else
                {
                    Image<Bgr24> pixelData = image.CloneAs<Bgr24>(customConfig);
                    //image.Dispose();
                    int bytesPerPixel = pixelData.PixelType.BitsPerPixel / 8;
                    int stride = pixelData.Width * bytesPerPixel;

                    if (!pixelData.DangerousTryGetSinglePixelMemory(out Memory<Bgr24> memory))
                        throw new Exception(
                            "This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");

                    using var pinHandle = memory.Pin();
                    void* ptr = pinHandle.Pointer;
                    bitmapT = FPDFBitmapCreateEx(pixelData.Width, pixelData.Height, pixelFormat,
                        (IntPtr)ptr, stride);

                    //get pointer to the image
                    FPDFImageObjSetBitmap(pageT, 1, pdfImageT, bitmapT);
                    FPDFImageObjSetMatrix(pdfImageT, image.Width, 0, 0, image.Height, 0, 0);
                    FPDFPageInsertObject(pageT, pdfImageT);
                    FPDFPageGenerateContent(pageT);

                    FPDF_ClosePage(pageT);
                    FPDFBitmapDestroy(bitmapT);
                    pixelData.Dispose();
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


            _ = FPDF_SaveAsCopy(newDocumentT, fileWrite, 0);
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
        lock (PdfiumLock)
        {
            if (!string.IsNullOrWhiteSpace(inputFilename) &&
                !string.IsNullOrWhiteSpace(outputLocation))
            {
                string filename = inputFilename;
                if (filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(filename))
                {
                    var documentT = fpdfview.FPDF_LoadDocument(inputFilename, password);

                    string namePart = IoUtils.RemoveInvalidChars(Path.GetFileNameWithoutExtension(filename));
                    if (!Directory.Exists(outputLocation)) Directory.CreateDirectory(outputLocation);

                    //get the render size calculated
                    // Calculate the scale based on desired DPI
                    float scale = dpi / 72.0f; // PDFium default DPI is 72

                    var numberOfPages = fpdfview.FPDF_GetPageCount(documentT);
                    for (int i = 0; i < numberOfPages; i++)
                    {
                        //if range is provided, and page is not included, skip
                        if (pageRange != null && !pageRange.Contains(i+1)) continue;
                        
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
                        
                        

                        //extension is missing here
                        
                        //string pageNamePart = $"{namePart}-{i + 1:D3}";
                        string fullOutputFilename = Path.Combine(outputLocation, pageNamePart);
                        //background color
                        uint color = uint.MaxValue;

                        

                        var page = fpdfview.FPDF_LoadPage(documentT, i);


                        // Get the page size and calculate the width and height at the specified DPI
                        double width = 0, height = 0;
                        fpdfview.FPDF_GetPageSizeByIndex(documentT, i, ref width, ref height);

                        int scaledWidth = (int)(width * scale);
                        int scaledHeight = (int)(height * scale);


                        var bitmap = fpdfview.FPDFBitmapCreateEx(scaledWidth, scaledHeight,
                            (int)FPDFBitmapFormat.BGRA, IntPtr.Zero, 0);

                        if (bitmap == null) throw new Exception("failed to create a bitmap object");

                        fpdfview.FPDFBitmapFillRect(bitmap, 0, 0, scaledWidth, scaledHeight, color);
                        var bitmapFormat = fpdfview.FPDFBitmapGetFormat(bitmap);

                        fpdfview.FPDF_RenderPageBitmap(bitmap, page, 0, 0, scaledWidth, scaledHeight, 0,
                            (int)RenderFlags.RenderAnnotations);
                        var scan0 = fpdfview.FPDFBitmapGetBuffer(bitmap);
                        var stride = fpdfview.FPDFBitmapGetStride(bitmap);

                        unsafe
                        {
                            var memoryMgr = new UnmanagedMemoryManager<byte>((byte*)scan0, (int)(stride * scaledHeight));
                            var imgData =
                                Image.WrapMemory<Bgra32>(Configuration.Default, memoryMgr.Memory, scaledWidth,
                                    scaledHeight);

                            imgData.SaveAsync(fullOutputFilename, encoder);
                            imgData.Dispose();
                        }

                        fpdfview.FPDFBitmapDestroy(bitmap);
                        Marshal.FreeHGlobal(scan0);

                        fpdfview.FPDF_ClosePage(page);


                        //Dispose bitmap
                    }


                    FPDF_CloseDocument(documentT);
                }
            }
        }
    }
    
}