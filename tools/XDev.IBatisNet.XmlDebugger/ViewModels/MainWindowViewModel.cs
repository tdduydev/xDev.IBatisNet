using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using XDev.IBatisNet.XmlDebugger.Models;
using XDev.IBatisNet.XmlDebugger.Services;

namespace XDev.IBatisNet.XmlDebugger.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly SqlMapAnalyzer _analyzer = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredStatements))]
    private string _searchText = "";

    [ObservableProperty]
    private string _configPath = "";

    [ObservableProperty]
    private string _workingRoot = "";

    [ObservableProperty]
    private string _providerName = "";

    [ObservableProperty]
    private string _dataSourceName = "";

    [ObservableProperty]
    private string _connectionStringPreview = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private StatementItem? _selectedStatement;

    public ObservableCollection<PropertyItem> Properties { get; } = [];
    public ObservableCollection<PropertyItem> Settings { get; } = [];
    public ObservableCollection<PropertyItem> Aliases { get; } = [];
    public ObservableCollection<SqlMapFileItem> SqlMapFiles { get; } = [];
    public ObservableCollection<StatementItem> Statements { get; } = [];
    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];

    public IEnumerable<StatementItem> FilteredStatements
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return Statements;
            }

            var term = SearchText.Trim();
            return Statements.Where(statement =>
                statement.Id.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                statement.Kind.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                statement.FilePath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                statement.SqlTemplate.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Analyze()
    {
        var result = _analyzer.Analyze(ConfigPath, WorkingRoot);

        ProviderName = result.ProviderName;
        DataSourceName = result.DataSourceName;
        ConnectionStringPreview = result.ConnectionStringPreview;
        ConfigPath = result.ConfigPath;
        WorkingRoot = result.WorkingRoot;

        Replace(Properties, result.Properties);
        Replace(Settings, result.Settings);
        Replace(Aliases, result.Aliases);
        Replace(SqlMapFiles, result.SqlMapFiles);
        Replace(Statements, result.Statements);
        Replace(Diagnostics, result.Diagnostics);

        SelectedStatement = Statements.FirstOrDefault();
        StatusText = $"{result.Statements.Count} statements, {result.SqlMapFiles.Count} maps, {result.ErrorCount} errors, {result.WarningCount} warnings, analyzed in {result.ElapsedText}";
        OnPropertyChanged(nameof(FilteredStatements));
    }

    public void Clear()
    {
        ProviderName = "";
        DataSourceName = "";
        ConnectionStringPreview = "";
        StatusText = "Ready";
        SelectedStatement = null;
        Properties.Clear();
        Settings.Clear();
        Aliases.Clear();
        SqlMapFiles.Clear();
        Statements.Clear();
        Diagnostics.Clear();
        OnPropertyChanged(nameof(FilteredStatements));
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredStatements));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
