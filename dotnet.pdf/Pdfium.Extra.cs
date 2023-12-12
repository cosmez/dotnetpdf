using System.Text;
using PDFiumCore;
using static PDFiumCore.fpdf_doc;
using static PDFiumCore.fpdf_edit;
using static PDFiumCore.fpdfview;
using static PDFiumCore.fpdf_save;
using static PDFiumCore.fpdf_text;
// ReSharper disable StringLiteralTypo

namespace dotnet.pdf;

public partial class Pdfium
{
    static string GetPdfActionType(uint type) => type switch
    {
        1 => "GOTO",
        2 => "REMOTEGOTO",
        3 => "URI",
        4 => "LAUNCH",
        5 => "EMBEDDEDGOTO",
        _ => "UNSUPPORTED"
    };
    
    static string GetUtf16String<T>(T element,
        Func<T, IntPtr, uint, uint> stringMethod)
    {
        uint length = stringMethod(element, IntPtr.Zero, 0);
        if (length == 0) return string.Empty;
        unsafe
        {
            Span<byte> txt = new byte[length];
            fixed (byte* ptrr = &txt[0])
            {
                var endOfString = stringMethod(element, (IntPtr)ptrr, length);
                endOfString -= 2;
                return Encoding.Unicode.GetString(ptrr, endOfString > 0 ? (int)endOfString : 0);
            }
        }
    }
    
    static string GetUtf16String<T>(T element, string field,
        Func<T, string, IntPtr, uint, uint> stringMethod)
    {
        uint endOfString = 0;
        uint length = stringMethod(element, field, IntPtr.Zero, 0);
        if (length == 0) return string.Empty;
        unsafe
        {
            Span<byte> txt = new byte[length];
            fixed (byte* ptrr = &txt[0])
            {
                endOfString = stringMethod(element, field, (IntPtr)ptrr, length);
                endOfString -= 2;
                return Encoding.Unicode.GetString(ptrr, endOfString > 0 ? (int)endOfString : 0);
            }
        }
    }

    public static List<PDfPageText>? GetPdfText(string inputfilename,
        List<int>? pageRange, string password = "")
    {
        var result = new List<PDfPageText>();
        if (inputfilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputfilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputfilename, password);

                var numberOfPages = FPDF_GetPageCount(documentT);

                for (int i = 0; i < numberOfPages; i++)
                {
                    if (pageRange != null && !pageRange.Contains(i+1)) continue;
                    var pageT = FPDF_LoadPage(documentT, i);
                    var pageText = GetPageText(pageT);
                    int charCount = pageText.Characters;
                    pageText.Page = i + 1;
                    Console.WriteLine(pageText.Text);
                    result.Add(pageText);
                    FPDF_ClosePage(pageT);
                }

                FPDF_CloseDocument(documentT);
            }
            return result;
        }

        return null;
    }
    
    public static List<PDfBookmark>? GetPdfBookmarks(string inputfilename, string password = "")
    {
        if (inputfilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputfilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputfilename, password);
                var bookmarks = GetBookmarks(documentT);
                FPDF_CloseDocument(documentT);
                return bookmarks;
            }
        }

        return null;
    }
    
    public static PdfInfo? GetPdfInformation(string inputfilename, string password = "")
    {
        if (inputfilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            && File.Exists(inputfilename))
        {
            lock (PdfiumLock)
            {
                var documentT = FPDF_LoadDocument(inputfilename, password);
                var pdfInfo = new PdfInfo();
                pdfInfo.Pages = FPDF_GetPageCount(documentT);
                pdfInfo.Author = GetMetaText(documentT, "Author");
                pdfInfo.CreationDate = GetMetaText(documentT, "CreationDate");
                pdfInfo.Creator = GetMetaText(documentT, "Creator");
                pdfInfo.Keywords = GetMetaText(documentT, "Keywords");
                pdfInfo.Producer = GetMetaText(documentT, "Procuder");
                pdfInfo.ModifiedDate = GetMetaText(documentT, "ModifiedDate");
                pdfInfo.Subject = GetMetaText(documentT, "Subject");
                pdfInfo.Title = GetMetaText(documentT, "Title");
                int version = 0;
                FPDF_GetFileVersion(documentT, ref version);
                pdfInfo.Version = version;
                pdfInfo.Trapped = GetMetaText(documentT, "Trapped");
                FPDF_CloseDocument(documentT);
                return pdfInfo;
            }
        }

        return null;
    }
    
    public static string GetMetaText(FpdfDocumentT document, string tag)
    {
        return GetUtf16String(document, tag, fpdf_doc.FPDF_GetMetaText);
        
    }
    
    /// <summary>
    /// Get all the document bookmarks
    /// </summary>
    /// <param name="document"></param>
    /// <returns></returns>
    private static List<PDfBookmark> GetBookmarks(FpdfDocumentT document)
    {
        List<PDfBookmark> bookmarks = new();
        void RecurseBookmark(FpdfBookmarkT bookmark, int level)
        {
            // Get the title of the bookmark
            //FPDF_WIDESTRING title = nullptr;
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


                if (actionType is "GOTO" or "REMOTOGOTO")
                {
                    var destT = FPDFActionGetDest(document, actionT);
                    var pageIndex = FPDFDestGetDestPageIndex(document, destT);
                    bMark.Page = pageIndex;
                }
            }
            else
            {
                var destT = FPDFBookmarkGetDest(document, bookmark);
                int pageIndex = FPDFDestGetDestPageIndex(document, destT);
                bMark.Action = "Page";
                bMark.Page = pageIndex;
            }



            bookmarks.Add(bMark);

            if (!string.IsNullOrWhiteSpace(title))
            {

                var child = FPDFBookmarkGetFirstChild(document, bookmark);
                if (child != null)
                {
                    RecurseBookmark(child, level + 1);
                }

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
    
    private static PDfPageText GetPageText(FpdfPageT page)
    {
        var pageText = new PDfPageText();
        var pageT = FPDFTextLoadPage(page);
        pageText.Characters =  FPDFTextCountChars(pageT);
        pageText.Rects = FPDFTextCountRects(pageT, 0, -1);

        // Get the word count
        int wordCount = 0;

        for (int i = 0; i < pageText.Characters; i++)
        {
            var charCode = FPDFTextGetUnicode(pageT, i);
            if (charCode is ' ' or '\n' or '\t')
            {
                wordCount++;
            }
        }

        pageText.WordsCount = wordCount;

        unsafe
        {
            Span<byte> txt = new byte[pageText.Characters * 2 + 1];
            fixed (byte* ptrr = &txt[0])
            {
                FPDFTextGetText(pageT, 0, pageText.Characters, ref *(ushort*)ptrr);
                pageText.Text = Encoding.Unicode.GetString(txt);
            }
        }

        FPDFTextClosePage(pageT);

        return pageText;
    }

}