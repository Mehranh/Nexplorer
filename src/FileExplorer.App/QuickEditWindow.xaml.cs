using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using ICSharpCode.AvalonEdit.Highlighting;

namespace FileExplorer.App;

public partial class QuickEditWindow : Window
{
    private readonly string _filePath;
    private readonly ICSharpCode.AvalonEdit.TextEditor Editor;
    private Encoding _encoding = Encoding.UTF8;
    private bool _isModified;
    private bool _closing;

    public QuickEditWindow(string filePath)
    {
        InitializeComponent();
        _filePath = filePath;
        FileNameLabel.Text = Path.GetFileName(filePath);
        ToolTip = filePath;

        Editor = new ICSharpCode.AvalonEdit.TextEditor
        {
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Code, Consolas, Courier New"),
            FontSize = 13,
            ShowLineNumbers = true,
            WordWrap = false,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Padding = new Thickness(4),
        };
        Editor.SetResourceReference(BackgroundProperty, "PaneBg");
        Editor.SetResourceReference(ForegroundProperty, "TextFg");
        Editor.SetResourceReference(ICSharpCode.AvalonEdit.TextEditor.LineNumbersForegroundProperty, "SubTextFg");
        EditorHost.Content = Editor;

        ApplySyntaxHighlighting();
        LoadFile();

        Editor.TextChanged += (_, _) =>
        {
            if (!_isModified)
            {
                _isModified = true;
                ModifiedIndicator.Visibility = Visibility.Visible;
            }
        };

        Editor.TextArea.Caret.PositionChanged += (_, _) => UpdateLineInfo();
    }

    private void LoadFile()
    {
        using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        Editor.Text = reader.ReadToEnd();
        _encoding = reader.CurrentEncoding;
        EncodingLabel.Text = _encoding.EncodingName;
        _isModified = false;
        ModifiedIndicator.Visibility = Visibility.Collapsed;
        UpdateLineInfo();
    }

    private void ApplySyntaxHighlighting()
    {
        var ext = Path.GetExtension(_filePath);
        var highlighting = HighlightingManager.Instance.GetDefinitionByExtension(ext);
        Editor.SyntaxHighlighting = highlighting;
    }

    private void UpdateLineInfo()
    {
        var caret = Editor.TextArea.Caret;
        LineInfoLabel.Text = $"Ln {caret.Line}, Col {caret.Column}";
    }

    private void Save()
    {
        File.WriteAllText(_filePath, Editor.Text, _encoding);
        _isModified = false;
        ModifiedIndicator.Visibility = Visibility.Collapsed;
    }

    // ─── Event handlers ───────────────────────────────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        { Save(); e.Handled = true; return; }

        if (e.Key == Key.Escape)
        { TryClose(); e.Handled = true; return; }

        base.OnKeyDown(e);
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void Reload_Click(object sender, RoutedEventArgs e) => LoadFile();

    private void Close_Click(object sender, RoutedEventArgs e) => TryClose();

    private void TryClose()
    {
        if (_isModified)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Save before closing?",
                "Quick Edit", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) Save();
            else if (result == MessageBoxResult.Cancel) return;
        }
        _closing = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_closing && _isModified)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Save before closing?",
                "Quick Edit", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) Save();
            else if (result == MessageBoxResult.Cancel) { e.Cancel = true; return; }
        }
        base.OnClosing(e);
    }

    private void WordWrapCheck_Changed(object sender, RoutedEventArgs e)
    { if (Editor is not null) Editor.WordWrap = WordWrapCheck.IsChecked == true; }

    private void ShowLineNumbers_Changed(object sender, RoutedEventArgs e)
    { if (Editor is not null) Editor.ShowLineNumbers = ShowLineNumbers.IsChecked == true; }
}
