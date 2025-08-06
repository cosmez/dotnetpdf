using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using PDFiumCore;
using static PDFiumCore.fpdf_annot;
using static PDFiumCore.fpdf_formfill;
using static PDFiumCore.fpdfview;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public unsafe class PdfFormFieldService : BasePdfService
{
    public PdfFormFieldService(ILogger<PdfFormFieldService> logger) : base(logger, null)
    {
    }

    /// <summary>
    /// Lists all form fields in a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PdfFormFieldInfo objects containing field information</returns>
    public List<PdfFormFieldInfo> ListFormFields(string inputFilename, string password = "", IProgress<PdfProgress>? progress = null)
    {
        var formFieldInfos = new List<PdfFormFieldInfo>();
        
        if (!IsValidPdfFile(inputFilename))
            return formFieldInfos;

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                string errorMessage = GetLastPdfiumError();
                Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                return formFieldInfos;
            }

            try
            {
                int pageCount = FPDF_GetPageCount(documentT);
                Logger.LogInformation("Analyzing form fields in {PageCount} pages", pageCount);

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
                        int annotCount = FPDFPageGetAnnotCount(pageT);
                        Logger.LogDebug("Found {AnnotCount} annotations on page {PageNumber}", annotCount, i + 1);

                        for (int j = 0; j < annotCount; j++)
                        {
                            var annot = FPDFPageGetAnnot(pageT, j);
                            if (annot == null) continue;

                            try
                            {
                                // Only process widget annotations (form fields)
                                if (FPDFAnnotGetSubtype(annot) != (int)PdfAnnotationType.FPDF_ANNOT_WIDGET)
                                    continue;

                                var fieldInfo = new PdfFormFieldInfo { Page = i + 1 };
                                
                                // Get field type (without form handle)
                                fieldInfo.Type = "Widget";  // Basic type since we can't use form handle
                                
                                // Get field name
                                fieldInfo.Name = GetAnnotationString(annot, true);

                                // Get field value (for now, will be empty without form handle)
                                fieldInfo.Value = GetAnnotationString(annot, false);

                                // Get field rectangle
                                var rect = new FS_RECTF_();
                                if (FPDFAnnotGetRect(annot, rect) != 0)
                                {
                                    fieldInfo.Rect = $"{rect.Left},{rect.Bottom},{rect.Right},{rect.Top}";
                                }
                                
                                formFieldInfos.Add(fieldInfo);
                                Logger.LogDebug("Found form field '{Name}' of type '{Type}' on page {Page}", 
                                    fieldInfo.Name, fieldInfo.Type, fieldInfo.Page);
                            }
                            finally
                            {
                                FPDFPageCloseAnnot(annot);
                            }
                        }
                    }
                    finally
                    {
                        FPDF_ClosePage(pageT);
                    }
                }

                Logger.LogInformation("Found {FieldCount} form fields in document", formFieldInfos.Count);
                progress?.Report(new PdfProgress(pageCount, pageCount));
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
        
        return formFieldInfos;
    }

    /// <summary>
    /// Gets a string from an annotation (simplified version without form environment)
    /// </summary>
    /// <param name="annot">Annotation handle</param>
    /// <param name="getName">True for name, false for value</param>
    /// <returns>Extracted string</returns>
    private string GetAnnotationString(FpdfAnnotationT annot, bool getName)
    {
        // For now, return empty strings as we're not using the form environment
        // This avoids the crash while still showing that form fields exist
        return string.Empty;
    }
}
