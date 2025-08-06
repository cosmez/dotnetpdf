using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PDFiumCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdfview;
using static PDFiumCore.fpdf_save;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

/// <summary>
/// Service for PDF rendering operations like converting between PDF and images
/// </summary>
public class PdfRenderService : BasePdfService
{
    /// <summary>
    /// Initializes a new instance of the PdfRenderService
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory for logging operations</param>
    public PdfRenderService(ILoggerFactory? loggerFactory = null) 
        : base((loggerFactory?.CreateLogger<PdfRenderService>()) ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PdfRenderService>.Instance, loggerFactory)
    {
    }

    /// <summary>
    /// Converts an image file to a PDF document asynchronously.
    /// </summary>
    /// <param name="filename">The path of the image file to be converted.</param>
    /// <param name="outputFilename">The optional output path for the converted PDF document. If not provided, a default filename will be used.</param>
    /// <param name="progress">An optional progress object to report the conversion progress.</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task ConvertImageToPdfAsync(string filename, string? outputFilename = null, IProgress<bool>? progress = null)
    {
        await Task.Run(() => ConvertImageToPdf(filename, outputFilename, progress));
    }

    /// <summary>
    /// Converts an image file to a PDF document.
    /// </summary>
    /// <param name="filename">The path of the image file to be converted.</param>
    /// <param name="outputFilename">The optional output path for the converted PDF document. If not provided, a default filename will be used.</param>
    /// <param name="progress">An optional progress object to report the conversion progress.</param>
    public void ConvertImageToPdf(string filename, string? outputFilename = null, IProgress<bool>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(filename))
            throw new ArgumentException("Filename cannot be null or empty", nameof(filename));

        if (!File.Exists(filename))
            throw new FileNotFoundException($"Input file not found: {filename}");

