using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace dotnet.pdf;

public class MoreCommandsHandler : BaseCommandHandler
{
    private readonly ILogger<MoreCommandsHandler> _logger;

    public MoreCommandsHandler(ILogger<MoreCommandsHandler> logger) : base(logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    

 

    public void ListPageObjects(FileInfo input, string? rangeOption, string? password, string outputFormat)
    {
        _logger.LogInformation("Listing page objects for range {Range} in file {File}", rangeOption, input.FullName);
        try
        {
            if (input is null ||
                !ValidateInputFile(input, "text extraction") ||
                !ValidateOutputFormat(outputFormat) ||
                !ValidatePageRange(rangeOption, out var parsedRange))
                return;

            var objects = Pdfium.ListPageObjects(input.FullName, parsedRange, password ?? "");
            if (outputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(objects, SourceGenerationContext.Default.ListPdfPageObjectInfo);
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"Found {objects.Count} objects:");
                foreach (var obj in objects)
                {
                    Console.WriteLine($" - Page: {obj.Page}, Type: {obj.Type}, Bounds: [L: {obj.Left:F2}, B: {obj.Bottom:F2}, R: {obj.Right:F2}, T: {obj.Top:F2}]");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing page objects from file: {FileName}", input.FullName);
            Console.WriteLine($"Error: Failed to list page objects - {ex.Message}");
        }
    }

    public void ListFormFields(FileInfo input, string? password, string outputFormat)
    {
        _logger.LogInformation("Listing form fields for file {File}", input.FullName);
        try
        {
            var fields = Pdfium.ListFormFields(input.FullName, password ?? "");
            if (outputFormat.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                var json = JsonSerializer.Serialize(fields, SourceGenerationContext.Default.ListPdfFormFieldInfo);
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine($"Found {fields.Count} form fields:");
                foreach (var field in fields)
                {
                    Console.WriteLine($" - Page: {field.Page}, Name: {field.Name}, Type: {field.Type}, Value: '{field.Value}', Rect: {field.Rect}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing form fields from file: {FileName}", input.FullName);
            Console.WriteLine($"Error: Failed to list form fields - {ex.Message}");
        }
    }

    public void AddWatermark(FileInfo input, FileInfo output, string? password, 
        string? text, FileInfo? image, string font, double fontSize,
        string color, byte opacity, double rotation, double scale)
    {
        _logger.LogInformation("Adding watermark to file {File}", input.FullName);
        try
        {
            if (string.IsNullOrEmpty(text) && image == null)
            {
                Console.WriteLine("Error: Either --text or --image must be specified for watermarking.");
                return;
            }
            if (!string.IsNullOrEmpty(text) && image != null)
            {
                Console.WriteLine("Error: Both --text and --image cannot be specified simultaneously.");
                return;
            }

            var options = new WatermarkOptions
            {
                Text = text,
                ImagePath = image?.FullName,
                Font = font,
                FontSize = fontSize,
                Opacity = opacity,
                Rotation = rotation,
                Scale = scale
            };
            
            var colorParts = color.Split(',').Select(byte.Parse).ToArray();
            if (colorParts.Length != 3) throw new ArgumentException("Color must be in R,G,B format.");
            options.ColorR = colorParts[0];
            options.ColorG = colorParts[1];
            options.ColorB = colorParts[2];

            Console.WriteLine($"Adding watermark to {input.Name} and saving to {output.Name}...");
            Pdfium.AddWatermark(input.FullName, output.FullName, options, password ?? "");
            Console.WriteLine("Watermark added successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding watermark to file: {FileName}", input.FullName);
            Console.WriteLine($"Error: Failed to add watermark - {ex.Message}");
        }
    }
}