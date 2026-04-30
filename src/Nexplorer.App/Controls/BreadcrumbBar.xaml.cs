using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nexplorer.App.Controls;

/// <summary>
/// Breadcrumb / editable address bar.
/// </summary>
/// <remarks>
/// Behaviour:
/// <list type="bullet">
///   <item>Default state: clickable segments, each navigates to its cumulative path.</item>
///   <item>Click empty space, the pencil button, or press <c>Ctrl+L</c> → enters edit mode (TextBox).</item>
///   <item>Edit mode: <c>Enter</c> commits the typed path via <see cref="NavigateCommand"/>;
///         <c>Esc</c> or losing focus reverts to breadcrumb mode.</item>
/// </list>
/// </remarks>
public partial class BreadcrumbBar : UserControl
{
    public BreadcrumbBar()
    {
        InitializeComponent();
        SegmentsItems.ItemsSource = Segments;

        // Ctrl+L focuses the address bar in edit mode (Win11 / browser convention).
        InputBindings.Add(new KeyBinding(
            new RelayCommand(_ => EnterEditMode()),
            new KeyGesture(Key.L, ModifierKeys.Control)));
    }

    /// <summary>The full path being displayed.</summary>
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path), typeof(string), typeof(BreadcrumbBar),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPathChanged));

    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    /// <summary>Command invoked with the target path string when the user navigates.</summary>
    public static readonly DependencyProperty NavigateCommandProperty = DependencyProperty.Register(
        nameof(NavigateCommand), typeof(ICommand), typeof(BreadcrumbBar));

    public ICommand? NavigateCommand
    {
        get => (ICommand?)GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    public ObservableCollection<BreadcrumbSegment> Segments { get; } = new();

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((BreadcrumbBar)d).RebuildSegments();

    private void RebuildSegments()
    {
        Segments.Clear();
        var path = Path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return;

        // Split on either separator; works for "C:\Users\me" and rare forward-slash input.
        var parts = path.Split(
            new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
            System.StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        var sep = System.IO.Path.DirectorySeparatorChar;

        // First segment: a drive ("C:") needs the trailing slash so Directory.Exists("C:\")
        // works — Directory.Exists("C:") would resolve to the *current* dir on that drive.
        var head = parts[0];
        var current = head.EndsWith(':') ? head + sep : head;
        Segments.Add(new BreadcrumbSegment(head, current, ShowSeparator: parts.Length > 1));

        for (int i = 1; i < parts.Length; i++)
        {
            current = System.IO.Path.Combine(current, parts[i]);
            Segments.Add(new BreadcrumbSegment(
                Display: parts[i],
                FullPath: current,
                ShowSeparator: i < parts.Length - 1));
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────

    private void Segment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is BreadcrumbSegment seg)
            Navigate(seg.FullPath);
    }

    private void Host_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only enter edit mode when the click landed on the host border itself —
        // not on a child segment button (those have their own click handler).
        if (ReferenceEquals(e.OriginalSource, BreadcrumbHost) ||
            e.OriginalSource is DockPanel)
        {
            EnterEditMode();
        }
    }

    private void EditButton_Click(object sender, RoutedEventArgs e) => EnterEditMode();

    private void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                var typed = PathBox.Text?.Trim() ?? string.Empty;
                ExitEditMode();
                Navigate(typed);
                break;
            case Key.Escape:
                e.Handled = true;
                PathBox.Text = Path; // discard edits
                ExitEditMode();
                break;
        }
    }

    private void PathBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => ExitEditMode();

    // ── Mode switching ────────────────────────────────────────────────────

    private void EnterEditMode()
    {
        PathBox.Text = Path ?? string.Empty;
        BreadcrumbHost.Visibility = Visibility.Collapsed;
        PathBox.Visibility = Visibility.Visible;
        PathBox.Focus();
        PathBox.SelectAll();
    }

    private void ExitEditMode()
    {
        PathBox.Visibility = Visibility.Collapsed;
        BreadcrumbHost.Visibility = Visibility.Visible;
    }

    private void Navigate(string path)
    {
        if (NavigateCommand?.CanExecute(path) == true)
            NavigateCommand.Execute(path);
    }

    // ── Tiny ICommand for the Ctrl+L key binding (avoids pulling in MVVM toolkit here) ──
    private sealed class RelayCommand : ICommand
    {
        private readonly System.Action<object?> _exec;
        public RelayCommand(System.Action<object?> exec) => _exec = exec;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _exec(parameter);
        public event System.EventHandler? CanExecuteChanged { add { } remove { } }
    }
}

/// <summary>One clickable segment in the breadcrumb. Immutable.</summary>
public sealed record BreadcrumbSegment(string Display, string FullPath, bool ShowSeparator)
{
    /// <summary>Used by screen readers — full path is more useful than the leaf name.</summary>
    public string AutomationName => $"Navigate to {FullPath}";
}
