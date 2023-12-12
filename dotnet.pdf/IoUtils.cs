using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace dotnet.pdf;

public class IoUtils
{
    static HashSet<char> _allowedChars = new HashSet<char>
    {
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
        'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        ' ', '.', '-', ',', '&', '(', ')', '_', '^'
    };

    public static bool IsValid(string name)
    {
        foreach (char character in name)
        {
            if (!_allowedChars.Contains(character))
            {
                return false;
            }
        }

        return true;
    }

    public static string RemoveInvalidChars(string name)
    {
        StringBuilder nameBuilder = new();
        foreach (char character in name)
        {
            if (_allowedChars.Contains(character))
            {
                nameBuilder.Append(character);
            }
        }

        return nameBuilder.ToString();
    }

    /// <summary>
    /// Returns an image encoder based on the provided file extension.
    /// </summary>
    /// <param name="fileName">The name of the file.</param>
    /// <returns>An instance of an image encoder.</returns>
    /// <exception cref="NotSupportedException">Thrown if the file extension is not supported.</exception>
    public static IImageEncoder GetEncoder(string fileName)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        switch (extension)
        {
            case ".png":
                return new PngEncoder();
            case ".jpg":
            case ".jpeg":
                return new JpegEncoder();
            case ".gif":
                return new GifEncoder();
            case ".bmp":
                return new BmpEncoder();
            default:
                throw new NotSupportedException($"File extension {extension} is not supported");
        }
    }

    /// <summary>
    /// Gets the file extension based on the given image encoder.
    /// </summary>
    /// <param name="encoder">The image encoder to get the file extension for.</param>
    /// <returns>The file extension for the specified image encoder.</returns>
    public static string GetExtension(IImageEncoder encoder)
    {
        if (encoder is PngEncoder)
            return ".png";
        else if (encoder is JpegEncoder)
            return ".jpg";
        else if (encoder is GifEncoder)
            return ".gif";
        else if (encoder is BmpEncoder)
            return ".bmp";
        else
            throw new NotSupportedException($"Encoder type is not supported");
    }
}