using System.Text.RegularExpressions;

namespace ReSeq.Core.Services;

public static partial class VideoNameParser
{
    [GeneratedRegex(@"^([1-9]\d*)-([1-9]\d*)(\.[^.]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex StrictNameRegex();

    public static bool TryParse(string fileName, out int x, out int y, out string extension)
    {
        x = 0;
        y = 0;
        extension = string.Empty;

        var match = StrictNameRegex().Match(fileName);
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups[1].Value, out x) ||
            !int.TryParse(match.Groups[2].Value, out y))
        {
            return false;
        }

        extension = match.Groups[3].Value;
        return true;
    }
}
