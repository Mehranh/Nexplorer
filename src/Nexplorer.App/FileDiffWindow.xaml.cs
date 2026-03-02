using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Nexplorer.App.Services;

namespace Nexplorer.App;

public partial class FileDiffWindow : Window
{
    public FileDiffWindow(string leftPath, string rightPath)
    {
        InitializeComponent();

        var leftName  = Path.GetFileName(leftPath);
        var rightName = Path.GetFileName(rightPath);
        HeaderLabel.Text = $"{leftName}  ↔  {rightName}";
        LeftLabel.Text   = leftPath;
        RightLabel.Text  = rightPath;

        var leftText  = File.ReadAllText(leftPath);
        var rightText = File.ReadAllText(rightPath);

        var (oldLines, newLines) = DiffService.ComputeSideBySide(leftText, rightText);

        int added   = newLines.Count(l => l.Kind == DiffLineKind.Added);
        int removed = oldLines.Count(l => l.Kind == DiffLineKind.Removed);
        StatsLabel.Text = $"+{added}  −{removed}  |  {oldLines.Count} lines";

        LeftDiff.DiffLines  = new ObservableCollection<DiffLine>(oldLines);
        RightDiff.DiffLines = new ObservableCollection<DiffLine>(newLines);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
