using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FileExplorer.App.Services.Settings;
using FileExplorer.App.ViewModels;

namespace FileExplorer.App;

public partial class MainWindow : Window
{

    // ── Dark title bar via DWM ──────────────────────────────────────────
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private void ApplyDarkTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;

        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // Paint the caption background to match the app
        int captionColor = 0x001C1C1C; // BGR format: #1C1C1C
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        // Apply dark title bar once the window handle is available
        SourceInitialized += (_, _) => ApplyDarkTitleBar();

        // Subscribe to terminal panel output changes for auto-scroll
        if (Vm.TerminalPanel.ActiveTab is not null)
        {
            Vm.TerminalPanel.ActiveTab.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TerminalTabViewModel.OutputText))
                    TerminalOutput.ScrollToEnd();
            };
        }

        Vm.TerminalPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TerminalPanelViewModel.ActiveTab) && Vm.TerminalPanel.ActiveTab is not null)
            {
                Vm.TerminalPanel.ActiveTab.PropertyChanged += (_, e2) =>
                {
                    if (e2.PropertyName == nameof(TerminalTabViewModel.OutputText))
                        TerminalOutput.ScrollToEnd();
                };
                // Re-broadcast changes so the old bindings still work
                Vm.OnPropertyChanged(nameof(MainViewModel.OutputText));
                Vm.OnPropertyChanged(nameof(MainViewModel.CommandText));
                Vm.OnPropertyChanged(nameof(MainViewModel.Shell));
                Vm.OnPropertyChanged(nameof(MainViewModel.TerminalPrompt));
                Vm.OnPropertyChanged(nameof(MainViewModel.Suggestions));
            }
        };

        SubscribePaneEvents(Vm.LeftPane, LeftList);
        SubscribePaneEvents(Vm.RightPane, RightList);

        Vm.LeftTabs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneTabsViewModel.ActiveTab))
                SubscribePaneEvents(Vm.LeftPane, LeftList);
        };
        Vm.RightTabs.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneTabsViewModel.ActiveTab))
                SubscribePaneEvents(Vm.RightPane, RightList);
        };

        // Subscribe to batch rename / search events
        Vm.BatchRenameRequested += (_, _2) => OpenBatchRename();
        Vm.SearchRequested      += (_, _2) => OpenSearch();

        // Wire up preview pane updates
        Vm.LeftPane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneViewModel.SelectedItem) && Vm.LeftPane.IsPreviewVisible)
                UpdatePreview(Vm.LeftPane.SelectedItem, LeftPreviewPanel);
        };
        Vm.RightPane.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PaneViewModel.SelectedItem) && Vm.RightPane.IsPreviewVisible)
                UpdatePreview(Vm.RightPane.SelectedItem, RightPreviewPanel);
        };
    }

    private MainViewModel Vm => (MainViewModel)DataContext;

    private void SubscribePaneEvents(PaneViewModel pane, ListView list)
    {
        pane.SelectAllRequested -= (_, _2) => list.SelectAll();
        pane.SelectAllRequested += (_, _2) => list.SelectAll();

        pane.InvertSelectionRequested -= InvertListSelection;
        pane.InvertSelectionRequested += InvertListSelection;

        void InvertListSelection(object? s, EventArgs _3)
        {
            foreach (var item in list.Items)
            {
                if (list.SelectedItems.Contains(item))
                    list.SelectedItems.Remove(item);
                else
                    list.SelectedItems.Add(item);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PANE FOCUS
    // ═══════════════════════════════════════════════════════════════════════

    private void LeftList_GotFocus(object sender, RoutedEventArgs e)
    {
        Vm.ActivateLeftPaneCommand.Execute(null);
        EnsureListItemFocused(LeftList);
    }
    private void RightList_GotFocus(object sender, RoutedEventArgs e)
    {
        Vm.ActivateRightPaneCommand.Execute(null);
        EnsureListItemFocused(RightList);
    }

    /// <summary>
    /// If no item is selected, selects the first item so Up/Down keys work immediately.
    /// </summary>
    private static void EnsureListItemFocused(ListView list)
    {
        if (list.SelectedItem is null && list.Items.Count > 0)
            list.SelectedIndex = 0;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  DOUBLE-CLICK
    // ═══════════════════════════════════════════════════════════════════════

    private void LeftList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => Vm.LeftPane.OpenSelectedCommand.Execute(null);
    private void RightList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => Vm.RightPane.OpenSelectedCommand.Execute(null);
    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is CommandHistoryEntry entry)
            Vm.LoadHistoryEntryCommand.Execute(entry);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SELECTION SYNC
    // ═══════════════════════════════════════════════════════════════════════

    private void LeftList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Vm.LeftPane.SelectedItems = LeftList.SelectedItems.Cast<FileItemViewModel>().ToList();
        if (Vm.LeftPane.IsPreviewVisible)
            UpdatePreview(Vm.LeftPane.SelectedItem, LeftPreviewPanel);
    }
    private void RightList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Vm.RightPane.SelectedItems = RightList.SelectedItems.Cast<FileItemViewModel>().ToList();
        if (Vm.RightPane.IsPreviewVisible)
            UpdatePreview(Vm.RightPane.SelectedItem, RightPreviewPanel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  CONTEXT MENU
    // ═══════════════════════════════════════════════════════════════════════

    private void LeftList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        => Vm.ActivateLeftPaneCommand.Execute(null);
    private void RightList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        => Vm.ActivateRightPaneCommand.Execute(null);
    private void CopyToOtherPane_Click(object sender, RoutedEventArgs e)
        => _ = Vm.CopyToOtherPaneCommand.ExecuteAsync(null);
    private void MoveToOtherPane_Click(object sender, RoutedEventArgs e)
        => _ = Vm.MoveToOtherPaneCommand.ExecuteAsync(null);

    // ═══════════════════════════════════════════════════════════════════════
    //  VIEW MODE
    // ═══════════════════════════════════════════════════════════════════════

    private void LeftViewModeButton_Click(object sender, RoutedEventArgs e)
        => ShowViewModeMenu((Button)sender, Vm.LeftPane);

    private void RightViewModeButton_Click(object sender, RoutedEventArgs e)
        => ShowViewModeMenu((Button)sender, Vm.RightPane);

    private void ShowViewModeMenu(Button button, PaneViewModel pane)
    {
        var menu = (ContextMenu)FindResource("ViewModeMenu");
        menu.DataContext = pane;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ViewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string mode)
        {
            var menu = mi.Parent as ContextMenu;
            if (menu?.DataContext is PaneViewModel pane)
                pane.SetViewModeCommand.Execute(mode);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  ICON LIST SELECTION SYNC
    // ═══════════════════════════════════════════════════════════════════════

    private void LeftIconList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Vm.LeftPane.SelectedItems = LeftIconList.SelectedItems.Cast<FileItemViewModel>().ToList();
        if (Vm.LeftPane.IsPreviewVisible)
            UpdatePreview(Vm.LeftPane.SelectedItem, LeftPreviewPanel);
    }

    private void RightIconList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Vm.RightPane.SelectedItems = RightIconList.SelectedItems.Cast<FileItemViewModel>().ToList();
        if (Vm.RightPane.IsPreviewVisible)
            UpdatePreview(Vm.RightPane.SelectedItem, RightPreviewPanel);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FAVORITES
    // ═══════════════════════════════════════════════════════════════════════

    private void AddToFavorites_Click(object sender, RoutedEventArgs e)
        => Vm.AddToFavoritesCommand.Execute(null);

    // ═══════════════════════════════════════════════════════════════════════
    //  GIT HISTORY
    // ═══════════════════════════════════════════════════════════════════════

    private void GitHistory_Click(object sender, RoutedEventArgs e)
        => _ = Vm.ShowGitHistoryCommand.ExecuteAsync(null);

    private void BranchItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item && item.DataContext is string branch)
            Vm.GitHistory.SelectBranchCommand.Execute(branch);
    }

    private void BottomTab_Terminal_Click(object sender, MouseButtonEventArgs e)
        => Vm.SwitchBottomTabCommand.Execute("Terminal");

    private void BottomTab_GitHistory_Click(object sender, MouseButtonEventArgs e)
        => Vm.SwitchBottomTabCommand.Execute("GitHistory");

    private void GitHistoryChangedFile_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListView lv && lv.SelectedItem is Services.GitChangedFile file)
            Vm.GitHistory.ShowFileDiffCommand.Execute(file);
    }

    private void BottomTab_Git_Click(object sender, MouseButtonEventArgs e)
        => Vm.SwitchBottomTabCommand.Execute("Git");

    private void GitFile_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is Services.GitStatusEntry entry)
            Vm.GitTab.SelectedFile = entry;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  BATCH RENAME
    // ═══════════════════════════════════════════════════════════════════════

    private void BatchRename_Click(object sender, RoutedEventArgs e) => OpenBatchRename();

    private void OpenBatchRename()
    {
        var paths = Vm.ActivePane.SelectedItems.Select(i => i.FullPath).ToList();
        if (paths.Count == 0 && Vm.ActivePane.SelectedItem is not null)
            paths.Add(Vm.ActivePane.SelectedItem.FullPath);
        if (paths.Count == 0) return;

        var dlg = new BatchRenameWindow(paths) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Renamed)
            _ = Vm.ActivePane.GoToAsync(Vm.ActivePane.CurrentPath, pushHistory: false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  QUICK EDIT
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly HashSet<string> s_quickEditExts = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".yaml", ".yml", ".xml", ".toml",
        ".env", ".ini", ".cfg", ".conf", ".config", ".properties",
        ".csv", ".tsv", ".log",
        ".cs", ".csx", ".csproj", ".sln", ".props", ".targets",
        ".js", ".ts", ".jsx", ".tsx", ".mjs", ".cjs",
        ".py", ".rb", ".rs", ".go", ".java", ".kt", ".swift",
        ".html", ".htm", ".css", ".scss", ".less",
        ".sh", ".bash", ".ps1", ".psm1", ".bat", ".cmd",
        ".sql", ".graphql",
        ".dockerfile", ".gitignore", ".gitattributes", ".editorconfig",
    };

    private void QuickEdit_Click(object sender, RoutedEventArgs e) => OpenQuickEdit();

    private void DiffFiles_Click(object sender, RoutedEventArgs e) => OpenFileDiff();

    private void OpenQuickEdit()
    {
        var selected = Vm.ActivePane.SelectedItem;
        if (selected is null || selected.IsDirectory) return;

        var ext = Path.GetExtension(selected.FullPath);
        var name = Path.GetFileName(selected.FullPath);

        // Allow extensionless dotfiles like .gitignore, .editorconfig, Dockerfile
        bool isEditable = s_quickEditExts.Contains(ext)
                       || s_quickEditExts.Contains("." + name.ToLowerInvariant())
                       || string.IsNullOrEmpty(ext);

        if (!isEditable)
        {
            MessageBox.Show($"Quick Edit does not support '{ext}' files.",
                "Quick Edit", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var info = new FileInfo(selected.FullPath);
        if (info.Length > 5 * 1024 * 1024) // 5 MB limit
        {
            MessageBox.Show("File is too large for Quick Edit (max 5 MB).",
                "Quick Edit", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var dlg = new QuickEditWindow(selected.FullPath) { Owner = this };
            dlg.Show();
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(Path.GetTempPath(), "quickedit_error.log");
            File.WriteAllText(logPath, ex.ToString());
            MessageBox.Show(ex.ToString(), "Quick Edit Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FILE DIFF
    // ═══════════════════════════════════════════════════════════════════════

    private void OpenFileDiff()
    {
        var leftSelected  = Vm.LeftPane.SelectedItem;
        var rightSelected = Vm.RightPane.SelectedItem;

        if (leftSelected is null || rightSelected is null)
        {
            MessageBox.Show("Select one file in the left pane and one in the right pane to compare.",
                "File Diff", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (leftSelected.IsDirectory || rightSelected.IsDirectory)
        {
            MessageBox.Show("File Diff only works with files, not directories.",
                "File Diff", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var leftExt  = Path.GetExtension(leftSelected.FullPath);
        var rightExt = Path.GetExtension(rightSelected.FullPath);
        if (!s_quickEditExts.Contains(leftExt) && !string.IsNullOrEmpty(leftExt))
        {
            MessageBox.Show($"File Diff does not support '{leftExt}' files.",
                "File Diff", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!s_quickEditExts.Contains(rightExt) && !string.IsNullOrEmpty(rightExt))
        {
            MessageBox.Show($"File Diff does not support '{rightExt}' files.",
                "File Diff", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        const long maxSize = 5 * 1024 * 1024;
        if (new FileInfo(leftSelected.FullPath).Length > maxSize || new FileInfo(rightSelected.FullPath).Length > maxSize)
        {
            MessageBox.Show("One of the files is too large for diff (max 5 MB each).",
                "File Diff", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var dlg = new FileDiffWindow(leftSelected.FullPath, rightSelected.FullPath) { Owner = this };
            dlg.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString(), "File Diff Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SEARCH
    // ═══════════════════════════════════════════════════════════════════════

    private void OpenSearch()
    {
        var dlg = new SearchWindow(Vm.ActivePane.CurrentPath) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedPath is not null)
            _ = Vm.ActivePane.GoToAsync(dlg.SelectedPath, pushHistory: true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SETTINGS
    // ═══════════════════════════════════════════════════════════════════════

    private void OpenSettings()
    {
        var dlg = new SettingsWindow(App.SettingsService) { Owner = this };
        dlg.ShowDialog();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => OpenSettings();

    // ═══════════════════════════════════════════════════════════════════════
    //  PREVIEW PANE
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly HashSet<string> s_imageExts = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".webp", ".tif", ".tiff" };

    private static readonly HashSet<string> s_textExts = new(StringComparer.OrdinalIgnoreCase)
        { ".txt",".md",".json",".xml",".csv",".log",".cs",".js",".ts",".py",
          ".html",".css",".yaml",".yml",".toml",".ini",".cfg",".bat",".ps1",
          ".sh",".sql",".xaml",".csproj",".sln",".gitignore",".editorconfig" };

    private void UpdatePreview(FileItemViewModel? item, StackPanel panel)
    {
        panel.Children.Clear();

        if (item is null)
        {
            panel.Children.Add(new TextBlock { Text = "No selection", Foreground = Brushes.Gray, FontStyle = FontStyles.Italic });
            return;
        }

        // File name
        panel.Children.Add(new TextBlock
        {
            Text       = item.Name,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
            TextWrapping = TextWrapping.Wrap,
            Margin     = new Thickness(0, 0, 0, 6),
        });

        // Type / Size / Modified
        panel.Children.Add(MakeInfoLine("Type", item.TypeDisplay));
        panel.Children.Add(MakeInfoLine("Size", item.SizeDisplay));
        panel.Children.Add(MakeInfoLine("Modified", item.LastWriteTimeLocal.ToString("yyyy-MM-dd HH:mm")));

        // If it's a directory, show item count
        if (item.IsDirectory)
        {
            try
            {
                var count = Directory.GetFileSystemEntries(item.FullPath).Length;
                panel.Children.Add(MakeInfoLine("Contains", $"{count} items"));
            }
            catch { }
            return;
        }

        var ext = Path.GetExtension(item.FullPath);

        // Image preview
        if (s_imageExts.Contains(ext))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource         = new Uri(item.FullPath);
                bi.DecodePixelWidth  = 180;
                bi.CacheOption       = BitmapCacheOption.OnLoad;
                bi.EndInit();
                bi.Freeze();

                panel.Children.Add(new Image
                {
                    Source  = bi,
                    MaxWidth = 180,
                    Margin   = new Thickness(0, 8, 0, 4),
                    Stretch  = Stretch.Uniform,
                });

                panel.Children.Add(MakeInfoLine("Dimensions", $"{bi.PixelWidth} × {bi.PixelHeight}"));
            }
            catch { panel.Children.Add(new TextBlock { Text = "Cannot load image", Foreground = Brushes.Gray }); }
            return;
        }

        // Text preview  
        if (s_textExts.Contains(ext))
        {
            try
            {
                using var sr = new StreamReader(item.FullPath);
                var text = new char[4096];
                var read = sr.Read(text, 0, text.Length);
                var preview = new string(text, 0, read);
                if (sr.Peek() >= 0) preview += "\n…";

                panel.Children.Add(new TextBox
                {
                    Text             = preview,
                    IsReadOnly       = true,
                    Background       = Brushes.Transparent,
                    Foreground       = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                    FontFamily       = new FontFamily("Cascadia Code,Consolas,Courier New"),
                    FontSize         = 10.5,
                    TextWrapping     = TextWrapping.Wrap,
                    BorderThickness  = new Thickness(0),
                    MaxHeight        = 300,
                    Margin           = new Thickness(0, 8, 0, 0),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                });
            }
            catch { }
            return;
        }

        // Hex preview for binary files
        try
        {
            using var fs = File.OpenRead(item.FullPath);
            var buffer   = new byte[256];
            var read     = fs.Read(buffer, 0, buffer.Length);

            var hex = new System.Text.StringBuilder();
            for (int i = 0; i < read; i++)
            {
                hex.Append(buffer[i].ToString("X2"));
                hex.Append(i % 16 == 15 ? '\n' : ' ');
            }

            panel.Children.Add(new TextBlock
            {
                Text = "Hex Preview",
                Foreground = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85)),
                FontWeight = FontWeights.SemiBold,
                FontSize   = 10,
                Margin     = new Thickness(0, 8, 0, 4),
            });

            panel.Children.Add(new TextBox
            {
                Text            = hex.ToString(),
                IsReadOnly      = true,
                Background      = Brushes.Transparent,
                Foreground      = new SolidColorBrush(Color.FromRgb(0x80, 0xC0, 0x80)),
                FontFamily      = new FontFamily("Cascadia Code,Consolas,Courier New"),
                FontSize        = 9,
                TextWrapping    = TextWrapping.Wrap,
                BorderThickness = new Thickness(0),
                MaxHeight       = 200,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            });
        }
        catch { }
    }

    private static TextBlock MakeInfoLine(string label, string value) => new()
    {
        Foreground  = new SolidColorBrush(Color.FromRgb(0x85, 0x85, 0x85)),
        FontSize    = 10.5,
        Margin      = new Thickness(0, 1, 0, 1),
        Inlines     =
        {
            new System.Windows.Documents.Run(label + ": ") { FontWeight = FontWeights.SemiBold },
            new System.Windows.Documents.Run(value),
        },
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  COLUMN HEADER SORTING
    // ═══════════════════════════════════════════════════════════════════════

    private void PaneList_ColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not GridViewColumnHeader header
            || header.Column is null
            || header.Role == GridViewColumnHeaderRole.Padding) return;

        var rawHeader = header.Column.Header as string;
        var pane = sender == LeftList ? Vm.LeftPane : Vm.RightPane;

        var sortProp = rawHeader?.Replace(" ▲", "").Replace(" ▼", "").Trim() switch
        {
            "Name"     => nameof(FileItemViewModel.Name),
            "Type"     => nameof(FileItemViewModel.TypeDisplay),
            "Size"     => nameof(FileItemViewModel.SizeBytes),
            "Modified" => nameof(FileItemViewModel.LastWriteTimeLocal),
            _          => null
        };
        if (sortProp is null) return;

        pane.SortByCommand.Execute(sortProp);

        if (sender is ListView lv && lv.View is GridView gv)
        {
            foreach (var col in gv.Columns)
                if (col.Header is string h)
                    col.Header = h.Replace(" ▲", "").Replace(" ▼", "").Trim();

            var arrow = pane.SortDescending ? " ▼" : " ▲";
            header.Column.Header = rawHeader?.Replace(" ▲", "").Replace(" ▼", "").Trim() + arrow;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  FOLDER TREE
    // ═══════════════════════════════════════════════════════════════════════

    private void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is FolderTreeNodeViewModel node && node.FullPath is not null)
            _ = Vm.ActivePane.GoToAsync(node.FullPath, pushHistory: true);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  INLINE RENAME
    // ═══════════════════════════════════════════════════════════════════════

    private void RenameBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox tb && (bool)e.NewValue && tb.DataContext is FileItemViewModel vm && vm.IsEditing)
        {
            tb.Dispatcher.BeginInvoke(() =>
            {
                tb.Focus();
                if (!vm.IsDirectory && vm.EditingName.Contains('.'))
                {
                    var dot = vm.EditingName.LastIndexOf('.');
                    if (dot > 0) { tb.Select(0, dot); return; }
                }
                tb.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void RenameBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || tb.DataContext is not FileItemViewModel item) return;
        if (e.Key == Key.Return) { FindOwnerPane(item)?.CommitRename(item); e.Handled = true; }
        else if (e.Key == Key.Escape) { item.CancelRename(); e.Handled = true; }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.DataContext is FileItemViewModel item && item.IsEditing)
            FindOwnerPane(item)?.CommitRename(item);
    }

    private PaneViewModel? FindOwnerPane(FileItemViewModel item)
    {
        if (Vm.LeftPane.Items.Contains(item))  return Vm.LeftPane;
        if (Vm.RightPane.Items.Contains(item)) return Vm.RightPane;
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TAB STRIP
    // ═══════════════════════════════════════════════════════════════════════

    private void TabItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PaneTabViewModel tab)
        {
            GetTabsOwnerOf(tab)?.SelectTabCommand.Execute(tab);
            e.Handled = true;
        }
    }
    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PaneTabViewModel tab)
        {
            GetTabsOwnerOf(tab)?.CloseTabCommand.Execute(tab);
            e.Handled = true;
        }
    }
    private PaneTabsViewModel? GetTabsOwnerOf(PaneTabViewModel tab)
    {
        if (Vm.LeftTabs.Tabs.Contains(tab))  return Vm.LeftTabs;
        if (Vm.RightTabs.Tabs.Contains(tab)) return Vm.RightTabs;
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  F-KEY TOOLBAR
    // ═══════════════════════════════════════════════════════════════════════

    private void FKey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;
        var ap = Vm.ActivePane;

        switch (tag)
        {
            case "F2":
            case "Rename":
                ap.BeginRenameCommand.Execute(null); break;
            case "F3":
                ap.TogglePreviewCommand.Execute(null); break;
            case "F4":
                CommandInput.Focus(); CommandInput.SelectAll(); break;
            case "F5":
                _ = Vm.CopyToOtherPaneCommand.ExecuteAsync(null); break;
            case "F6":
                _ = Vm.MoveToOtherPaneCommand.ExecuteAsync(null); break;
            case "F7":
                _ = ap.NewFolderCommand.ExecuteAsync(null); break;
            case "F8":
                ap.TogglePreviewCommand.Execute(null); break;
            case "F9":
                OpenSearch(); break;
            case "Compare":
                Vm.CompareDirectoriesCommand.Execute(null); break;
            case "BatchRename":
                OpenBatchRename(); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  SMART TERMINAL INPUT
    // ═══════════════════════════════════════════════════════════════════════

    private void CommandInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = Vm;
        switch (e.Key)
        {
            case Key.Up:
                if (vm.ShowSuggestions && vm.Suggestions.Count > 0)
                {
                    var next = vm.SelectedSuggestionIndex - 1;
                    vm.SelectedSuggestionIndex = next < -1 ? -1 : next;
                }
                else { vm.NavigateHistoryUp(); CommandInput.CaretIndex = CommandInput.Text.Length; }
                e.Handled = true; break;

            case Key.Down:
                if (vm.ShowSuggestions && vm.Suggestions.Count > 0)
                {
                    var next = vm.SelectedSuggestionIndex + 1;
                    if (next >= vm.Suggestions.Count) next = vm.Suggestions.Count - 1;
                    vm.SelectedSuggestionIndex = next;
                }
                else { vm.NavigateHistoryDown(); CommandInput.CaretIndex = CommandInput.Text.Length; }
                e.Handled = true; break;

            case Key.Enter when vm.ShowSuggestions && vm.SelectedSuggestionIndex >= 0:
                vm.AcceptSuggestion(); CommandInput.CaretIndex = CommandInput.Text.Length;
                e.Handled = true; break;

            case Key.Tab:
                vm.HandleTabCompletion(); CommandInput.CaretIndex = CommandInput.Text.Length;
                e.Handled = true; break;

            case Key.Right when CommandInput.CaretIndex == CommandInput.Text.Length:
            case Key.End   when CommandInput.CaretIndex == CommandInput.Text.Length:
                if (!string.IsNullOrEmpty(vm.InlineSuggestion) && vm.InlineSuggestion != vm.CommandText
                    && vm.InlineSuggestion.StartsWith(vm.CommandText, StringComparison.OrdinalIgnoreCase))
                { vm.AcceptSuggestion(); CommandInput.CaretIndex = CommandInput.Text.Length; e.Handled = true; }
                break;

            case Key.Escape when vm.ShowSuggestions:
                vm.DismissSuggestions(); e.Handled = true; break;

            // Ctrl+R: Reverse history search
            case Key.R when Keyboard.Modifiers == ModifierKeys.Control:
                if (vm.TerminalPanel.ActiveTab is not null)
                {
                    vm.TerminalPanel.ActiveTab.ToggleHistorySearchCommand.Execute(null);
                    if (vm.TerminalPanel.ActiveTab.IsHistorySearchActive)
                    {
                        HistorySearchBox.Focus();
                        HistorySearchBox.SelectAll();
                    }
                }
                e.Handled = true; break;
        }
    }

    private void SuggestionList_Click(object sender, MouseButtonEventArgs e)
    {
        if (Vm.SelectedSuggestionIndex >= 0)
        {
            Vm.AcceptSuggestion();
            CommandInput.Focus();
            CommandInput.CaretIndex = CommandInput.Text.Length;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  TERMINAL TABS
    // ═══════════════════════════════════════════════════════════════════════

    private void TerminalTab_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TerminalTabViewModel tab)
        {
            Vm.TerminalPanel.SelectTabCommand.Execute(tab);
            e.Handled = true;
        }
    }

    private void TerminalTabClose_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TerminalTabViewModel tab)
        {
            Vm.TerminalPanel.CloseTabCommand.Execute(tab);
            e.Handled = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HISTORY SEARCH (Ctrl+R)
    // ═══════════════════════════════════════════════════════════════════════

    private void HistorySearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var tab = Vm.TerminalPanel.ActiveTab;
        if (tab is null) return;

        switch (e.Key)
        {
            case Key.Enter:
                tab.AcceptHistorySearch();
                CommandInput.Focus();
                CommandInput.CaretIndex = CommandInput.Text.Length;
                e.Handled = true;
                break;

            case Key.Escape:
                tab.CancelHistorySearch();
                CommandInput.Focus();
                e.Handled = true;
                break;

            case Key.R when Keyboard.Modifiers == ModifierKeys.Control:
                // Ctrl+R again = find next match
                tab.FindNextHistoryMatch();
                e.Handled = true;
                break;
        }
    }

    private void HistorySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && Vm.TerminalPanel.ActiveTab is not null)
        {
            Vm.TerminalPanel.ActiveTab.UpdateHistorySearch(tb.Text);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  GLOBAL KEYBOARD SHORTCUTS
    // ═══════════════════════════════════════════════════════════════════════

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Tab switches between left and right file panes (standard dual-pane behavior)
        if (e.Key == Key.Tab && !IsTextInputFocused())
        {
            if (Vm.ActivePane == Vm.LeftPane)
                FocusPaneList(RightList, Vm.RightPane);
            else
                FocusPaneList(LeftList, Vm.LeftPane);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var ap = Vm.ActivePane;

        // ── File operations ──
        if (e.Key == Key.F2 && !IsTextInputFocused())
        { ap.BeginRenameCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.Delete && !IsTextInputFocused())
        { ap.DeleteSelectedCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.F7 && !IsTextInputFocused())
        { _ = ap.NewFolderCommand.ExecuteAsync(null); e.Handled = true; return; }

        if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
        { _ = ap.NewFileCommand.ExecuteAsync(null); e.Handled = true; return; }

        // ── Filter (Ctrl+F) ──
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ap.ToggleFilterCommand.Execute(null);
            if (ap.IsFilterVisible)
            {
                var box = Vm.ActivePane == Vm.LeftPane ? LeftFilterBox : RightFilterBox;
                box.Focus();
                box.SelectAll();
            }
            e.Handled = true; return;
        }

        // ── Quick Edit (F3) ──
        if (e.Key == Key.F3 && !IsTextInputFocused())
        { OpenQuickEdit(); e.Handled = true; return; }

        // ── File Diff (Ctrl+D) ──
        if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        { OpenFileDiff(); e.Handled = true; return; }

        // ── Preview (F8) ──
        if (e.Key == Key.F8 && !IsTextInputFocused())
        { ap.TogglePreviewCommand.Execute(null); e.Handled = true; return; }

        // ── Search (F9 / Ctrl+Shift+F) ──
        if ((e.Key == Key.F9 || (e.Key == Key.F && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
            && !IsTextInputFocused())
        { OpenSearch(); e.Handled = true; return; }

        // ── Settings (Ctrl+,) ──
        if (e.Key == Key.OemComma && Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        { OpenSettings(); e.Handled = true; return; }

        // ── Batch rename (Ctrl+M) ──
        if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        { OpenBatchRename(); e.Handled = true; return; }

        // ── Cross-pane copy/move ──
        if (e.Key == Key.F5 && !IsTextInputFocused())
        { _ = Vm.CopyToOtherPaneCommand.ExecuteAsync(null); e.Handled = true; return; }

        if (e.Key == Key.F6 && !IsTextInputFocused())
        { _ = Vm.MoveToOtherPaneCommand.ExecuteAsync(null); e.Handled = true; return; }

        // ── Clipboard ──
        if (e.Key == Key.C && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && IsListFocused())
        { ap.CopyPathCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control && IsListFocused())
        { ap.CutSelectedCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && IsListFocused())
        { ap.CopySelectedCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control && IsListFocused())
        { _ = ap.PasteCommand.ExecuteAsync(null); e.Handled = true; return; }

        if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control && IsListFocused())
        {
            var list = Vm.ActivePane == Vm.LeftPane ? LeftList : RightList;
            list.SelectAll(); e.Handled = true; return;
        }

        // ── Git History (Ctrl+G) ──
        if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        { _ = Vm.ShowGitHistoryCommand.ExecuteAsync(null); e.Handled = true; return; }

        // ── Tab management ──
        var tabs = Vm.ActivePane == Vm.LeftPane ? Vm.LeftTabs : Vm.RightTabs;

        if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
        { tabs.AddTabCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
        { if (tabs.ActiveTab is not null) tabs.CloseTabCommand.Execute(tabs.ActiveTab); e.Handled = true; return; }

        if (e.Key == Key.D && Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        { tabs.DuplicateTabCommand.Execute(null); e.Handled = true; return; }

        // ── Panel switching (Ctrl+1/2/3/4 for Tree/Left/Right/Terminal) ──
        if (Keyboard.Modifiers == ModifierKeys.Control && !IsTextInputFocused())
        {
            if (e.Key == Key.D1) { FocusFolderTree(); e.Handled = true; return; }
            if (e.Key == Key.D2) { FocusPaneList(LeftList, Vm.LeftPane); e.Handled = true; return; }
            if (e.Key == Key.D3) { FocusPaneList(RightList, Vm.RightPane); e.Handled = true; return; }
            if (e.Key == Key.D4) { CommandInput.Focus(); CommandInput.SelectAll(); e.Handled = true; return; }
        }

        // ── Navigation ──
        if (e.Key == Key.Back && !IsTextInputFocused())
        { ap.NavigateUpCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.Left && Keyboard.Modifiers == ModifierKeys.Alt)
        { ap.NavigateBackCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.Alt)
        { ap.NavigateForwardCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Control)
        { ap.NavigateCommand.Execute(null); e.Handled = true; return; }

        if ((e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control) || e.Key == Key.F4)
        { CommandInput.Focus(); CommandInput.SelectAll(); e.Handled = true; return; }

        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None && IsListFocused())
        { ap.OpenSelectedCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Alt && IsListFocused())
        { ap.OpenPropertiesCommand.Execute(null); e.Handled = true; return; }

        if (e.Key == Key.Escape && CommandInput.IsFocused)
        { Vm.CommandText = string.Empty; Vm.DismissSuggestions(); e.Handled = true; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private bool IsTextInputFocused()
    {
        var focused = FocusManager.GetFocusedElement(this);
        return focused is TextBox or ComboBox;
    }

    private bool IsListFocused()
    {
        var focused = FocusManager.GetFocusedElement(this);
        return focused is ListView
            || (focused is DependencyObject dep
                && (IsDescendant(LeftList, dep) || IsDescendant(RightList, dep)));
    }

    private static bool IsDescendant(DependencyObject parent, DependencyObject child)
    {
        var current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PANEL FOCUS HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Moves keyboard focus to a file list pane. If no item is selected,
    /// auto-selects the first item so arrow-key navigation works immediately.
    /// </summary>
    private void FocusPaneList(ListView list, PaneViewModel pane)
    {
        // Activate the pane in the view-model
        if (pane == Vm.LeftPane)
            Vm.ActivateLeftPaneCommand.Execute(null);
        else
            Vm.ActivateRightPaneCommand.Execute(null);

        list.Focus();

        // Ensure an item is selected so Up/Down arrows work right away
        if (list.SelectedItem is null && list.Items.Count > 0)
            list.SelectedIndex = 0;

        // Move keyboard focus into the selected item container
        if (list.SelectedItem is not null)
        {
            list.Dispatcher.BeginInvoke(() =>
            {
                list.ScrollIntoView(list.SelectedItem);
                if (list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem)
                    is ListViewItem container)
                {
                    container.Focus();
                }
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    /// <summary>
    /// Moves keyboard focus to the folder tree. Selects the first node if nothing is selected.
    /// </summary>
    private void FocusFolderTree()
    {
        FolderTreeView.Focus();
    }
}
