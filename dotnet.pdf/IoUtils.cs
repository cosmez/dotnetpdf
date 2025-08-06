using System.Text;

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
}