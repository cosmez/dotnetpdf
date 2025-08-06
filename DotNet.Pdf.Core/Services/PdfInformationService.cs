using System.Text;
using PDFiumCore;
using static PDFiumCore.fpdf_doc;
using static PDFiumCore.fpdfview;
using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public class PdfInformationService : BasePdfService
{
    public PdfInformationService(ILogger<PdfInformationService> logger) : base(logger, null)
    {
    }

    /// <summary>
    /// Extracts metadata and information from a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <returns>PdfInfo object containing document metadata</returns>
    public PdfInfo? GetPdfInformation(string inputFilename, string password = "")
    {
        if (!IsValidPdfFile(inputFilename))
            return null;

        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                string errorMessage = GetLastPdfiumError();
                Logger.LogError("Failed to load PDF document: {Filename}. Error: {ErrorMessage}", inputFilename, errorMessage);
                return null;
            }

            try
            {
                var pdfInfo = new PdfInfo
                {
                    Pages = FPDF_GetPageCount(documentT),
                    Author = GetMetaText(documentT, "Author"),
                    CreationDate = GetMetaText(documentT, "CreationDate"),
                    Creator = GetMetaText(documentT, "Creator"),
                    Keywords = GetMetaText(documentT, "Keywords"),
                    Producer = GetMetaText(documentT, "Producer"), // Note: Fixed typo from "Procuder"
                    ModifiedDate = GetMetaText(documentT, "ModDate"),
                    Subject = GetMetaText(documentT, "Subject"),
                    Title = GetMetaText(documentT, "Title"),
                    Trapped = GetMetaText(documentT, "Trapped")
                };

                int version = 0;
                FPDF_GetFileVersion(documentT, ref version);
                pdfInfo.Version = version;

                Logger.LogInformation("Extracted PDF information: {Pages} pages, version {Version}", 
                    pdfInfo.Pages, pdfInfo.Version);

                return pdfInfo;
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    /// <summary>
    /// Gets metadata text from a PDF document
    /// </summary>
    /// <param name="document">PDFium document handle</param>
    /// <param name="tag">Metadata tag to retrieve</param>
    /// <returns>Metadata value as string</returns>
    private string GetMetaText(FpdfDocumentT document, string tag)
    {
        return GetUtf16String(document, tag, FPDF_GetMetaText);
    }

    /// <summary>
}
