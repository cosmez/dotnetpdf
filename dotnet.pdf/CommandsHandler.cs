using Microsoft.Extensions.Logging;
using System.Text.Json;
using DotNet.Pdf.Core;
using DotNet.Pdf.Core.Models;

namespace dotnet.pdf;

/// <summary>
/// Centralized command handler for all PDF operations
/// </summary>
public class CommandsHandler : BaseCommandHandler
{
    private readonly PdfProcessor _pdfProcessor;

    public CommandsHandler(ILogger<CommandsHandler> logger, ILoggerFactory loggerFactory) : base(logger)
    {
        _pdfProcessor = new PdfProcessor(loggerFactory);
    }

    

    #region Command Implementations

    /// <summary>
    /// Splits a PDF file into multiple files based on the given parameters.
    /// </summary>
    public void SplitPdf(FileInfo input, DirectoryInfo? outputDirectory, string? rangeOption, 
        string? password, bool useBookmarks, string? outputNames, FileInfo? outputScript)
    {
        _logger.LogInformation("Starting PDF split for file: {FileName}, range: {Range}", 
            input.FullName, rangeOption ?? "all");

        try
        {
            if (!ValidateInputFile(input, "PDF splitting") ||
                !ValidatePageRange(rangeOption, out var parsedRange) ||
                !ValidateOutputDirectory(outputDirectory, input, out var validOutputDir))
                return;

            using var operation = _logger.BeginScope("SplitCommand");

            var progress = new Progress<PdfSplitProgress>(progress =>
            {
                Console.WriteLine($"{progress.Current+1}/{progress.Max}\t{progress.Filename}");
            });

            Dictionary<int, string> outputScriptNames = new(); 
        
            if (outputScript?.Exists ?? false)
            {
                try
                {
                    int i = 1;
                    foreach (var line in File.ReadAllLines(outputScript.FullName))
                    {
                        if (line.Contains('='))
                        {
                            string[] parts = line.Split('=');
                            if (int.TryParse(parts[0], out int pageNumber) && parts.Length > 1)
                            {
                                outputScriptNames.TryAdd(pageNumber, parts[1]);
                                i = pageNumber;
                            }
                        } 
                        else
                        {
                            outputScriptNames.TryAdd(i, line);
                        }
                        i++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading output script file: {FileName}", outputScript.FullName);
                    Console.WriteLine($"Error: Failed to read output script file - {ex.Message}");
                    return;
                }
            }

            _pdfProcessor.SplitPdf(input.FullName, validOutputDir.FullName, password: password ?? "",
                outputName: outputNames, useBookmarks: useBookmarks, progress: progress, pageRange: parsedRange,
                outputScriptNames: outputScriptNames);

            _logger.LogInformation("Successfully split PDF: {InputFile} into {OutputDir}", input.FullName, validOutputDir.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error splitting PDF file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to split PDF file - {ex.Message}");
        }
    }

    /// <summary>
    /// Merges PDF files according to the specified parameters.
    /// </summary>
    public void MergePdfs(FileInfo[]? inputFilenames, FileInfo? outputFilename, FileInfo? inputScript,
        DirectoryInfo? inputDirectory, bool useRecursive, string? password)
    {
        _logger.LogInformation("Starting PDF merge operation with {InputCount} files", inputFilenames?.Length ?? 0);

        try
        {
            if (outputFilename == null)
            {
                _logger.LogError("Output filename is required for merge operation");
                Console.WriteLine("Error: Missing output filename --output");
                return;
            }

            if (outputFilename.Exists)
            {
                _logger.LogError("Output file already exists: {FileName}", outputFilename.FullName);
                Console.WriteLine($"{outputFilename.FullName} Already exists, specify another location");
                return;
            }

            using var operation = _logger.BeginScope("MergeCommand");

            List<string> filenames = new();
            
            if (inputFilenames is not null)
            {
                foreach (var file in inputFilenames)
                {
                    if (!ValidateInputFile(file, "merge operation"))
                        continue;
                    filenames.Add(file.FullName);
                }
            }

            if (inputDirectory is not null)
            {
                if (!inputDirectory.Exists)
                {
                    _logger.LogError("Input directory does not exist: {Directory}", inputDirectory.FullName);
                    Console.WriteLine($"Error: Input directory does not exist: {inputDirectory.FullName}");
                    return;
                }

                var searchOptions = useRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var file in Directory.EnumerateFileSystemEntries(inputDirectory.FullName,
                             "*.pdf", searchOptions))
                {
                    if (File.Exists(file))
                        filenames.Add(file);
                }
            }

            if (inputScript?.Exists ?? false)
            {
                try
                {
                    foreach (var line in File.ReadAllLines(inputScript.FullName))
                    {
                        if (File.Exists(line) && Path.GetExtension(line).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            filenames.Add(line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading input script file: {FileName}", inputScript.FullName);
                    Console.WriteLine($"Error: Failed to read input script file - {ex.Message}");
                    return;
                }
            }

            if (filenames.Count == 0)
            {
                _logger.LogError("No valid PDF files found for merge operation");
                Console.WriteLine("Error: No valid PDF files found for merge operation");
                return;
            }

            var progress = new Progress<PdfProgress>(progress =>
            {
                Console.WriteLine($"{progress.Current + 1}/{progress.Max}\t Merge Progress");
            });

            _pdfProcessor.MergeFiles(outputFilename.FullName, filenames, password ?? "", deleteOriginal: false, progress: progress);
            _logger.LogInformation("Successfully merged {Count} files into {OutputFile}", filenames.Count, outputFilename.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging PDF files");
            Console.WriteLine($"Error: Failed to merge PDF files - {ex.Message}");
        }
    }

    /// <summary>
    /// Converts a PDF file to images using the specified parameters.
    /// </summary>
    public void ConvertPdfToImages(FileInfo input, DirectoryInfo? outputDirectory, string? rangeOption, 
        string? password, string? outputNames, int dpi, string encoder)
    {
        _logger.LogInformation("Starting PDF to image conversion for file: {FileName}, dpi: {Dpi}, encoder: {Encoder}", 
            input?.FullName, dpi, encoder);

        try
        {
            if (input is null ||
                !ValidateInputFile(input, "PDF to image conversion") ||
                !ValidatePageRange(rangeOption, out var parsedRange) ||
                !ValidateDpi(dpi) ||
                !ValidateOutputDirectory(outputDirectory, input, out var validOutputDir))
                return;

            using var operation = _logger.BeginScope("ConvertCommand");

            // Determine image format from encoder parameter or output file extension
            string imageFormat = encoder;
            
            if (!string.IsNullOrEmpty(outputNames))
            {
                var ext = Path.GetExtension(outputNames);
                if (!string.IsNullOrEmpty(ext))
                {
                    imageFormat = ext.TrimStart('.');
                }
            }
            
            var progress = new Progress<PdfProgress>(progress =>
            {
                Console.WriteLine($"{progress.Current+1}/{progress.Max}\t Encoding");
            });

            _pdfProcessor.ConvertPdfToImage(input.FullName, validOutputDir.FullName, imageFormat, dpi, 
                parsedRange, password ?? "", outputNames, progress);

            _logger.LogInformation("Successfully converted PDF to images: {InputFile} -> {OutputDir}", 
                input.FullName, validOutputDir.FullName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting PDF to images: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to convert PDF to images - {ex.Message}");
        }
    }

    /// <summary>
    /// Converts an image file to a PDF document.
    /// </summary>
    public void ConvertImageToPdf(FileInfo input, FileInfo? output)
    {
        _logger.LogInformation("Starting image to PDF conversion for file: {FileName}", input?.FullName);

        try
        {
            if (input is null ||
                !ValidateInputFile(input, "image to PDF conversion"))
                return;

            using var operation = _logger.BeginScope("ImageToPdfCommand");

            _pdfProcessor.ConvertImageToPdf(input.FullName, output?.FullName);

            _logger.LogInformation("Successfully converted image to PDF: {OutputFile}", 
                output?.FullName ?? Path.ChangeExtension(input.FullName, ".pdf"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting image to PDF: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to convert image to PDF - {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts text from a PDF file and outputs it in the specified format.
    /// </summary>
    public void ExtractText(FileInfo input, string? rangeOption, string? password, string outputFormat)
    {
        _logger.LogInformation("Starting text extraction for file: {FileName}, range: {Range}", 
            input?.FullName, rangeOption ?? "all");

        try
        {
            if (input is null || 
                !ValidateInputFile(input, "text extraction") ||
                !ValidateOutputFormat(outputFormat) ||
                !ValidatePageRange(rangeOption, out var parsedRange))
                return;

            using var operation = _logger.BeginScope("TextCommand");

            var texts = _pdfProcessor.GetPdfText(input.FullName, parsedRange, password ?? "");

            if (texts is not null)
            {
                if (outputFormat.ToLower() == "text")
                {
                    foreach (var text in texts)
                    {
                        Console.WriteLine(text.Text);
                    }
                }
                else if (outputFormat.ToLower() == "json")
                {
                    var jsonString = JsonSerializer.Serialize(
                        texts, DotNet.Pdf.Core.Models.SourceGenerationContext.Default.ListPDfPageText);
                    Console.WriteLine(jsonString);
                }

                _logger.LogInformation("Successfully extracted text from {Count} pages", texts.Count);
            }
            else
            {
                _logger.LogWarning("Failed to extract text from file: {FileName}", input.FullName);
                Console.WriteLine("Error: Failed to extract text");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting text from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to extract text - {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts bookmarks from a PDF file and outputs them in the specified format.
    /// </summary>
    public void ExtractBookmarks(FileInfo input, string? password, string outputFormat)
    {
        _logger.LogInformation("Starting bookmark extraction for file: {FileName}", input?.FullName);

        try
        {
            if (input is null ||
                !ValidateInputFile(input, "bookmark extraction") ||
                !ValidateOutputFormat(outputFormat))
                return;

            using var operation = _logger.BeginScope("BookmarksCommand");

            var bookmarks = _pdfProcessor.GetPdfBookmarks(input.FullName, password ?? "");
            if (bookmarks is not null)
            {
                if (outputFormat.ToLower() == "text")
                {
                    foreach (var bookmark in bookmarks)
                    {
                        Console.WriteLine($"{bookmark.Level}> {bookmark.Title}\t[{bookmark.Action} {bookmark.Page}]");
                    }
                }
                else if (outputFormat.ToLower() == "json")
                {
                    var jsonString = JsonSerializer.Serialize(
                        bookmarks, DotNet.Pdf.Core.Models.SourceGenerationContext.Default.ListPDfBookmark);
                    Console.WriteLine(jsonString);
                }

                _logger.LogInformation("Successfully extracted {Count} bookmarks", bookmarks.Count);
            }
            else
            {
                _logger.LogWarning("No bookmarks found or failed to extract bookmarks from file: {FileName}", input.FullName);
                Console.WriteLine("No bookmarks found or failed to extract bookmarks");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting bookmarks from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to extract bookmarks - {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves information from a PDF file and displays it in the console or as a JSON string.
    /// </summary>
    public void GetPdfInfo(FileInfo input, string? password, string outputFormat)
    {
        _logger.LogInformation("Starting info extraction for file: {FileName}", input?.FullName);

        try
        {
            if (input is null ||
                !ValidateInputFile(input, "info extraction") ||
                !ValidateOutputFormat(outputFormat))
                return;

            using var operation = _logger.BeginScope("InfoCommand");
            
            var pdfInfo = _pdfProcessor.GetPdfInformation(input.FullName, password ?? "");

            if (pdfInfo is not null)
            {
                if (outputFormat.ToLower() == "text")
                {
                    Console.WriteLine($"Pages = {pdfInfo.Pages}");
                    Console.WriteLine($"Author = {pdfInfo.Author}");
                    Console.WriteLine($"CreationDate = {pdfInfo.CreationDate}");
                    Console.WriteLine($"Creator = {pdfInfo.Creator}");
                    Console.WriteLine($"Keywords = {pdfInfo.Keywords}");
                    Console.WriteLine($"Producer = {pdfInfo.Producer}");
                    Console.WriteLine($"ModifiedDate = {pdfInfo.ModifiedDate}");
                    Console.WriteLine($"Subject = {pdfInfo.Subject}");
                    Console.WriteLine($"Title = {pdfInfo.Title}");
                    Console.WriteLine($"Version = {pdfInfo.Version}");
                    Console.WriteLine($"Trapped = {pdfInfo.Trapped}");
                }
                else if (outputFormat.ToLower() == "json")
                {
                    var jsonString = JsonSerializer.Serialize(
                        pdfInfo, DotNet.Pdf.Core.Models.SourceGenerationContext.Default.PdfInfo);
                    Console.WriteLine(jsonString);
                }

                _logger.LogInformation("Successfully extracted PDF info for {Pages} pages", pdfInfo.Pages);
            }
            else
            {
                _logger.LogWarning("Failed to extract PDF information from file: {FileName}", input.FullName);
                Console.WriteLine("Error: Failed to extract PDF information");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting PDF info from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to extract PDF info - {ex.Message}");
        }
    }

    /// <summary>
    /// Rotates specified pages in a PDF document
    /// </summary>
    public void RotatePdfPages(FileInfo input, FileInfo output, int rotation, string? rangeOption, string? password)
    {
        try
        {
            if (!ValidateInputFile(input, "rotate PDF pages")) return;

            if (output == null)
            {
                _logger.LogError("Output file is required for rotate operation");
                Console.WriteLine("Error: Output file is required for rotate operation");
                return;
            }

            var pageRange = Parsers.ParsePageRange(rangeOption);

            _logger.LogInformation("Rotating pages in PDF: {InputFile} -> {OutputFile}", input.FullName, output.FullName);
            Console.WriteLine($"Rotating pages in PDF: {input.Name} -> {output.Name}");

            _pdfProcessor.RotatePdfPages(input.FullName, output.FullName, rotation, pageRange, password ?? "");

            _logger.LogInformation("Successfully rotated PDF pages");
            Console.WriteLine("PDF pages rotated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rotating PDF pages from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to rotate PDF pages - {ex.Message}");
        }
    }

    /// <summary>
    /// Removes specified pages from a PDF document
    /// </summary>
    public void RemovePdfPages(FileInfo input, FileInfo output, string pageNumbersToRemove, string? password)
    {
        try
        {
            if (!ValidateInputFile(input, "remove PDF pages")) return;

            if (output == null)
            {
                _logger.LogError("Output file is required for remove operation");
                Console.WriteLine("Error: Output file is required for remove operation");
                return;
            }

            var pagesToRemove = ParsePageNumbers(pageNumbersToRemove);
            if (!pagesToRemove.Any())
            {
                _logger.LogError("No valid pages specified for removal");
                Console.WriteLine("Error: No valid pages specified for removal");
                return;
            }

            _logger.LogInformation("Removing pages from PDF: {InputFile} -> {OutputFile}", input.FullName, output.FullName);
            Console.WriteLine($"Removing pages from PDF: {input.Name} -> {output.Name}");

            _pdfProcessor.RemovePdfPages(input.FullName, output.FullName, pagesToRemove, password ?? "");

            _logger.LogInformation("Successfully removed PDF pages");
            Console.WriteLine("PDF pages removed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing PDF pages from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to remove PDF pages - {ex.Message}");
        }
    }

    /// <summary>
    /// Inserts blank pages into a PDF document
    /// </summary>
    public void InsertBlankPages(FileInfo input, FileInfo output, string insertSpec, double pageWidth, double pageHeight, string? password)
    {
        try
        {
            if (!ValidateInputFile(input, "insert blank pages")) return;

            if (output == null)
            {
                _logger.LogError("Output file is required for insert operation");
                Console.WriteLine("Error: Output file is required for insert operation");
                return;
            }

            var insertPositions = ParseInsertSpec(insertSpec);
            if (!insertPositions.Any())
            {
                _logger.LogError("No valid insert positions specified");
                Console.WriteLine("Error: No valid insert positions specified");
                return;
            }

            _logger.LogInformation("Inserting blank pages into PDF: {InputFile} -> {OutputFile}", input.FullName, output.FullName);
            Console.WriteLine($"Inserting blank pages into PDF: {input.Name} -> {output.Name}");

            _pdfProcessor.InsertBlankPages(input.FullName, output.FullName, insertPositions, pageWidth, pageHeight, password ?? "");

            _logger.LogInformation("Successfully inserted blank pages");
            Console.WriteLine("Blank pages inserted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting blank pages into file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to insert blank pages - {ex.Message}");
        }
    }

    /// <summary>
    /// Reorders pages in a PDF document
    /// </summary>
    public void ReorderPdfPages(FileInfo input, FileInfo output, string newOrder, string? password)
    {
        try
        {
            if (!ValidateInputFile(input, "reorder PDF pages")) return;

            if (output == null)
            {
                _logger.LogError("Output file is required for reorder operation");
                Console.WriteLine("Error: Output file is required for reorder operation");
                return;
            }

            var pageOrder = ParsePageNumbers(newOrder);
            if (!pageOrder.Any())
            {
                _logger.LogError("No valid page order specified");
                Console.WriteLine("Error: No valid page order specified");
                return;
            }

            _logger.LogInformation("Reordering pages in PDF: {InputFile} -> {OutputFile}", input.FullName, output.FullName);
            Console.WriteLine($"Reordering pages in PDF: {input.Name} -> {output.Name}");

            _pdfProcessor.ReorderPdfPages(input.FullName, output.FullName, pageOrder, password ?? "");

            _logger.LogInformation("Successfully reordered PDF pages");
            Console.WriteLine("PDF pages reordered successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering PDF pages from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to reorder PDF pages - {ex.Message}");
        }
    }

    /// <summary>
    /// Lists attachments in a PDF document
    /// </summary>
    public void ListAttachments(FileInfo input, string? password, string outputFormat)
    {
        try
        {
            if (!ValidateInputFile(input, "list attachments")) return;

            _logger.LogInformation("Listing attachments from PDF: {InputFile}", input.FullName);
            Console.WriteLine($"Listing attachments from PDF: {input.Name}");

            var attachments = _pdfProcessor.GetPdfAttachments(input.FullName, password ?? "");

            if (attachments == null)
            {
                _logger.LogWarning("Failed to extract attachments from file: {FileName}", input.FullName);
                Console.WriteLine("Error: Failed to extract attachments");
                return;
            }

            if (!attachments.Any())
            {
                Console.WriteLine("No attachments found in the PDF document.");
                return;
            }

            switch (outputFormat.ToLowerInvariant())
            {
                case "json":
                    var jsonText = JsonSerializer.Serialize(attachments, DotNet.Pdf.Core.Models.SourceGenerationContext.Default.ListPdfAttachment);
                    Console.WriteLine(jsonText);
                    break;
                case "text":
                default:
                    Console.WriteLine($"Found {attachments.Count} attachment(s):");
                    Console.WriteLine();
                    for (int i = 0; i < attachments.Count; i++)
                    {
                        var attachment = attachments[i];
                        Console.WriteLine($"Attachment {i}:");
                        Console.WriteLine($"  Name: {attachment.Name}");
                        Console.WriteLine($"  Size: {attachment.Size:N0} bytes");
                        Console.WriteLine($"  MIME Type: {attachment.MimeType}");
                        if (attachment.CreationDate.HasValue)
                            Console.WriteLine($"  Created: {attachment.CreationDate.Value:yyyy-MM-dd HH:mm:ss}");
                        if (attachment.ModificationDate.HasValue)
                            Console.WriteLine($"  Modified: {attachment.ModificationDate.Value:yyyy-MM-dd HH:mm:ss}");
                        if (!string.IsNullOrEmpty(attachment.Description))
                            Console.WriteLine($"  Description: {attachment.Description}");
                        if (attachment.Metadata.Any())
                        {
                            Console.WriteLine("  Metadata:");
                            foreach (var metadata in attachment.Metadata)
                            {
                                Console.WriteLine($"    {metadata.Key}: {metadata.Value}");
                            }
                        }
                        Console.WriteLine();
                    }
                    break;
            }

            _logger.LogInformation("Successfully listed {Count} attachments", attachments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing attachments from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to list attachments - {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts attachments from a PDF document
    /// </summary>
    public void ExtractAttachments(FileInfo input, DirectoryInfo? outputDirectory, string? password, string? attachmentIndex)
    {
        try
        {
            if (!ValidateInputFile(input, "extract attachments")) return;

            if (!ValidateOutputDirectory(outputDirectory, input, out var validOutputDir)) return;

            _logger.LogInformation("Extracting attachments from PDF: {InputFile} to {OutputDir}", input.FullName, validOutputDir.FullName);
            Console.WriteLine($"Extracting attachments from PDF: {input.Name} to {validOutputDir.Name}");

            // First, get the list of attachments
            var attachments = _pdfProcessor.GetPdfAttachments(input.FullName, password ?? "");

            if (attachments == null || !attachments.Any())
            {
                Console.WriteLine("No attachments found in the PDF document.");
                return;
            }

            var extractedCount = 0;

            if (!string.IsNullOrEmpty(attachmentIndex))
            {
                // Extract specific attachment by index
                if (int.TryParse(attachmentIndex, out var index) && index >= 0 && index < attachments.Count)
                {
                    var attachment = attachments[index];
                    var outputPath = Path.Combine(validOutputDir.FullName, IoUtils.RemoveInvalidChars(attachment.Name));
                    
                    if (_pdfProcessor.ExtractAttachment(input.FullName, index, outputPath, password ?? ""))
                    {
                        Console.WriteLine($"Successfully extracted: {attachment.Name} ({attachment.Size:N0} bytes)");
                        extractedCount = 1;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to extract attachment: {attachment.Name}");
                    }
                }
                else
                {
                    Console.WriteLine($"Invalid attachment index: {attachmentIndex}. Valid range: 0-{attachments.Count - 1}");
                    return;
                }
            }
            else
            {
                // Extract all attachments
                for (int i = 0; i < attachments.Count; i++)
                {
                    var attachment = attachments[i];
                    var outputPath = Path.Combine(validOutputDir.FullName, IoUtils.RemoveInvalidChars(attachment.Name));
                    
                    if (_pdfProcessor.ExtractAttachment(input.FullName, i, outputPath, password ?? ""))
                    {
                        Console.WriteLine($"Extracted: {attachment.Name} ({attachment.Size:N0} bytes)");
                        extractedCount++;
                    }
                    else
                    {
                        Console.WriteLine($"Failed to extract: {attachment.Name}");
                    }
                }
            }

            _logger.LogInformation("Successfully extracted {Count} of {Total} attachments", extractedCount, attachments.Count);
            Console.WriteLine($"Extraction complete. {extractedCount} of {attachments.Count} attachments extracted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting attachments from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to extract attachments - {ex.Message}");
        }
    }

    #region Helper Methods for New Commands

    /// <summary>
    /// Parses a comma-separated list of page numbers
    /// </summary>
    private List<int> ParsePageNumbers(string pageNumbers)
    {
        var result = new List<int>();
        
        if (string.IsNullOrWhiteSpace(pageNumbers))
            return result;

        var parts = pageNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out var pageNum) && pageNum > 0)
            {
                result.Add(pageNum);
            }
        }

        return result;
    }

    /// <summary>
    /// Parses insert specification like "1:2,5:1" (position:count)
    /// </summary>
    private Dictionary<int, int> ParseInsertSpec(string insertSpec)
    {
        var result = new Dictionary<int, int>();
        
        if (string.IsNullOrWhiteSpace(insertSpec))
            return result;

        var parts = insertSpec.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var positionCount = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (positionCount.Length == 2 && 
                int.TryParse(positionCount[0].Trim(), out var position) && position > 0 &&
                int.TryParse(positionCount[1].Trim(), out var count) && count > 0)
            {
                result[position] = count;
            }
        }

        return result;
    }

    #endregion

    #region PDF Security Operations

    /// <summary>
    /// Unlocks a password-protected PDF document by removing security restrictions
    /// </summary>
    /// <param name="input">The input password-protected PDF file</param>
    /// <param name="output">The output file where the unlocked PDF will be saved</param>
    /// <param name="password">The password required to open the input PDF</param>
    public void UnlockPdf(FileInfo input, FileInfo output, string password)
    {
        try
        {
            if (!ValidateInputFile(input, "unlock PDF")) return;

            if (output == null)
            {
                _logger.LogError("Output file is required for unlock operation");
                Console.WriteLine("Error: Output file is required for unlock operation");
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("Password is required for unlock operation");
                Console.WriteLine("Error: Password is required for unlock operation");
                return;
            }

            _logger.LogInformation("Unlocking PDF: {InputFile} -> {OutputFile}", input.FullName, output.FullName);
            Console.WriteLine($"Unlocking PDF: {input.Name} -> {output.Name}");

            _pdfProcessor.UnlockPdf(input.FullName, output.FullName, password);

            _logger.LogInformation("Successfully unlocked PDF");
            Console.WriteLine("PDF unlocked successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking PDF from file: {FileName}", input?.FullName);
            Console.WriteLine($"Error: Failed to unlock PDF - {ex.Message}");
        }
    }

    #endregion

    #endregion
}