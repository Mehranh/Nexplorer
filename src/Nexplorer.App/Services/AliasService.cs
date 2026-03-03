using System.IO;
using System.Text.Json;
using Nexplorer.App.ViewModels;

namespace Nexplorer.App.Services;

/// <summary>
/// Manages user-defined command aliases. Persisted to JSON.
/// </summary>
public sealed class AliasService
{
    private static readonly JsonSerializerOptions s_writeIndentedOptions = new() { WriteIndented = true };

    private static readonly string FilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "Nexplorer", "aliases.json");

    private readonly List<CommandAlias> _aliases = new();
    public IReadOnlyList<CommandAlias> Aliases => _aliases;

    public AliasService()
    {
        Load();
        EnsureDefaults();
    }

    /// <summary>
    /// Expands aliases in a command string. Only the first word is checked.
    /// </summary>
    public string ExpandAliases(string command, ShellKind shell)
    {
        if (string.IsNullOrWhiteSpace(command)) return command;

        var parts = command.Split(' ', 2);
        var cmd = parts[0].Trim();
        var rest = parts.Length > 1 ? " " + parts[1] : string.Empty;

        foreach (var alias in _aliases)
        {
            if (!string.Equals(alias.Name, cmd, StringComparison.OrdinalIgnoreCase))
                continue;
            if (alias.Shell is not null && alias.Shell != shell)
                continue;
            return alias.Expansion + rest;
        }

        return command;
    }

    public void AddAlias(string name, string expansion, ShellKind? shell = null)
    {
        _aliases.RemoveAll(a =>
            string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
            && a.Shell == shell);
        _aliases.Add(new CommandAlias(name, expansion, shell));
        Save();
    }

    public void RemoveAlias(string name)
    {
        _aliases.RemoveAll(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public IReadOnlyList<CommandAlias> GetAliases(ShellKind? shell = null)
    {
        return shell is null
            ? _aliases.ToList()
            : _aliases.Where(a => a.Shell is null || a.Shell == shell).ToList();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<List<CommandAlias>>(json);
            if (loaded is not null)
                _aliases.AddRange(loaded);
        }
        catch { /* best-effort */ }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(_aliases, s_writeIndentedOptions);
            File.WriteAllText(FilePath, json);
        }
        catch { /* best-effort */ }
    }

    private void EnsureDefaults()
    {
        if (_aliases.Count > 0) return;

        // Common aliases
        _aliases.Add(new CommandAlias("ll",     "Get-ChildItem -Force",   ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("la",     "Get-ChildItem -Force",   ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("which",  "Get-Command",            ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("touch",  "New-Item -ItemType File -Name", ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("grep",   "Select-String",          ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("head",   "Get-Content -Head",      ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("tail",   "Get-Content -Tail",      ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("open",   "Invoke-Item",            ShellKind.PowerShell));
        _aliases.Add(new CommandAlias("ll",     "dir /a",                 ShellKind.Cmd));
        _aliases.Add(new CommandAlias("cls",    "Clear-Host",             ShellKind.PowerShell));

        Save();
    }
}
