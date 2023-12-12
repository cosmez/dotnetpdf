using System.Text.Json.Serialization;

namespace dotnet.pdf;

public record PdfProgress(int Current, int Max);
public record PdfSplitProgress(int Current, int Max, string Filename);
public record PdfTextProgress(int Current, int Max, PDfPageText PageText);
public record PdfBookmarkProgress(int Current, int Max, PDfBookmark PageText);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PdfInfo))]
[JsonSerializable(typeof(List<PDfBookmark>))]
[JsonSerializable(typeof(List<PDfPageText>))]
internal partial class SourceGenerationContext : JsonSerializerContext
{
}

public class PdfInfo
{
    public int Pages { get; set; }
    public string Author { get; set; } = "";
    public string CreationDate { get; set; } = "";
    public string Creator { get; set; } = "";
    public string Keywords { get; set; } = "";
    public string Producer { get; set; } = "";
    public string ModifiedDate { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Title { get; set; } = "";
    public int Version { get; set; } 
    public string Trapped { get; set; } = "";
}
 
public class PDfBookmark
{
    public string Title { get; set; } = "";
    public int Level { get; set; }
    private List<PDfBookmark> Bookmarks { get; set; } = new();
    public string Action { get; set; } = "";
    public int Page { get; set; }
}

public class PDfPageText
{
    public int Page { get; set; }
    public int Words { get; set; }
    public int Characters { get; set; }
    public string Text { get; set; } = "";
    public int Rects { get; set; }
    public int WordsCount { get; set; }
}