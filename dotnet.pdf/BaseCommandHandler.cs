using Microsoft.Extensions.Logging;

namespace dotnet.pdf;

public class BaseCommandHandler
{

    protected readonly ILogger<BaseCommandHandler> _logger;
    public BaseCommandHandler(ILogger<BaseCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }       

    protected bool ValidateInputFile(FileInfo? input, string operationName)
    {
        if (input == null)
        {
            _logger.LogError("Input file is required for {Operation}", operationName);
            Console.WriteLine($"Error: Input file is required for {operationName}");
            return false;
        }

        if (!input.Exists)
        {
            _logger.LogError("Input file does not exist: {FileName}", input.FullName);
            Console.WriteLine($"Error: Input file does not exist: {input.FullName}");
            return false;
        }

        if (input.Length == 0)
        {
            _logger.LogError("Input file is empty: {FileName}", input.FullName);
            Console.WriteLine($"Error: Input file is empty: {input.FullName}");
            return false;
        }

        return true;
    }

    protected bool ValidateOutputDirectory(DirectoryInfo? outputDirectory, FileInfo inputFile, out DirectoryInfo validOutputDir)
    {
        validOutputDir = outputDirectory ?? inputFile.Directory ?? new DirectoryInfo(Environment.CurrentDirectory);

        try
        {
            if (!validOutputDir.Exists)
            {
                validOutputDir.Create();
                _logger.LogInformation("Created output directory: {Directory}", validOutputDir.FullName);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create or access output directory: {Directory}", validOutputDir.FullName);
            Console.WriteLine($"Error: Failed to create or access output directory: {validOutputDir.FullName}");
            return false;
        }
    }

    protected bool ValidatePageRange(string? rangeOption, out List<int>? parsedRange)
    {
        parsedRange = null;
        
        if (string.IsNullOrWhiteSpace(rangeOption))
            return true;

        try
        {
            parsedRange = Parsers.ParsePageRange(rangeOption);
            if (parsedRange == null || parsedRange.Count == 0)
            {
                _logger.LogError("Invalid page range format: {Range}", rangeOption);
                Console.WriteLine($"Error: Invalid page range format: {rangeOption}");
                return false;
            }

            if (parsedRange.Any(p => p <= 0))
            {
                _logger.LogError("Page numbers must be positive: {Range}", rangeOption);
                Console.WriteLine($"Error: Page numbers must be positive: {rangeOption}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing page range: {Range}", rangeOption);
            Console.WriteLine($"Error: Invalid page range: {rangeOption}");
            return false;
        }
    }

    protected bool ValidateDpi(int dpi)
    {
        if (dpi <= 0 || dpi > 2400)
        {
            _logger.LogError("DPI must be between 1 and 2400, got: {Dpi}", dpi);
            Console.WriteLine($"Error: DPI must be between 1 and 2400, got: {dpi}");
            return false;
        }
        return true;
    }

    protected bool ValidateOutputFormat(string outputFormat)
    {
        var validFormats = new[] { "text", "json", "xml" };
        if (!validFormats.Contains(outputFormat.ToLower()))
        {
            _logger.LogError("Invalid output format: {Format}. Valid formats are: {ValidFormats}", 
                outputFormat, string.Join(", ", validFormats));
            Console.WriteLine($"Error: Invalid output format: {outputFormat}. Valid formats are: {string.Join(", ", validFormats)}");
            return false;
        }
        return true;
    }
}
