using System.Text;
using PDFiumCore;
using static PDFiumCore.fpdf_doc;
using static PDFiumCore.fpdfview;
using Microsoft.Extensions.Logging;
using DotNet.Pdf.Core.Models;

namespace DotNet.Pdf.Core.Services;

public class PdfBookmarkService : BasePdfService
{
    public PdfBookmarkService(ILogger<PdfBookmarkService> logger) : base(logger, null)
    {
    }

    /// <summary>
    /// Extracts all bookmarks from a PDF document
    /// </summary>
    /// <param name="inputFilename">Path to the PDF file</param>
    /// <param name="password">Optional password to unlock the PDF</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of PDfBookmark objects containing bookmark information</returns>
    public List<PDfBookmark>? GetPdfBookmarks(string inputFilename, string password = "", IProgress<PdfBookmarkProgress>? progress = null)
    {
        if (!IsValidPdfFile(inputFilename))
            return null;

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
                var bookmarks = GetBookmarks(documentT, progress);
                Logger.LogInformation("Extracted {BookmarkCount} bookmarks from PDF", bookmarks.Count);
                return bookmarks;
            }
            finally
            {
                FPDF_CloseDocument(documentT);
            }
        }
    }

    /// <summary>
    /// Gets all bookmarks from a PDF document
    /// </summary>
    /// <param name="document">PDFium document handle</param>
    /// <param name="progress">Optional progress reporting callback</param>
    /// <returns>List of bookmarks with hierarchical structure flattened</returns>
    private List<PDfBookmark> GetBookmarks(FpdfDocumentT document, IProgress<PdfBookmarkProgress>? progress = null)
    {
        List<PDfBookmark> bookmarks = new();
        
        void RecurseBookmark(FpdfBookmarkT bookmark, int level)
        {
            // Get the title of the bookmark
            string title = GetUtf16String(bookmark, FPDFBookmarkGetTitle);
            var bMark = new PDfBookmark
            {
                Title = title,
                Level = level,
                Action = string.Empty
            };

            var actionT = FPDFBookmarkGetAction(bookmark);
            if (actionT != null)
            {
                var actionTypeId = FPDFActionGetType(actionT);
                var actionType = GetPdfActionType(actionTypeId);
                bMark.Action = actionType;

                if (actionType is "GOTO" or "REMOTEGOTO")
                {
                    var destT = FPDFActionGetDest(document, actionT);
                    if (destT != null)
                    {
                        var pageIndex = FPDFDestGetDestPageIndex(document, destT);
                        bMark.Page = pageIndex + 1; // Convert to 1-based page number
                    }
                }
            }
            else
            {
                var destT = FPDFBookmarkGetDest(document, bookmark);
                if (destT != null)
                {
                    int pageIndex = FPDFDestGetDestPageIndex(document, destT);
                    bMark.Action = "Page";
                    bMark.Page = pageIndex + 1; // Convert to 1-based page number
                }
            }

            bookmarks.Add(bMark);

            // Report progress for each bookmark
            progress?.Report(new PdfBookmarkProgress(bookmarks.Count, bookmarks.Count, bMark));

            if (!string.IsNullOrWhiteSpace(title))
            {
                // Process child bookmarks
                var child = FPDFBookmarkGetFirstChild(document, bookmark);
                if (child != null)
                {
                    RecurseBookmark(child, level + 1);
                }

                // Process sibling bookmarks
                var sibling = FPDFBookmarkGetNextSibling(document, bookmark);
                if (sibling != null)
                {
                    RecurseBookmark(sibling, level);
                }
            }
        }

        var rootBookmark = FPDFBookmarkGetFirstChild(document, null);
        if (rootBookmark != null)
        {
            RecurseBookmark(rootBookmark, 0);
        }

        return bookmarks;
    }




}
