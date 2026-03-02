using System.Text;
using System.Text.RegularExpressions;
using FileExplorer.App.ViewModels;

namespace FileExplorer.App.Services;

/// <summary>
/// Parses ANSI escape sequences from terminal output into colored segments.
/// Supports SGR (Select Graphic Rendition) codes: basic colors, bright colors, 256-color.
/// </summary>
public static partial class AnsiParser
{
    // Captures everything between ESC[ and the final letter (m for SGR)
    [GeneratedRegex(@"\x1B\[([0-9;]*)m", RegexOptions.Compiled)]
    private static partial Regex AnsiSgrRegex();

    // Strip all other escape sequences we don't handle (cursor movement, etc.)
    [GeneratedRegex(@"\x1B\[[0-9;]*[A-HJKSTfhlnsu]|\x1B\][^\x07]*\x07|\x1B\(B", RegexOptions.Compiled)]
    private static partial Regex AnsiOtherRegex();

    private static readonly string[] BasicColors =
    {
        "#0C0C0C", // 0 Black
        "#C50F1F", // 1 Red
        "#13A10E", // 2 Green
        "#C19C00", // 3 Yellow
        "#0037DA", // 4 Blue
        "#881798", // 5 Magenta
        "#3A96DD", // 6 Cyan
        "#CCCCCC", // 7 White
    };

    private static readonly string[] BrightColors =
    {
        "#767676", // 0 Bright Black
        "#E74856", // 1 Bright Red
        "#16C60C", // 2 Bright Green
        "#F9F1A5", // 3 Bright Yellow
        "#3B78FF", // 4 Bright Blue
        "#B4009E", // 5 Bright Magenta
        "#61D6D6", // 6 Bright Cyan
        "#F2F2F2", // 7 Bright White
    };

    /// <summary>
    /// Parses raw terminal output containing ANSI escape codes into a sequence
    /// of text segments with optional foreground/background colors and bold flag.
    /// </summary>
    public static List<AnsiSegment> Parse(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
            return new List<AnsiSegment>();

        // Strip non-SGR escape sequences first
        var cleaned = AnsiOtherRegex().Replace(rawText, string.Empty);

        var segments = new List<AnsiSegment>();
        string? currentFg = null;
        string? currentBg = null;
        bool currentBold = false;

        int lastIndex = 0;
        foreach (Match match in AnsiSgrRegex().Matches(cleaned))
        {
            // Text before this escape sequence
            if (match.Index > lastIndex)
            {
                var text = cleaned[lastIndex..match.Index];
                if (!string.IsNullOrEmpty(text))
                    segments.Add(new AnsiSegment(text, currentFg, currentBg, currentBold));
            }

            // Parse the SGR parameters
            var codes = match.Groups[1].Value;
            if (string.IsNullOrEmpty(codes))
            {
                // ESC[m is the same as ESC[0m (reset)
                currentFg = null;
                currentBg = null;
                currentBold = false;
            }
            else
            {
                var parts = codes.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!int.TryParse(parts[i], out int code)) continue;

                    switch (code)
                    {
                        case 0: // Reset
                            currentFg = null;
                            currentBg = null;
                            currentBold = false;
                            break;
                        case 1: // Bold
                            currentBold = true;
                            break;
                        case 22: // Normal intensity
                            currentBold = false;
                            break;
                        case >= 30 and <= 37: // Standard FG
                            currentFg = currentBold ? BrightColors[code - 30] : BasicColors[code - 30];
                            break;
                        case 39: // Default FG
                            currentFg = null;
                            break;
                        case >= 40 and <= 47: // Standard BG
                            currentBg = BasicColors[code - 40];
                            break;
                        case 49: // Default BG
                            currentBg = null;
                            break;
                        case >= 90 and <= 97: // Bright FG
                            currentFg = BrightColors[code - 90];
                            break;
                        case >= 100 and <= 107: // Bright BG
                            currentBg = BrightColors[code - 100];
                            break;
                        case 38: // Extended FG (256 or RGB)
                            if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 2], out int colorIndex))
                                    currentFg = Get256Color(colorIndex);
                                i += 2;
                            }
                            else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 2], out int r) &&
                                    int.TryParse(parts[i + 3], out int g) &&
                                    int.TryParse(parts[i + 4], out int b))
                                    currentFg = $"#{r:X2}{g:X2}{b:X2}";
                                i += 4;
                            }
                            break;
                        case 48: // Extended BG  
                            if (i + 1 < parts.Length && parts[i + 1] == "5" && i + 2 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 2], out int colorIndex))
                                    currentBg = Get256Color(colorIndex);
                                i += 2;
                            }
                            else if (i + 1 < parts.Length && parts[i + 1] == "2" && i + 4 < parts.Length)
                            {
                                if (int.TryParse(parts[i + 2], out int r) &&
                                    int.TryParse(parts[i + 3], out int g) &&
                                    int.TryParse(parts[i + 4], out int b))
                                    currentBg = $"#{r:X2}{g:X2}{b:X2}";
                                i += 4;
                            }
                            break;
                    }
                }
            }

            lastIndex = match.Index + match.Length;
        }

        // Remaining text after the last escape sequence
        if (lastIndex < cleaned.Length)
        {
            var text = cleaned[lastIndex..];
            if (!string.IsNullOrEmpty(text))
                segments.Add(new AnsiSegment(text, currentFg, currentBg, currentBold));
        }

        // If no escape sequences were found, return the whole text as one segment
        if (segments.Count == 0 && !string.IsNullOrEmpty(cleaned))
            segments.Add(new AnsiSegment(cleaned));

        return segments;
    }

    /// <summary>
    /// Strips all ANSI escape sequences from text, returning plain text.
    /// </summary>
    public static string StripAnsi(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = AnsiSgrRegex().Replace(text, string.Empty);
        return AnsiOtherRegex().Replace(result, string.Empty);
    }

    /// <summary>
    /// Maps a 256-color index to a hex color string.
    /// 0-7: basic, 8-15: bright, 16-231: 6×6×6 color cube, 232-255: grayscale.
    /// </summary>
    private static string Get256Color(int index)
    {
        if (index < 0) index = 0;
        if (index > 255) index = 255;

        if (index < 8) return BasicColors[index];
        if (index < 16) return BrightColors[index - 8];

        if (index < 232)
        {
            // 6×6×6 color cube
            int adjusted = index - 16;
            int r = adjusted / 36;
            int g = (adjusted % 36) / 6;
            int b = adjusted % 6;
            return $"#{r * 51:X2}{g * 51:X2}{b * 51:X2}";
        }

        // Grayscale: 232-255 → 8 to 238 in steps of 10
        int gray = 8 + (index - 232) * 10;
        return $"#{gray:X2}{gray:X2}{gray:X2}";
    }
}
