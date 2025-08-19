using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using DotNet.Pdf.Core.Models;

namespace dotnet.pdf.McpTools;

[McpServerToolType]
public class BasicTools
{
    [McpServerTool, Description("Downloads a pdf file into a temporary directory, returns the local filename to send to other commands or an erorr string")]
    public static async Task<string> DownloadFile(HttpClient httpClient, [Description("Pdf url to download to process later")] string url)
    {
        var tmpFile = Path.GetTempFileName();
        string pdfFilename = $"{tmpFile}.pdf";
        try
        {
            await using var stream = await httpClient.GetStreamAsync(new Uri(url));
            await using var outputStream = File.OpenWrite(pdfFilename);
            await stream.CopyToAsync(outputStream);
            return pdfFilename;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(ex);
        }
    }


    [McpServerTool, Description("Saves this base64 encoded pdf into a local file and returns the filename, use it only for very small pdfs")]
    public static async Task<string> CreateFromBase64([Description("Pdf filename contents as base64 string")] string base64)
    {
        var tmpFile = Path.GetTempFileName();
        string pdfFilename = $"{tmpFile}.pdf";
        try
        {
            await using var outputStream = File.OpenWrite(pdfFilename);
            var bytes = Convert.FromBase64String(base64);
            await outputStream.WriteAsync(bytes, 0, bytes.Length);
            return pdfFilename;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(ex);
        }
    }
}
