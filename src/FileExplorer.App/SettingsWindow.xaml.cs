using System.Windows;
using System.Windows.Input;
using FileExplorer.App.Services.Settings;
using FileExplorer.App.ViewModels;

namespace FileExplorer.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow(ISettingsService settingsService)
    {
        InitializeComponent();
        DataContext = new SettingsViewModel(settingsService);

        // Close on Escape
        CloseCommand = new RelayCommand(_ => Close());
    }

    public ICommand CloseCommand { get; }

    private sealed class RelayCommand(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute(parameter);
    }
}
