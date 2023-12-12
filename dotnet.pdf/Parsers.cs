namespace dotnet.pdf;

public class Parsers
{
    /// <summary>
    /// Parses a given page range string and returns a list of page numbers.
    /// </summary>
    /// <param name="pageRange">The string representation of the page range.</param>
    /// <returns>A list of integers representing the page numbers. Returns null if the parsing fails.</returns>
    public static List<int>? ParsePageRange(string? pageRange)
    {
        try
        {
            if (pageRange is null) return null;
            var pageRanges = pageRange.Split(',');
            var pageList = new List<int>();
            foreach (var range in pageRanges)
            {
                if (range.Contains("-"))
                {
                    var startEnd = range.Split('-');
                    if (int.TryParse(startEnd[0], out int start) && int.TryParse(startEnd[1], out int end))
                    {
                        for (int i = start; i <= end; i++)
                        {
                            pageList.Add(i);
                        }
                    }
                }
                else if (int.TryParse(range, out int pageNumber))
                {
                    pageList.Add(pageNumber);
                }
            }

            return pageList;
        }
        catch
        {
            return null;
        }
    }
}