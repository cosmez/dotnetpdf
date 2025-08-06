namespace DotNet.Pdf.Core.Models;


public enum PdfSaveFlags
{
    FPDF_NONE = 0,
    FPDF_INCREMENTAL = 1, // Save as a new file, not incremental
    FPDF_NO_INCREMENTAL = 2, // Save incrementally to an existing file
    FPDF_REMOVE_SECURITY = 3, // Remove security from the PDF
    
}

public enum PdfPageObjectType
{
    FPDF_PAGEOBJ_UNKNOWN = 0,
    FPDF_PAGEOBJ_TEXT = 1,
    FPDF_PAGEOBJ_PATH = 2,
    FPDF_PAGEOBJ_IMAGE = 3,
    FPDF_PAGEOBJ_SHAPE = 4, //is this shape or shading?
    FPDF_PAGEOBJ_FORM = 5,
    FPDF_PAGEOBJ_SVG = 6,
    FPDF_PAGEOBJ_ANNOTATION = 7
}

public enum PdfAnnotationType
{
    FPDF_ANNOT_UNKNOWN = 0,
    FPDF_ANNOT_TEXT = 1,
    FPDF_ANNOT_LINK = 2,
    FPDF_ANNOT_FREETEXT = 3,
    FPDF_ANNOT_LINE = 4,
    FPDF_ANNOT_SQUARE = 5,
    FPDF_ANNOT_CIRCLE = 6,
    FPDF_ANNOT_POLYGON = 7,
    FPDF_ANNOT_POLYLINE = 8,
    FPDF_ANNOT_HIGHLIGHT = 9,
    FPDF_ANNOT_UNDERLINE = 10,
    FPDF_ANNOT_SQUIGGLY = 11,
    FPDF_ANNOT_STRIKEOUT = 12,
    FPDF_ANNOT_STAMP = 13,
    FPDF_ANNOT_CARET = 14,
    FPDF_ANNOT_INK = 15,
    FPDF_ANNOT_POPUP = 16,
    FPDF_ANNOT_FILEATTACHMENT = 17,
    FPDF_ANNOT_SOUND = 18,
    FPDF_ANNOT_MOVIE = 19,
    FPDF_ANNOT_WIDGET = 20,
    FPDF_ANNOT_SCREEN = 21,
    FPDF_ANNOT_PRINTERMARK = 22,
    FPDF_ANNOT_TRAPNET = 23,
    FPDF_ANNOT_WATERMARK = 24,
    FPDF_ANNOT_THREED = 25,
    FPDF_ANNOT_RICHMEDIA = 26,
    FPDF_ANNOT_XFAWIDGET = 27,
    FPDF_ANNOT_REDACT = 28
}

/// <summary>
/// PDFium error codes as defined in the PDFium C++ API
/// </summary>
public enum FpdfErrorCode
{
    /// <summary>
    /// No error
    /// </summary>
    FPDF_ERR_SUCCESS = 0,
    
    /// <summary>
    /// Unknown error
    /// </summary>
    FPDF_ERR_UNKNOWN = 1,
    
    /// <summary>
    /// File not found or could not be opened
    /// </summary>
    FPDF_ERR_FILE = 2,
    
    /// <summary>
    /// File not in PDF format or corrupted
    /// </summary>
    FPDF_ERR_FORMAT = 3,
    
    /// <summary>
    /// Password required or incorrect password
    /// </summary>
    FPDF_ERR_PASSWORD = 4,
    
    /// <summary>
    /// Unsupported security scheme
    /// </summary>
    FPDF_ERR_SECURITY = 5,
    
    /// <summary>
    /// Page not found or content error
    /// </summary>
    FPDF_ERR_PAGE = 6,
    
    /// <summary>
    /// Load XFA error (only available if PDF_ENABLE_XFA is defined)
    /// </summary>
    FPDF_ERR_XFALOAD = 7,
    
    /// <summary>
    /// Layout XFA error (only available if PDF_ENABLE_XFA is defined)
    /// </summary>
    FPDF_ERR_XFALAYOUT = 8
}
