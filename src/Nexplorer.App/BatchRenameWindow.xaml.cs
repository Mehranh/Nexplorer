using System.IO;
using System.Windows;
using System.Windows.Controls;
using Nexplorer.App.Services;

namespace Nexplorer.App;

public partial class BatchRenameWindow : Window
{
    private readonly IReadOnlyList<string> _filePaths;
    private readonly IReadOnlyList<string> _fileNames;
    private readonly string _directory;

    /// <summary>True if the user pressed Rename and the operation succeeded.</summary>
    public bool Renamed { get; private set; }

    public BatchRenameWindow(IReadOnlyList<string> filePaths)
    {
        InitializeComponent();
        _filePaths = filePaths;
        _fileNames = filePaths.Select(Path.GetFileName).ToList()!;
        _directory = Path.GetDirectoryName(filePaths[0]) ?? string.Empty;
        RefreshPreview();
    }

    private BatchRenameSpec BuildSpec() => new()
    {
        FindPattern    = FindBox.Text,
        ReplaceWith    = ReplaceBox.Text,
        UseRegex       = RegexCheck.IsChecked == true,
        CaseSensitive  = CaseCheck.IsChecked == true,
        AddCounter     = CounterCheck.IsChecked == true,
        CounterStart   = int.TryParse(CounterStartBox.Text, out var cs) ? cs : 1,
        CounterStep    = int.TryParse(CounterStepBox.Text, out var cst) ? cst : 1,
        CounterPadding = int.TryParse(CounterPadBox.Text, out var cp) ? cp : 1,
        Prefix         = PrefixBox.Text,
        Suffix         = SuffixBox.Text,
        CaseMode       = CaseCombo.SelectedIndex switch
        {
            1 => CaseTransform.Lower,
            2 => CaseTransform.Upper,
            3 => CaseTransform.TitleCase,
            _ => CaseTransform.None,
        },
        NewExtension = ExtBox.Text,
    };

    private void RefreshPreview()
    {
        var spec    = BuildSpec();
        var preview = BatchRenameService.Preview(_fileNames, spec);
        PreviewList.ItemsSource = preview;

        var changed = preview.Count(p => p.Original != p.New);
        StatusLabel.Text = $"{changed} of {preview.Count} will change";
    }

    private void OnSpecChanged(object sender, RoutedEventArgs e) => RefreshPreview();
    private void OnSpecChanged(object sender, SelectionChangedEventArgs e) => RefreshPreview();
    private void OnSpecChanged(object sender, TextChangedEventArgs e) => RefreshPreview();

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        var spec    = BuildSpec();
        var preview = BatchRenameService.Preview(_fileNames, spec);
        int errors  = 0;

        for (int i = 0; i < preview.Count; i++)
        {
            var (original, newName) = preview[i];
            if (original == newName) continue;

            try
            {
                FileOperationService.Rename(_filePaths[i], newName);
            }
            catch { errors++; }
        }

        Renamed = true;
        if (errors > 0)
            NotificationService.Instance.Warn($"{errors} file(s) could not be renamed.", "Batch Rename");

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
