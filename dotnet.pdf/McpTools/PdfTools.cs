using ModelContextProtocol.Server;
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace dotnet.pdf.McpTools;

// Define minimal AI content record types locally to avoid missing dependency names
/*public abstract record AIContent;
public sealed record TextContent(string Text) : AIContent;
public sealed record DataContent(string Data) : AIContent;
*/
/// <summary>
/// Unified PDF input representation for MCP tools.
/// Exactly one of FilePath, Base64Data, Url should be provided.
/// </summary>
public class PdfInput
{
    public string? FilePath { get; set; }
    public string? Base64Data { get; set; }
    public string? Url { get; set; }
    public string? Password { get; set; }
}

internal static class PdfInputExtensions
{
    public static async Task<FileInfo?> ResolveToFileAsync(this PdfInput input, HttpClient http)
    {
        if (!string.IsNullOrWhiteSpace(input.FilePath))
        {
            var fi = new FileInfo(input.FilePath);
            return fi.Exists ? fi : null;
        }
        if (!string.IsNullOrWhiteSpace(input.Base64Data))
        {
            try
            {
                var tmp = Path.GetTempFileName() + ".pdf";
                var bytes = Convert.FromBase64String(input.Base64Data);
                await File.WriteAllBytesAsync(tmp, bytes);
                return new FileInfo(tmp);
            }
            catch
            {
                return null;
            }
        }
        if (!string.IsNullOrWhiteSpace(input.Url))
        {
            try
            {
                var tmp = Path.GetTempFileName() + ".pdf";
                await using var stream = await http.GetStreamAsync(new Uri(input.Url));
                await using var output = File.OpenWrite(tmp);
                await stream.CopyToAsync(output);
                return new FileInfo(tmp);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }
}

[McpServerToolType]
public class PdfTools
{
    private readonly CommandsHandler _cmd;
    private readonly MoreCommandsHandler _more;
    private readonly HttpClient _http;

    public PdfTools(CommandsHandler cmd, MoreCommandsHandler more, HttpClient http)
    {
        _cmd = cmd; _more = more; _http = http;
    }

    private static string CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        using var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error: {ex.Message}");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        return sw.ToString();
    }

    private async Task<(FileInfo? file, string? error)> GetFileAsync(PdfInput input)
    {
        var file = await input.ResolveToFileAsync(_http);
        if (file == null)
        {
            return (null, "Could not resolve PDF input to local file (expected FilePath, Base64Data or Url)");
        }
        return (file, null);
    }

    // Range format explanation reused across tools
    private const string RangeHelp = "Range format: 1-based pages, comma separated singles and inclusive ranges. Examples: '1,3,5-8,10' or '2-4,7'.";

    [McpServerTool, Description("Split a PDF. Optional range. " + RangeHelp + " useBookmarks => name pages from bookmarks. Returns console output + OutputDir path.")]
    public async Task<string> SplitPdf(PdfInput input, string? range = null, bool useBookmarks = false, string? outputNames = null)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "split-" + Guid.NewGuid().ToString("N")));
        outDir.Create();
        var output = CaptureConsole(() => _cmd.SplitPdf(file!, outDir, range, input.Password, useBookmarks, outputNames, null));
        return output + $"\nOutputDir={outDir.FullName}";
    }

    [McpServerTool, Description("Merge PDFs. Provide additionalFiles list. Returns console output + OutputFile path.")]
    public async Task<string> MergePdfs(PdfInput primary, List<PdfInput> additionalFiles, string? password = null)
    {
        var files = new List<FileInfo>();
        var (first, err) = await GetFileAsync(primary);
        if (err != null) return err;
        files.Add(first!);
        foreach (var add in additionalFiles)
        {
            var (f, e) = await GetFileAsync(add);
            if (e != null) return e;
            files.Add(f!);
        }
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _cmd.MergePdfs(files.ToArray(), outputFile, null, null, false, password ?? primary.Password));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("Convert PDF pages to images. Optional range. " + RangeHelp + " dpi (1-2400), encoder (.jpg/.png/etc), outputNames pattern. Returns console + OutputDir path.")]
    public async Task<string> ConvertPdfToImages(PdfInput input, string? range = null, int dpi = 200, string encoder = ".jpg", string? outputNames = null)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "convert-" + Guid.NewGuid().ToString("N")));
        outDir.Create();
        var output = CaptureConsole(() => _cmd.ConvertPdfToImages(file!, outDir, range, input.Password, outputNames, dpi, encoder));
        return output + $"\nOutputDir={outDir.FullName}";
    }

    [McpServerTool, Description("Extract text. Optional range. " + RangeHelp + " format text|json. Returns console output.")]
    public async Task<string> ExtractText(PdfInput input, string? range = null, string format = "text")
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        return CaptureConsole(() => _cmd.ExtractText(file!, range, input.Password, format));
    }

    [McpServerTool, Description("Extract bookmarks. format: text|json. Returns console output.")]
    public async Task<string> ExtractBookmarks(PdfInput input, string format = "text")
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        return CaptureConsole(() => _cmd.ExtractBookmarks(file!, input.Password, format));
    }

    [McpServerTool, Description("Get PDF info. format: text|json. Returns console output.")]
    public async Task<string> GetInfo(PdfInput input, string format = "text")
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        return CaptureConsole(() => _cmd.GetPdfInfo(file!, input.Password, format));
    }

    [McpServerTool, Description("Rotate pages. Optional range. " + RangeHelp + " rotation (90/180/270). Returns console + OutputFile path.")]
    public async Task<string> Rotate(PdfInput input, int rotation = 90, string? range = null)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _cmd.RotatePdfPages(file!, outputFile, rotation, range, input.Password));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("Remove pages by comma list e.g. 1,3,5. Returns console + OutputFile path.")]
    public async Task<string> RemovePages(PdfInput input, string pages)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _cmd.RemovePdfPages(file!, outputFile, pages, input.Password));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("Insert blank pages using spec position:count e.g. 1:2,5:1. Returns console + OutputFile path.")]
    public async Task<string> InsertBlankPages(PdfInput input, string spec, double width = 612.0, double height = 792.0)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _cmd.InsertBlankPages(file!, outputFile, spec, width, height, input.Password));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("Reorder pages with new order comma list e.g. 3,1,2. Returns console + OutputFile path.")]
    public async Task<string> ReorderPages(PdfInput input, string order)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _cmd.ReorderPdfPages(file!, outputFile, order, input.Password));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("List attachments format text|json. Returns console output.")]
    public async Task<string> ListAttachments(PdfInput input, string format = "text")
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        return CaptureConsole(() => _cmd.ListAttachments(file!, input.Password, format));
    }

    [McpServerTool, Description("Extract attachments. Optional index to extract single attachment. Returns console + OutputDir path.")]
    public async Task<string> ExtractAttachments(PdfInput input, string? index = null)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "attach-" + Guid.NewGuid().ToString("N")));
        outDir.Create();
        var output = CaptureConsole(() => _cmd.ExtractAttachments(file!, outDir, input.Password, index));
        return output + $"\nOutputDir={outDir.FullName}";
    }

    [McpServerTool, Description("List page objects. Optional range. " + RangeHelp + " format text|json. Returns console output.")]
    public async Task<string> ListPageObjects(PdfInput input, string? range = null, string format = "text")
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        return CaptureConsole(() => _more.ListPageObjects(file!, range, input.Password, format));
    }

    [McpServerTool, Description("List form fields. format text|json. Returns console output.")]
    public async Task<string> ListFormFields(PdfInput input, string format = "text")
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        return CaptureConsole(() => _more.ListFormFields(file!, input.Password, format));
    }

    [McpServerTool, Description("Remove single page object by page number (1-based) and index (0-based). Returns console + OutputFile path.")]
    public async Task<string> RemovePageObject(PdfInput input, int page, int index)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _more.RemovePageObject(file!, outputFile, page, index, input.Password));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("Add watermark (either text or imagePath). Color R,G,B. Returns console + OutputFile path.")]
    public async Task<string> AddWatermark(PdfInput input, string? text = null, string? imagePath = null, string font = "Helvetica", double fontSize = 50.0, string color = "128,128,128", byte opacity = 50, double rotation = 45.0, double scale = 1.0)
    {
        if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(imagePath)) return "Specify either text or imagePath, not both";
        if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(imagePath)) return "Must specify text or imagePath";
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        FileInfo? imageFile = null;
        if (!string.IsNullOrWhiteSpace(imagePath)) imageFile = new FileInfo(imagePath);
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _more.AddWatermark(file!, outputFile, input.Password, text, imageFile, font, fontSize, color, opacity, rotation, scale));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool, Description("Unlock password protected PDF. Returns console + OutputFile path.")]
    public async Task<string> Unlock(PdfInput input)
    {
        var (file, err) = await GetFileAsync(input);
        if (err != null) return err;
        var outputFile = new FileInfo(Path.GetTempFileName() + ".pdf");
        var output = CaptureConsole(() => _cmd.UnlockPdf(file!, outputFile, input.Password ?? string.Empty));
        return output + $"\nOutputFile={outputFile.FullName}";
    }

    [McpServerTool(Name = "renderPageBase64"), Description("Render a single PDF page to a base64 data URL image (png or jpg). Specify 1-based page number, format png|jpg, optional dpi (default 200). Returns TextContent + DataContent.")]
    public async Task<IEnumerable<AIContent>> RenderPageBase64(PdfInput input, int page, string format = "png", int dpi = 200)
    {
        var list = new List<AIContent>();
        if (page < 1)
        {
            list.Add(new TextContent("error: page must be >= 1"));
            return list;
        }
        if (!string.IsNullOrEmpty(format)) format = format.Trim('.').ToLowerInvariant();
        if (format != "png" && format != "jpg" && format != "jpeg")
        {
            list.Add(new TextContent("error: format must be png or jpg"));
            return list;
        }
        var (file, err) = await GetFileAsync(input);
        if (err != null)
        {
            list.Add(new TextContent(err));
            return list;
        }
        var outDir = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "render-" + Guid.NewGuid().ToString("N")));
        outDir.Create();
        var encoder = format == "png" ? ".png" : ".jpg";
        var outputPattern = "render-{page}" + encoder;
        var range = page.ToString();
        CaptureConsole(() => _cmd.ConvertPdfToImages(file!, outDir, range, input.Password, outputPattern, dpi, encoder));
        var imageFile = outDir.GetFiles("render-*" + encoder).FirstOrDefault();
        if (imageFile == null)
        {
            list.Add(new TextContent("error: failed to render page"));
            return list;
        }
        string mime = format == "png" ? "image/png" : "image/jpeg";
        var bytes = await File.ReadAllBytesAsync(imageFile.FullName);
        var b64 = Convert.ToBase64String(bytes);
        var dataUrl = $"data:{mime};base64,{b64}";
        list.Add(new TextContent($"Rendered page {page} ({format}, dpi={dpi})"));
        list.Add(new DataContent(dataUrl));
        return list;
    }
}