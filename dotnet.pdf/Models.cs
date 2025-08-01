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
[JsonSerializable(typeof(List<PdfAttachment>))]
[JsonSerializable(typeof(List<PdfPageObjectInfo>))] 
[JsonSerializable(typeof(List<PdfFormFieldInfo>))]  
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

public class PdfAttachment
{
    public string Name { get; set; } = "";
    public string MimeType { get; set; } = "";
    public int Size { get; set; }
    public DateTime? CreationDate { get; set; }
    public DateTime? ModificationDate { get; set; }
    public string Description { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class PdfFormFieldInfo
{
    public int Page { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public string Rect { get; set; } = "";
}

public class WatermarkOptions
{
    public string? Text { get; set; }
    public string? ImagePath { get; set; }
    public string Font { get; set; } = "Helvetica";
    public double FontSize { get; set; } = 50;
    public byte Opacity { get; set; } = 50;
    public double Rotation { get; set; } = 45;
    public double Scale { get; set; } = 1.0;
    public byte ColorR { get; set; } = 255;
    public byte ColorG { get; set; }
    public byte ColorB { get; set; }
}

public class PdfPageObjectInfo
{
    public string Type { get; set; } = "";
    public float Left { get; set; }
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public int Page { get; set; }
}
