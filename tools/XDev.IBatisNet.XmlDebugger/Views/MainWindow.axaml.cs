using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using XDev.IBatisNet.XmlDebugger.ViewModels;

namespace XDev.IBatisNet.XmlDebugger.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private async void BrowseConfigButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open SqlMap.config or SQL map XML",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("iBATIS config") { Patterns = ["*.config", "*.xml"] },
                FilePickerFileTypes.All
            ]
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path) || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.ConfigPath = path;
        if (string.IsNullOrWhiteSpace(viewModel.WorkingRoot))
        {
            viewModel.WorkingRoot = Path.GetDirectoryName(path) ?? "";
        }

        viewModel.Analyze();
    }

    private async void BrowseRootButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open working root",
            AllowMultiple = false
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path) && DataContext is MainWindowViewModel viewModel)
        {
            viewModel.WorkingRoot = path;
        }
    }

    private void AnalyzeButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Analyze();
        }
    }

    private void ClearButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.Clear();
        }
    }

    private void RefreshSqlPreviewButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.RefreshSqlPreview();
        }
    }

    private async void ExportSelectedSqlButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await ExportSqlAsync("Export selected SQL", "selected-sql.md", viewModel.BuildSelectedSqlExport());
        }
    }

    private async void ExportAllSqlButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await ExportSqlAsync("Export all SQL", "sql-preview-export.md", viewModel.BuildAllSqlExport());
        }
    }

    private async void RunExplainPlanButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.RunExplainPlanAsync();
        }
    }

    private void ResetDbConfigButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.ResetDbConfigFromSqlMap();
        }
    }

    private async Task ExportSqlAsync(string title, string suggestedFileName, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedFileName,
            FileTypeChoices =
            [
                new FilePickerFileType("Markdown") { Patterns = ["*.md"] },
                new FilePickerFileType("SQL") { Patterns = ["*.sql"] },
                FilePickerFileTypes.All
            ]
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await File.WriteAllTextAsync(path, content);
        }
    }
}
