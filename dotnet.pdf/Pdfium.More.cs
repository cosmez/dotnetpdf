using PDFiumCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.InteropServices;
using static PDFiumCore.fpdf_annot;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdf_formfill;
using static PDFiumCore.fpdf_save;
using static PDFiumCore.fpdfview;

namespace dotnet.pdf;

public unsafe partial class Pdfium
{
    public static string GetPageObjectType(int value)
    {
        return value switch
        {
            (int)PdfPageObjectType.FPDF_PAGEOBJ_UNKNOWN => $"Unknown ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_TEXT => $"Text ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_PATH => $"Path ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_IMAGE => $"Image ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_SHAPE => $"Shape ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_FORM => $"Form ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_SVG => $"SVG ({value})",
            (int)PdfPageObjectType.FPDF_PAGEOBJ_ANNOTATION => $"Annotation ({value})",
            _ => $"Unknown ({value})"
        };
    }
    
    /// <summary>
    /// Lists all page objects on a specific page.
    /// </summary>
    public static List<PdfPageObjectInfo> ListPageObjects(string inputFilename, List<int>? pageRange, string password = "")
    {
        var objectInfos = new List<PdfPageObjectInfo>();
        lock (PdfiumLock)
        {
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null) throw new InvalidOperationException("Failed to load PDF document.");

            try
            {
                var numberOfPages = FPDF_GetPageCount(documentT);
                for (int pageI = 0; pageI < numberOfPages; pageI++)
                {
                    if (pageRange != null && !pageRange.Contains(pageI + 1)) continue;

                    var pageT = FPDF_LoadPage(documentT, pageI);
                    if (pageT == null) throw new InvalidOperationException($"Failed to load page {pageI}.");

                    try
                    {
                        int objectCount = FPDFPageCountObjects(pageT);
                        for (int i = 0; i < objectCount; i++)
                        {

                            var pageObject = FPDFPageGetObject(pageT, i);
                            if (pageObject == null) continue;

                            float left = 0, bottom = 0, right = 0, top = 0;

                            _ = fpdf_edit.FPDFPageObjGetBounds(pageObject, ref left, ref bottom, ref right, ref top);


                            var objectInfo = new PdfPageObjectInfo
                            {
                                Type = GetPageObjectType(FPDFPageObjGetType(pageObject)),
                                Left = left,
                                Bottom = bottom,
                                Right = right,
                                Top = top,
                                Page = pageI + 1
                            };
                            objectInfos.Add(objectInfo);
                        }
                    }
                    finally
                    {
                        FPDF_ClosePage(pageT);
                    }
                }
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
        return objectInfos;
    }

    /// <summary>
    /// Lists all form fields in the document.
    /// </summary>
    public static List<PdfFormFieldInfo> ListFormFields(string inputFilename, string password = "")
    {
        var formFieldInfos = new List<PdfFormFieldInfo>();
        lock (PdfiumLock)
        {
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null) throw new InvalidOperationException("Failed to load PDF document.");

            try
            {
                var formInfo = new FPDF_FORMFILLINFO();
                formInfo.Version = 2;

                var formHandle = FPDFDOC_InitFormFillEnvironment(documentT, formInfo);
                if (formHandle == null) throw new InvalidOperationException("Failed to initialize form fill environment.");
                
                try
                {
                    int pageCount = FPDF_GetPageCount(documentT);
                    for (int i = 0; i < pageCount; i++)
                    {
                        var pageT = FPDF_LoadPage(documentT, i);
                        try
                        {
                            FORM_OnAfterLoadPage(pageT, formHandle);
                            
                            int annotCount = FPDFPageGetAnnotCount(pageT);
                            for (int j = 0; j < annotCount; j++)
                            {
                                
                                var annot = FPDFPageGetAnnot(pageT, j);
                                if (annot == null) continue;

                                try
                                {
                                    
                                    if (FPDFAnnotGetSubtype(annot) != (int)PdfAnnotationType.FPDF_ANNOT_WIDGET)
                                        continue;

                                    var fieldInfo = new PdfFormFieldInfo { Page = i + 1 };
                                    
                                    fieldInfo.Type = FPDFAnnotGetFormFieldType(formHandle, annot).ToString();
                                    
                                    // Get Name
                                    uint nameLen = FPDFAnnotGetFormFieldName(formHandle, annot, ref *(ushort*)IntPtr.Zero, 0);
                                    if (nameLen > 0)
                                    {
                                        var buffer = new ushort[nameLen];
                                        FPDFAnnotGetFormFieldName(formHandle, annot, ref buffer[0], nameLen);
                                        fieldInfo.Name = Marshal.PtrToStringUni((IntPtr)System.Runtime.CompilerServices.Unsafe.AsPointer(ref buffer[0]), (int)(nameLen/2) - 1);
                                    }

                                    // Get Value
                                    uint valueLen = FPDFAnnotGetFormFieldValue(formHandle, annot, ref *(ushort*)IntPtr.Zero, 0);
                                    if (valueLen > 0)
                                    {
                                        var buffer = new ushort[valueLen];
                                        FPDFAnnotGetFormFieldValue(formHandle, annot, ref buffer[0], valueLen);
                                        fieldInfo.Value = Marshal.PtrToStringUni((IntPtr)System.Runtime.CompilerServices.Unsafe.AsPointer(ref buffer[0]), (int)(valueLen/2) - 1);
                                    }

                                    var rect = new FS_RECTF_();
                                    if(FPDFAnnotGetRect(annot, rect) != 0)
                                    {
                                       fieldInfo.Rect = $"{rect.Left},{rect.Bottom},{rect.Right},{rect.Top}";
                                    }
                                    
                                    formFieldInfos.Add(fieldInfo);
                                }
                                finally
                                {
                                    FPDFPageCloseAnnot(annot);
                                }
                            }
                            FORM_OnBeforeClosePage(pageT, formHandle);
                        }
                        finally
                        {
                            FPDF_ClosePage(pageT);
                        }
                    }
                }
                finally
                {
                    FPDFDOC_ExitFormFillEnvironment(formHandle);
                }
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
        return formFieldInfos;
    }
    
    /// <summary>
    /// Adds a text or image watermark to a PDF document.
    /// </summary>
    public static void AddWatermark(string inputFilename, string outputFilename, WatermarkOptions options, string password = "")
    {
        lock (PdfiumLock)
        {
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null) throw new InvalidOperationException("Failed to load PDF document.");

            try
            {
                int pageCount = FPDF_GetPageCount(documentT);
                for (int i = 0; i < pageCount; i++)
                {
                    var pageT = FPDF_LoadPage(documentT, i);
                    try
                    {
                        if (!string.IsNullOrEmpty(options.Text))
                            AddTextWatermarkToPage(documentT, pageT, options);
                        else if (!string.IsNullOrEmpty(options.ImagePath))
                            AddImageWatermarkToPage(documentT, pageT, options);

                        FPDFPageGenerateContent(pageT);
                    }
                    finally
                    {
                        FPDF_ClosePage(pageT);
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

                var success = fpdf_save.FPDF_SaveAsCopy(documentT, fileWrite, 0);
                if (success == 0)
                {
                    throw new InvalidOperationException("Failed to save PDF document");
                }
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    private static void AddTextWatermarkToPage(FpdfDocumentT doc, FpdfPageT page, WatermarkOptions options)
    {
        var font = FPDFTextLoadStandardFont(doc, options.Font);
        if (font == null) throw new InvalidOperationException($"Failed to load font: {options.Font}");

        var textObject = FPDFPageObjNewTextObj(doc, options.Font, (float)options.FontSize);
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
            FPDFPageObjDestroy(textObject); // Clean up the created object
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

        // The transform matrix is [a b c d e f]
        // a = cos(angle), b = sin(angle), c = -sin(angle), d = cos(angle)
        // We need to calculate e and f (the translation) to center the object.
        // The final position of the text's center should be (pageWidth/2, pageHeight/2).
        // The text object's own center is at (textWidth/2, textHeight/2).
        // The formula for the transformed center is:
        // new_x = a * old_x + c * old_y + e
        // new_y = b * old_x + d * old_y + f
        // We solve for e and f to place the center correctly.
        double centerX = textWidth / 2.0;
        double centerY = textHeight / 2.0;

        double final_e = (pageWidth / 2.0) - (centerX * cos_a - centerY * sin_a);
        double final_f = (pageHeight / 2.0) - (centerX * sin_a + centerY * cos_a);

        // Apply the combined rotation and translation matrix
        FPDFPageObjTransform(textObject, cos_a, sin_a, -sin_a, cos_a, final_e, final_f);

        FPDFPageInsertObject(page, textObject);
    }
    
    private static void AddImageWatermarkToPage(FpdfDocumentT doc, FpdfPageT page, WatermarkOptions options)
    {
        if (!File.Exists(options.ImagePath)) throw new FileNotFoundException("Watermark image not found.", options.ImagePath);

        using var image = Image.Load(options.ImagePath);
        var imageObject = FPDFPageObjNewImageObj(doc);

        unsafe
        {
            Configuration customConfig = Configuration.Default.Clone();
            customConfig.PreferContiguousImageBuffers = true;
            
            using var pixelData = image.CloneAs<Bgra32>(customConfig);
            if (!pixelData.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory))
                throw new InvalidOperationException("Could not get contiguous memory for image.");

            using var pinHandle = memory.Pin();
            int stride = pixelData.Width * 4;
            var bitmap = FPDFBitmapCreateEx(pixelData.Width, pixelData.Height, (int)FPDFBitmapFormat.BGRA, (IntPtr)pinHandle.Pointer, stride);
            
            if (bitmap == null) throw new InvalidOperationException("Failed to create PDFium bitmap from image.");

            try
            {
                // Set the bitmap, no need to provide pages to clear cache for a new object
                if (FPDFImageObjSetBitmap(null, 0, imageObject, bitmap) == 0)
                    throw new InvalidOperationException("Failed to set image object bitmap.");
            }
            finally
            {
                FPDFBitmapDestroy(bitmap);
            }
        }
        
        var (pageWidth, pageHeight) = (FPDF_GetPageWidthF(page), FPDF_GetPageHeightF(page));
        float imageWidth = image.Width * (float)options.Scale;
        float imageHeight = image.Height * (float)options.Scale;
        float x = (pageWidth - imageWidth) / 2;
        float y = (pageHeight - imageHeight) / 2;
        
        FPDFImageObjSetMatrix(imageObject, imageWidth, 0, 0, imageHeight, x, y);
        FPDFPageInsertObject(page, imageObject);
    }
}