using System.Text;
using PDFiumCore;
using static PDFiumCore.fpdf_text;
using static PDFiumCore.fpdfview;
using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public class PdfTextExtractionService : BasePdfService
{
    public PdfTextExtractionService(ILogger<PdfTextExtractionService> logger) : base(logger, null)
    {
    }

    /// <summary>
    /// Extracts text from all pages or specified pages of a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="pageRange">Optional list of page numbers to extract from. If null, extracts from all pages</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PDfPageText objects containing text from each page</returns>
    public List<PDfPageText>? GetPdfText(string inputFilename, List<int>? pageRange, string password = "", IProgress<PdfTextProgress>? progress = null)
    {
        if (!IsValidPdfFile(inputFilename))
            return null;

        var result = new List<PDfPageText>();
        
        lock (PdfiumLock)
        {
            InitLibrary();
            var documentT = FPDF_LoadDocument(inputFilename, password);
            if (documentT == null)
            {
                Logger.LogError("Failed to load PDF document: {Filename}", inputFilename);
                return null;
            }

            try
            {
                var numberOfPages = FPDF_GetPageCount(documentT);
                Logger.LogInformation("Processing {PageCount} pages for text extraction", numberOfPages);

                int processedPages = 0;
                for (int i = 0; i < numberOfPages; i++)
                {
                    if (pageRange != null && !pageRange.Contains(i + 1)) continue;
                    
                    var pageT = FPDF_LoadPage(documentT, i);
                    if (pageT == null)
                    {
                        Logger.LogWarning("Failed to load page {PageNumber}", i + 1);
                        continue;
                    }

                    try
                    {
                        var pageText = GetPageText(pageT);
                        pageText.Page = i + 1;
                        result.Add(pageText);
                        Logger.LogDebug("Extracted text from page {PageNumber}: {CharacterCount} characters", 
                            i + 1, pageText.Characters);

                        // Report progress
                        processedPages++;
                        progress?.Report(new PdfTextProgress(processedPages, numberOfPages, pageText));
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
        
        return result;
    }

    /// <summary>
    /// Extracts text from a single page
    /// </summary>
    /// <param name="page">PDFium page handle</param>
    /// <returns>PDfPageText object containing page text information</returns>
    private PDfPageText GetPageText(FpdfPageT page)
    {
        var pageText = new PDfPageText();
        var textPageT = FPDFTextLoadPage(page);
        
        if (textPageT == null)
        {
            Logger.LogWarning("Failed to load text page");
            return pageText;
        }

        try
        {
            pageText.Characters = FPDFTextCountChars(textPageT);
            pageText.Rects = FPDFTextCountRects(textPageT, 0, -1);

            // Get the word count
            int wordCount = 0;
            for (int i = 0; i < pageText.Characters; i++)
            {
                var charCode = FPDFTextGetUnicode(textPageT, i);
                if (charCode is ' ' or '\n' or '\t')
                {
                    wordCount++;
                }
            }
            pageText.WordsCount = wordCount;

            // Extract the actual text
            if (pageText.Characters > 0)
            {
                pageText.Text = GetUtf16TextString(pageText.Characters, ptr => 
                {
                    unsafe
                    {
                        return (uint)FPDFTextGetText(textPageT, 0, pageText.Characters, ref *(ushort*)ptr.ToPointer());
                    }
                });
            }
        }
        finally
        {
            FPDFTextClosePage(textPageT);
        }

        return pageText;
    }
}
