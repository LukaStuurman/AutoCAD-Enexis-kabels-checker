using System.Globalization;
using System.Text.RegularExpressions;

namespace Enexis.KabelChecker.Core;

public static class CurrentTextParser
{
    private static readonly Regex SingleNumberPattern = new(
        @"(?<![\d.,])\d+(?:[.,]\d+)?(?![\d.,])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseSingleCurrent(string? text, out double amps)
    {
        amps = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var matches = SingleNumberPattern.Matches(text);
        if (matches.Count != 1)
            return false;

        var normalized = matches[0].Value.Replace(',', '.');
        if (!double.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out amps))
        {
            return false;
        }

        return amps >= 0 && !double.IsNaN(amps) && !double.IsInfinity(amps);
    }
}
