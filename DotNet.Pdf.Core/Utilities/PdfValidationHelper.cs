using Microsoft.Extensions.Logging;

namespace DotNet.Pdf.Core.Utilities;

public static class PdfValidationHelper
{
    /// <summary>
    /// Validates if the input file is a valid PDF
    /// </summary>
    /// <param name="inputFilename">Path to the file</param>
    /// <param name="logger">Logger instance for error reporting</param>
    /// <returns>True if valid PDF file, false otherwise</returns>
    public static bool IsValidPdfFile(string inputFilename, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(inputFilename))
        {
            logger?.LogError("Input filename is null or empty");
            return false;
        }

        if (!inputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            logger?.LogError("File is not a PDF: {Filename}", inputFilename);
            return false;
        }

        if (!File.Exists(inputFilename))
        {
            logger?.LogError("File does not exist: {Filename}", inputFilename);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates output filename and creates directory if needed
    /// </summary>
    /// <param name="outputFilename">Path to the output file</param>
    /// <param name="logger">Logger instance for error reporting</param>
    /// <returns>True if output path is valid, false otherwise</returns>
    public static bool ValidateOutputPath(string outputFilename, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(outputFilename))
        {
            logger?.LogError("Output filename cannot be null or empty");
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(outputFilename);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                logger?.LogDebug("Created output directory: {Directory}", directory);
            }
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to create output directory for: {OutputFilename}", outputFilename);
            return false;
        }
    }
}
