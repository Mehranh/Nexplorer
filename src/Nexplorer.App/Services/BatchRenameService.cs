using System.Text.RegularExpressions;

namespace Nexplorer.App.Services;

/// <summary>Describes how a batch rename should transform file names.</summary>
public sealed record BatchRenameSpec
{
    public string FindPattern     { get; init; } = string.Empty;
    public string ReplaceWith     { get; init; } = string.Empty;
    public bool   UseRegex        { get; init; }
    public bool   CaseSensitive   { get; init; }

    // Counter
    public bool   AddCounter      { get; init; }
    public int    CounterStart    { get; init; } = 1;
    public int    CounterStep     { get; init; } = 1;
    public int    CounterPadding  { get; init; } = 1;  // e.g. 3 → "001"

    // Prefix / Suffix
    public string Prefix          { get; init; } = string.Empty;
    public string Suffix          { get; init; } = string.Empty;   // before extension

    // Case
    public CaseTransform CaseMode { get; init; } = CaseTransform.None;

    // Extension
    public string NewExtension    { get; init; } = string.Empty;   // empty = keep original
}

public enum CaseTransform { None, Lower, Upper, TitleCase }

public static class BatchRenameService
{
    /// <summary>
    /// Generates a preview list: (originalName, newName) for each input.
    /// Does NOT touch the filesystem.
    /// </summary>
    public static IReadOnlyList<(string Original, string New)> Preview(
        IReadOnlyList<string> fileNames, BatchRenameSpec spec)
    {
        var results = new List<(string, string)>(fileNames.Count);
        int counter = spec.CounterStart;

        for (int i = 0; i < fileNames.Count; i++)
        {
            var original = fileNames[i];
            var ext      = System.IO.Path.GetExtension(original);
            var baseName = System.IO.Path.GetFileNameWithoutExtension(original);

            // Find / Replace
            if (!string.IsNullOrEmpty(spec.FindPattern))
            {
                if (spec.UseRegex)
                {
                    var opts = spec.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                    try { baseName = Regex.Replace(baseName, spec.FindPattern, spec.ReplaceWith, opts); }
                    catch { /* invalid regex – leave as-is */ }
                }
                else
                {
                    var comp = spec.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    baseName = baseName.Replace(spec.FindPattern, spec.ReplaceWith, comp);
                }
            }

            // Prefix / Suffix
            baseName = spec.Prefix + baseName + spec.Suffix;

            // Counter
            if (spec.AddCounter)
            {
                var formatted = counter.ToString().PadLeft(spec.CounterPadding, '0');
                baseName += formatted;
                counter += spec.CounterStep;
            }

            // Case
            baseName = spec.CaseMode switch
            {
                CaseTransform.Lower     => baseName.ToLowerInvariant(),
                CaseTransform.Upper     => baseName.ToUpperInvariant(),
                CaseTransform.TitleCase => System.Globalization.CultureInfo.CurrentCulture
                                                .TextInfo.ToTitleCase(baseName.ToLower()),
                _ => baseName,
            };

            // Extension
            if (!string.IsNullOrEmpty(spec.NewExtension))
            {
                ext = spec.NewExtension.StartsWith('.') ? spec.NewExtension : "." + spec.NewExtension;
            }

            results.Add((original, baseName + ext));
        }

        return results;
    }
}
