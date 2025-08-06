using System.Runtime.InteropServices;
using PDFiumCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdf_save;
using static PDFiumCore.fpdfview;
using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public unsafe class PdfWatermarkService : BasePdfService
{
    public PdfWatermarkService(ILogger<PdfWatermarkService> logger) : base(logger, null)
    {
    }

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
        if (!IsValidPdfFile(inputFilename))
            return;

        if (string.IsNullOrWhiteSpace(outputFilename))
        {
            Logger.LogError("Output filename cannot be null or empty");
            return;
        }

        if (options == null)
        {
            Logger.LogError("Watermark options cannot be null");
            return;
        }

        if (string.IsNullOrEmpty(options.Text) && string.IsNullOrEmpty(options.ImagePath))
        {
            Logger.LogError("Either text or image path must be specified for watermark");
            return;
        }

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                Logger.LogError("Failed to load PDF document: {Filename}", inputFilename);
                return;
            }

            try
            {
                int pageCount = FPDF_GetPageCount(documentT);
                Logger.LogInformation("Adding watermark to {PageCount} pages", pageCount);

                for (int i = 0; i < pageCount; i++)
                {
                    progress?.Report(new PdfProgress(i + 1, pageCount));
                    
                    var pageT = FPDF_LoadPage(documentT, i);
                    if (pageT == null)
                    {
                        Logger.LogWarning("Failed to load page {PageNumber}", i + 1);
                        continue;
                    }

                    try
                    {
                        if (!string.IsNullOrEmpty(options.Text))
                        {
                            AddTextWatermarkToPage(documentT, pageT, options);
                            Logger.LogDebug("Added text watermark to page {PageNumber}", i + 1);
                        }
                        else if (!string.IsNullOrEmpty(options.ImagePath))
                        {
                            AddImageWatermarkToPage(documentT, pageT, options);
                            Logger.LogDebug("Added image watermark to page {PageNumber}", i + 1);
                        }

                        FPDFPageGenerateContent(pageT);
                    }
                    finally
                    {
                        FPDF_ClosePage(pageT);
                    }
                }

                // Save the watermarked document
                SaveDocument(documentT, outputFilename);
                Logger.LogInformation("Successfully saved watermarked PDF to {OutputFilename}", outputFilename);
                progress?.Report(new PdfProgress(pageCount, pageCount));
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    /// <summary>
    /// Adds a text watermark to a specific page
    /// </summary>
    /// <param name="doc">PDFium document handle</param>
    /// <param name="page">PDFium page handle</param>
    /// <param name="options">Watermark options</param>
    private void AddTextWatermarkToPage(FpdfDocumentT doc, FpdfPageT page, WatermarkOptions options)
    {
        var font = FPDFTextLoadStandardFont(doc, options.Font);
        if (font == null)
        {
            Logger.LogError("Failed to load font: {Font}", options.Font);
            throw new InvalidOperationException($"Failed to load font: {options.Font}");
        }

        var textObject = FPDFPageObjNewTextObj(doc, options.Font, (float)options.FontSize);
        if (textObject == null)
        {
            Logger.LogError("Failed to create text object");
            throw new InvalidOperationException("Failed to create text object");
        }

        try
        {
            fixed (char* pText = options.Text)
            {
                if (FPDFTextSetText(textObject, ref *(ushort*)pText) == 0)
                {
                    throw new InvalidOperationException("Failed to set text for watermark object.");
                }
            }

            // Get the untransformed bounding box of the text object to find its width and height
            float left = 0, bottom = 0, right = 0, top = 0;
            if (FPDFPageObjGetBounds(textObject, ref left, ref bottom, ref right, ref top) == 0)
            {
                FPDFPageObjDestroy(textObject);
                throw new InvalidOperationException("Could not get text object bounds.");
            }
            float textWidth = right - left;
            float textHeight = top - bottom;

            // Set color and opacity
            FPDFPageObjSetFillColor(textObject, options.ColorR, options.ColorG, options.ColorB, options.Opacity);

            var (pageWidth, pageHeight) = (FPDF_GetPageWidthF(page), FPDF_GetPageHeightF(page));
            
            // Calculate the transformation matrix to center and rotate the text
            double angleRad = options.Rotation * Math.PI / 180.0;
            double cos_a = Math.Cos(angleRad);
            double sin_a = Math.Sin(angleRad);

            // Calculate translation to center the text
            double centerX = textWidth / 2.0;
            double centerY = textHeight / 2.0;

            double final_e = (pageWidth / 2.0) - (centerX * cos_a - centerY * sin_a);
            double final_f = (pageHeight / 2.0) - (centerX * sin_a + centerY * cos_a);

            // Apply the combined rotation and translation matrix
            FPDFPageObjTransform(textObject, cos_a, sin_a, -sin_a, cos_a, final_e, final_f);

            FPDFPageInsertObject(page, textObject);
        }
        catch
        {
            FPDFPageObjDestroy(textObject);
            throw;
        }
    }
    
    /// <summary>
    /// Adds an image watermark to a specific page
    /// </summary>
    /// <param name="doc">PDFium document handle</param>
    /// <param name="page">PDFium page handle</param>
    /// <param name="options">Watermark options</param>
    private void AddImageWatermarkToPage(FpdfDocumentT doc, FpdfPageT page, WatermarkOptions options)
    {
        if (!File.Exists(options.ImagePath))
        {
            Logger.LogError("Watermark image not found: {ImagePath}", options.ImagePath);
            throw new FileNotFoundException("Watermark image not found.", options.ImagePath);
        }

        using var image = Image.Load(options.ImagePath);
        var imageObject = FPDFPageObjNewImageObj(doc);

        if (imageObject == null)
        {
            Logger.LogError("Failed to create image object");
            throw new InvalidOperationException("Failed to create image object");
        }

        try
        {
            var customConfig = SixLabors.ImageSharp.Configuration.Default.Clone();
            customConfig.PreferContiguousImageBuffers = true;
            
            using var pixelData = image.CloneAs<Bgra32>(customConfig);
            if (!pixelData.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory))
                throw new InvalidOperationException("Could not get contiguous memory for image.");

            using var pinHandle = memory.Pin();
            int stride = pixelData.Width * 4;
            var bitmap = FPDFBitmapCreateEx(pixelData.Width, pixelData.Height, 4, (IntPtr)pinHandle.Pointer, stride);
            
            if (bitmap == null)
            {
                Logger.LogError("Failed to create PDFium bitmap from image");
                throw new InvalidOperationException("Failed to create PDFium bitmap from image.");
            }

            try
            {
                // Set the bitmap for the image object
                if (FPDFImageObjSetBitmap(null, 0, imageObject, bitmap) == 0)
                    throw new InvalidOperationException("Failed to set image object bitmap.");
            }
            finally
            {
                FPDFBitmapDestroy(bitmap);
            }
        
            var (pageWidth, pageHeight) = (FPDF_GetPageWidthF(page), FPDF_GetPageHeightF(page));
            float imageWidth = image.Width * (float)options.Scale;
            float imageHeight = image.Height * (float)options.Scale;
            float x = (pageWidth - imageWidth) / 2;
            float y = (pageHeight - imageHeight) / 2;
            
            FPDFImageObjSetMatrix(imageObject, imageWidth, 0, 0, imageHeight, x, y);
            FPDFPageInsertObject(page, imageObject);
        }
        catch
        {
            FPDFPageObjDestroy(imageObject);
            throw;
        }
    }

    /// <summary>
    /// Saves a PDF document to disk
    /// </summary>
    /// <param name="documentT">PDFium document handle</param>
    /// <param name="outputFilename">Output file path</param>
    private void SaveDocument(FpdfDocumentT documentT, string outputFilename)
    {
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

        var success = FPDF_SaveAsCopy(documentT, fileWrite, 0);
        if (success == 0)
        {
            Logger.LogError("Failed to save PDF document to {OutputFilename}", outputFilename);
            throw new InvalidOperationException("Failed to save PDF document");
        }
    }
}
