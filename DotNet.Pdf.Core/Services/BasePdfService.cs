using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DotNet.Pdf.Core.Models;
using Microsoft.Extensions.Logging;
using PDFiumCore;
using static PDFiumCore.fpdf_annot;
using static PDFiumCore.fpdf_formfill;

namespace DotNet.Pdf.Core.Services;

public abstract class BasePdfService
{
    protected static readonly object PdfiumLock = new();
    protected readonly ILogger Logger;
    protected readonly ILoggerFactory? LoggerFactory;
    protected static bool IsInitialized = false;

    public void InitLibrary()
    {
        if (!IsInitialized)
        {
            fpdfview.FPDF_InitLibrary();
            IsInitialized = true;
            Logger.LogInformation("PDFium library initialized successfully.");
        }
        else
        {
            Logger.LogWarning("PDFium library is already initialized.");
        }
    }

    public void ShutdownLibrary()
    {
        if (IsInitialized)
        {
            fpdfview.FPDF_DestroyLibrary();
            Logger.LogInformation("PDFium library destroyed successfully.");
            IsInitialized = false;
        }
        else
        {
            Logger.LogWarning("PDFium library is not initialized.");
        }
    }

    /// <summary>
    /// Initializes a new instance of the BasePdfService
    /// </summary>
    /// <param name="logger">Logger instance for this service</param>
    /// <param name="loggerFactory">Optional logger factory for creating other service loggers</param>
    protected BasePdfService(ILogger logger, ILoggerFactory? loggerFactory = null)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        LoggerFactory = loggerFactory;
    }

    /// <summary>
    /// Validates if the input file is a valid PDF
    /// </summary>
    /// <param name="inputFilename">Path to the file</param>
    /// <returns>True if valid PDF file, false otherwise</returns>
    protected bool IsValidPdfFile(string inputFilename)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
        {
            Logger.LogError("Input filename is null or empty");
            return false;
        }

        if (!inputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogError("File is not a PDF: {Filename}", inputFilename);
            return false;
        }

        if (!File.Exists(inputFilename))
        {
            Logger.LogError("File does not exist: {Filename}", inputFilename);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a UTF-16 string from PDFium using the specified method
    /// </summary>
    /// <typeparam name="T">Type of the element</typeparam>
    /// <param name="element">The element to get string from</param>
    /// <param name="stringMethod">Method to call for string extraction</param>
    /// <returns>Extracted string</returns>
    protected string GetUtf16String<T>(T element, Func<T, IntPtr, uint, uint> stringMethod)
    {
        uint length = stringMethod(element, IntPtr.Zero, 0);
        if (length == 0) return string.Empty;

        unsafe
        {
            Span<byte> txt = new byte[length];
            fixed (byte* ptrr = &txt[0])
            {
                var endOfString = stringMethod(element, (IntPtr)ptrr, length);
                endOfString -= 2; // Remove null terminator
                return Encoding.Unicode.GetString(ptrr, endOfString > 0 ? (int)endOfString : 0);
            }
        }
    }

    /// <summary>
    /// Gets a UTF-16 string from PDFium using the specified method with an additional field parameter
    /// </summary>
    /// <typeparam name="T">Type of the element</typeparam>
    /// <param name="element">The element to get string from</param>
    /// <param name="field">Field name to retrieve</param>
    /// <param name="stringMethod">Method to call for string extraction</param>
    /// <returns>Extracted string</returns>
    protected string GetUtf16String<T>(T element, string field, Func<T, string, IntPtr, uint, uint> stringMethod)
    {
        uint length = stringMethod(element, field, IntPtr.Zero, 0);
        if (length == 0) return string.Empty;
        
        unsafe
        {
            Span<byte> txt = new byte[length];
            fixed (byte* ptrr = &txt[0])
            {
                var endOfString = stringMethod(element, field, (IntPtr)ptrr, length);
                endOfString -= 2; // Remove null terminator
                return Encoding.Unicode.GetString(ptrr, endOfString > 0 ? (int)endOfString : 0);
            }
        }
    }

    /// <summary>
    /// Gets a UTF-16 string from PDFium text extraction with known character count
    /// </summary>
    /// <param name="characterCount">Number of characters to extract</param>
    /// <param name="extractionAction">Action that performs the actual text extraction</param>
    /// <returns>Extracted string</returns>
    protected unsafe string GetUtf16TextString(int characterCount, Func<IntPtr, uint> extractionAction)
    {
        if (characterCount <= 0) return string.Empty;

        Span<byte> txt = new byte[characterCount * 2 + 2]; // Add extra space for null terminator
        fixed (byte* ptrr = &txt[0])
        {
            var actualLength = extractionAction((IntPtr)ptrr);
            if (actualLength > 0)
            {
                return Encoding.Unicode.GetString(txt[..(int)(actualLength * 2)]);
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Gets a UTF-16 string from PDFium form field functions that use ushort buffers
    /// </summary>
    /// <param name="formHandle">Form handle</param>
    /// <param name="annot">Annotation handle</param>
    /// <param name="nameOrValue">True for name extraction, false for value extraction</param>
    /// <returns>Extracted UTF-16 string</returns>
    protected unsafe string GetFormFieldString(FpdfFormHandleT formHandle, FpdfAnnotationT annot, bool nameOrValue)
    {
        uint length;
        if (nameOrValue)
            length = FPDFAnnotGetFormFieldName(formHandle, annot, ref *(ushort*)IntPtr.Zero, 0);
        else
            length = FPDFAnnotGetFormFieldValue(formHandle, annot, ref *(ushort*)IntPtr.Zero, 0);
            
        if (length == 0) return string.Empty;
        
        var buffer = new ushort[length];
        if (nameOrValue)
            FPDFAnnotGetFormFieldName(formHandle, annot, ref buffer[0], length);
        else
            FPDFAnnotGetFormFieldValue(formHandle, annot, ref buffer[0], length);
        
        return Marshal.PtrToStringUni(
            (IntPtr)System.Runtime.CompilerServices.Unsafe.AsPointer(ref buffer[0]), 
            (int)(length / 2) - 1);
    }

    /// <summary>
    /// Gets a human-readable string representation of a page object type
    /// </summary>
    /// <param name="value">Page object type value from PDFium</param>
    /// <returns>String description of the object type</returns>
    protected static string GetPageObjectType(int value)
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
    /// Gets the PDF action type name from the action type ID
    /// </summary>
    /// <param name="type">Action type ID</param>
    /// <returns>Human-readable action type name</returns>
    protected static string GetPdfActionType(uint type) => type switch
    {
        1 => "GOTO",
        2 => "REMOTEGOTO",
        3 => "URI",
        4 => "LAUNCH",
        5 => "EMBEDDEDGOTO",
        _ => "UNSUPPORTED"
    };

    /// <summary>
    /// Gets the last PDFium error and returns a human-readable error message
    /// </summary>
    /// <returns>Error message describing the last PDFium error</returns>
    protected string GetLastPdfiumError()
    {
        var errorCode = (FpdfErrorCode)fpdfview.FPDF_GetLastError();
        return GetPdfiumErrorMessage(errorCode);
    }

    /// <summary>
    /// Converts a PDFium error code to a human-readable error message
    /// </summary>
    /// <param name="errorCode">The PDFium error code</param>
    /// <returns>Human-readable error message</returns>
    protected static string GetPdfiumErrorMessage(FpdfErrorCode errorCode)
    {
        return errorCode switch
        {
            FpdfErrorCode.FPDF_ERR_SUCCESS => "No error",
            FpdfErrorCode.FPDF_ERR_UNKNOWN => "Unknown error",
            FpdfErrorCode.FPDF_ERR_FILE => "File not found or could not be opened",
            FpdfErrorCode.FPDF_ERR_FORMAT => "File not in PDF format or corrupted",
            FpdfErrorCode.FPDF_ERR_PASSWORD => "Password required or incorrect password",
            FpdfErrorCode.FPDF_ERR_SECURITY => "Unsupported security scheme",
            FpdfErrorCode.FPDF_ERR_PAGE => "Page not found or content error",
            FpdfErrorCode.FPDF_ERR_XFALOAD => "Load XFA error",
            FpdfErrorCode.FPDF_ERR_XFALAYOUT => "Layout XFA error",
            _ => $"Unknown error code: {(int)errorCode}"
        };
    }

}