        lock (PdfiumLock)
        {
            try
            {
                InitLibrary();
                Logger.LogInformation("Converting image {Filename} to PDF", filename);

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
                    
                    progress?.Report(false);
                    
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

                    progress?.Report(true);
                    Logger.LogInformation("Successfully converted image to PDF: {OutputFile}", pdfFilename);
                }
                finally
                {
                    FPDF_CloseDocument(newDocumentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error converting image to PDF: {Message}", ex.Message);
                throw new InvalidOperationException($"Error converting image to PDF: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Converts a PDF file to image(s) and saves the image(s) to the specified output location asynchronously.
    /// </summary>
    /// <param name="inputFilename">The path to the input PDF file.</param>
    /// <param name="outputLocation">The path to the output location where the image(s) will be saved.</param>
    /// <param name="imageFormat">The image format to use for saving the image(s). Default is "png".</param>
    /// <param name="dpi">The DPI (dots per inch) for rendering the PDF. Default is 200.</param>
    /// <param name="pageRange">The range of pages to convert. Default is null (convert all pages).</param>
    /// <param name="password">The password required to open the PDF. Default is an empty string.</param>
    /// <param name="outputName">The name format of the output image(s). Use "{original}" to include the original PDF file name,
    /// and "{page}" to include the page number. If null, a default name format will be used. Default is null.</param>
    /// <param name="progress">The progress object used to report the conversion progress. Default is null (no progress tracking).</param>
    /// <returns>Task representing the asynchronous operation</returns>
    public async Task ConvertPdfToImageAsync(string inputFilename, string outputLocation, string imageFormat = "png",
        int dpi = 200, List<int>? pageRange = null, string password = "", string? outputName = null,
        IProgress<PdfProgress>? progress = null)
    {
        await Task.Run(() => ConvertPdfToImage(inputFilename, outputLocation, imageFormat, dpi, pageRange, password, outputName, progress));
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
    /// <param name="outputName">The name format of the output image(s). Use "{original}" to include the original PDF file name,
    /// and "{page}" to include the page number. If null, a default name format will be used. Default is null.</param>
    /// <param name="progress">The progress object used to report the conversion progress. Default is null (no progress tracking).</param>
    public void ConvertPdfToImage(string inputFilename, string outputLocation, string imageFormat = "png",
        int dpi = 200, List<int>? pageRange = null, string password = "", string? outputName = null,
        IProgress<PdfProgress>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
            throw new ArgumentException("Input filename cannot be null or empty", nameof(inputFilename));
        
        if (string.IsNullOrWhiteSpace(outputLocation))
            throw new ArgumentException("Output location cannot be null or empty", nameof(outputLocation));
        
        var encoder = GetEncoder(imageFormat);
        if (encoder == null)
            throw new ArgumentNullException(nameof(encoder));

        if (dpi <= 0 || dpi > 2400)
            throw new ArgumentOutOfRangeException(nameof(dpi), "DPI must be between 1 and 2400");

        if (!IsValidPdfFile(inputFilename))
            return;

        lock (PdfiumLock)
        {
            try
            {
                InitLibrary();
                var documentT = FPDF_LoadDocument(inputFilename, password);
                if (documentT == null)
                {
                    Logger.LogError("Failed to load PDF document: {Filename}", inputFilename);
                    throw new InvalidOperationException("Failed to load PDF document. Check if the file is valid and password is correct.");
                }

                try
                {
                    string namePart = RemoveInvalidChars(Path.GetFileNameWithoutExtension(inputFilename));
                    if (!Directory.Exists(outputLocation)) 
                        Directory.CreateDirectory(outputLocation);

                    // Calculate the scale based on desired DPI
                    float scale = dpi / 72.0f; // PDFium default DPI is 72

                    var numberOfPages = FPDF_GetPageCount(documentT);
                    Logger.LogInformation("Converting {PageCount} pages from PDF to images at {Dpi} DPI", numberOfPages, dpi);
                    
                    for (int i = 0; i < numberOfPages; i++)
                    {
                        if (pageRange != null && !pageRange.Contains(i + 1)) continue;
                        
                        string extension = GetExtension(encoder);
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
                        
                        progress?.Report(new PdfProgress(i + 1, numberOfPages));

                        string fullOutputFilename = Path.Combine(outputLocation, pageNamePart);
                        uint color = uint.MaxValue; // background color

                        var page = FPDF_LoadPage(documentT, i);
                        if (page == null)
                        {
                            Logger.LogWarning("Failed to load page {PageNumber}", i + 1);
                            continue;
                        }

                        try
                        {
                            // Get the page size and calculate the width and height at the specified DPI
                            double width = 0, height = 0;
                            FPDF_GetPageSizeByIndex(documentT, i, ref width, ref height);

                            int scaledWidth = (int)(width * scale);
                            int scaledHeight = (int)(height * scale);

                            var bitmap = FPDFBitmapCreateEx(scaledWidth, scaledHeight,
                                4, IntPtr.Zero, 0); // 4 = BGRA format

                            if (bitmap == null) 
                                throw new InvalidOperationException($"Failed to create bitmap for page {i + 1}");

                            try
                            {
                                FPDFBitmapFillRect(bitmap, 0, 0, scaledWidth, scaledHeight, color);
                                FPDF_RenderPageBitmap(bitmap, page, 0, 0, scaledWidth, scaledHeight, 0,
                                    0x01); // FPDF_ANNOT flag for rendering annotations
                                
                                var scan0 = FPDFBitmapGetBuffer(bitmap);
                                var stride = FPDFBitmapGetStride(bitmap);

                                unsafe
                                {
                                    var memoryMgr = new UnmanagedMemoryManager<byte>((byte*)scan0, (int)(stride * scaledHeight));
                                    using var imgData = Image.WrapMemory<Bgra32>(Configuration.Default, memoryMgr.Memory, scaledWidth, scaledHeight);
                                    imgData.Save(fullOutputFilename, encoder);
                                }
                            }
                            finally
                            {
                                FPDFBitmapDestroy(bitmap);
                            }
                        }
                        finally
                        {
                            FPDF_ClosePage(page);
                        }
                    }

                    progress?.Report(new PdfProgress(numberOfPages, numberOfPages));
                    Logger.LogInformation("Successfully converted PDF to images in {OutputLocation}", outputLocation);
                }
                finally
                {
                    FPDF_CloseDocument(documentT);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error converting PDF to images: {Message}", ex.Message);
                throw new InvalidOperationException($"Error converting PDF to images: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Gets an image encoder for the specified format
    /// </summary>
    /// <param name="imageFormat">The image format (e.g., "png", ".png", "jpg", ".jpg")</param>
    /// <returns>An instance of an image encoder</returns>
    /// <exception cref="NotSupportedException">Thrown if the format is not supported</exception>
    private static IImageEncoder GetEncoder(string imageFormat)
    {
        // Remove leading dot if present and convert to lowercase
        var format = imageFormat.TrimStart('.').ToLowerInvariant();
        
        return format switch
        {
            "png" => new PngEncoder(),
            "jpg" or "jpeg" => new JpegEncoder(),
            "gif" => new GifEncoder(),
            "bmp" => new BmpEncoder(),
            "tiff" or "tif" => new TiffEncoder(),
            "webp" => new WebpEncoder(),
            _ => throw new NotSupportedException($"Image format '{imageFormat}' is not supported")
        };
    }

    /// <summary>
    /// Gets the file extension for the given image encoder
    /// </summary>
    /// <param name="encoder">The image encoder</param>
    /// <returns>File extension with dot prefix</returns>
    private static string GetExtension(IImageEncoder encoder) => encoder switch
    {
        SixLabors.ImageSharp.Formats.Png.PngEncoder => ".png",
        SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder => ".jpg",
        SixLabors.ImageSharp.Formats.Gif.GifEncoder => ".gif",
        SixLabors.ImageSharp.Formats.Bmp.BmpEncoder => ".bmp",
        SixLabors.ImageSharp.Formats.Tiff.TiffEncoder => ".tiff",
        SixLabors.ImageSharp.Formats.Webp.WebpEncoder => ".webp",
        _ => ".png" // Default fallback
    };

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
