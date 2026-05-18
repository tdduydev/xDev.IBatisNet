using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using XDev.IBatisNet.XmlDebugger.Models;
using XDev.IBatisNet.XmlDebugger.Services;

namespace XDev.IBatisNet.XmlDebugger.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly SqlMapAnalyzer _analyzer = new();
    private readonly SqlPreviewService _previewService = new();
    private readonly SqlPlanRunner _planRunner = new();
    private ProviderInfo _provider = ProviderInfo.Empty();
    private string _resolvedConnectionString = "";
    private SqlPreviewResult _previewResult = new("", [], []);
    private bool _isUpdatingParameterRows;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredStatements))]
    private string _searchText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredDiagnostics))]
    private string _issueSearchText = "";

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
    private string _providerDetails = "";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private StatementItem? _selectedStatement;

    [ObservableProperty]
    private DiagnosticItem? _selectedDiagnostic;

    [ObservableProperty]
    private string _parameterInput = "";

    [ObservableProperty]
    private string _preparedSql = "";

    [ObservableProperty]
    private string _sqlPreviewStatus = "Choose a statement to preview SQL.";

    [ObservableProperty]
    private string _planConnectionString = "";

    [ObservableProperty]
    private string _dbConnectionString = "";

    [ObservableProperty]
    private string _dbProviderSummary = "";

    [ObservableProperty]
    private string _dbParameterSummary = "";

    [ObservableProperty]
    private int _commandTimeoutSeconds = 30;

    [ObservableProperty]
    private string _planResultText = "Run an explain plan to check whether the database reports scans, missing indexes, or other obvious query-plan issues.";

    public ObservableCollection<PropertyItem> Properties { get; } = [];
    public ObservableCollection<PropertyItem> Settings { get; } = [];
    public ObservableCollection<PropertyItem> Aliases { get; } = [];
    public ObservableCollection<SqlMapFileItem> SqlMapFiles { get; } = [];
    public ObservableCollection<StatementItem> Statements { get; } = [];
    public ObservableCollection<DiagnosticItem> Diagnostics { get; } = [];
    public ObservableCollection<SqlParameterInputItem> SqlParameters { get; } = [];

    public string AnalysisHealthText
    {
        get
        {
            if (Diagnostics.Count == 0 && Statements.Count == 0)
            {
                return "Ready";
            }

            if (ErrorCount > 0)
            {
                return "Blocked";
            }

            if (SecurityCount > 0)
            {
                return "Security review";
            }

            if (WarningCount > 0)
            {
                return "Review";
            }

            return "Clean";
        }
    }

    public int ErrorCount => Diagnostics.Count(x => x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
    public int WarningCount => Diagnostics.Count(x => x.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
    public int SecurityCount => Diagnostics.Count(x => x.Severity.Equals("Security", StringComparison.OrdinalIgnoreCase));
    public int InfoCount => Diagnostics.Count(x => x.Severity.Equals("Info", StringComparison.OrdinalIgnoreCase));
    public int StatementCount => Statements.Count;
    public int MapCount => SqlMapFiles.Count;
    public int InlineSqlCount => Statements.Sum(x => x.InlineSubstitutions.Count);
    public int ParameterCount => Statements.Sum(x => x.Parameters.Count);

    public string IssueCountText => $"{SecurityCount} security · {ErrorCount} errors · {WarningCount} warnings";
    public string MapCountText => $"{MapCount} maps";
    public string StatementCountText => $"{StatementCount} statements";
    public string InlineSqlCountText => $"{InlineSqlCount} raw inline";
    public string ParameterCountText => $"{ParameterCount} parameters";

    public IEnumerable<DiagnosticItem> FilteredDiagnostics
    {
        get
        {
            if (string.IsNullOrWhiteSpace(IssueSearchText))
            {
                return Diagnostics;
            }

            var term = IssueSearchText.Trim();
            return Diagnostics.Where(issue =>
                issue.Severity.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                issue.Message.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                issue.DisplayPath.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (issue.Location?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }

    public IEnumerable<DiagnosticItem> SelectedStatementDiagnostics
    {
        get
        {
            if (SelectedStatement is null)
            {
                return [];
            }

            return Diagnostics.Where(issue => MatchesStatement(issue, SelectedStatement));
        }
    }

    public string SelectedStatementIssueText
    {
        get
        {
            var count = SelectedStatementDiagnostics.Count();
            return count == 0 ? "No linked issues" : $"{count} linked issues";
        }
    }

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
        ProviderDetails = result.Provider.DisplayText;
        ConfigPath = result.ConfigPath;
        WorkingRoot = result.WorkingRoot;
        _provider = result.Provider;
        _resolvedConnectionString = result.ConnectionString;
        DbConnectionString = result.ConnectionString;
        DbProviderSummary = result.Provider.DisplayText;
        DbParameterSummary = $"SQL prefix '{result.Provider.ParameterPrefix}', positional={result.Provider.UsePositionalParameters}, prefix-in-sql={result.Provider.UseParameterPrefixInSql}, prefix-in-parameter={result.Provider.UseParameterPrefixInParameter}";

        Replace(Properties, result.Properties);
        Replace(Settings, result.Settings);
        Replace(Aliases, result.Aliases);
        Replace(SqlMapFiles, result.SqlMapFiles);
        Replace(Statements, result.Statements);
        Replace(Diagnostics, result.Diagnostics);

        SelectedDiagnostic = Diagnostics.FirstOrDefault(x =>
            x.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
            x.Severity.Equals("Security", StringComparison.OrdinalIgnoreCase));
        SelectedStatement = SelectedDiagnostic is null
            ? Statements.FirstOrDefault()
            : FindStatementForDiagnostic(SelectedDiagnostic) ?? Statements.FirstOrDefault();
        StatusText = $"{result.Statements.Count} statements, {result.SqlMapFiles.Count} maps, {result.ErrorCount} errors, {result.WarningCount} warnings, analyzed in {result.ElapsedText}";
        NotifyAnalysisPropertiesChanged();
    }

    public void Clear()
    {
        ProviderName = "";
        DataSourceName = "";
        ConnectionStringPreview = "";
        ProviderDetails = "";
        StatusText = "Ready";
        SelectedStatement = null;
        SelectedDiagnostic = null;
        IssueSearchText = "";
        SearchText = "";
        ParameterInput = "";
        PreparedSql = "";
        SqlPreviewStatus = "Choose a statement to preview SQL.";
        PlanConnectionString = "";
        DbConnectionString = "";
        DbProviderSummary = "";
        DbParameterSummary = "";
        CommandTimeoutSeconds = 30;
        PlanResultText = "Run an explain plan to check whether the database reports scans, missing indexes, or other obvious query-plan issues.";
        _provider = ProviderInfo.Empty();
        _resolvedConnectionString = "";
        _previewResult = new SqlPreviewResult("", [], []);
        ClearParameterRows();
        Properties.Clear();
        Settings.Clear();
        Aliases.Clear();
        SqlMapFiles.Clear();
        Statements.Clear();
        Diagnostics.Clear();
        NotifyAnalysisPropertiesChanged();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredStatements));
    }

    partial void OnIssueSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredDiagnostics));
    }

    partial void OnSelectedStatementChanged(StatementItem? value)
    {
        RebuildParameterRows(value);
        RefreshSqlPreview();
        OnPropertyChanged(nameof(SelectedStatementDiagnostics));
        OnPropertyChanged(nameof(SelectedStatementIssueText));
    }

    partial void OnSelectedDiagnosticChanged(DiagnosticItem? value)
    {
        if (value is null)
        {
            return;
        }

        var statement = FindStatementForDiagnostic(value);
        if (statement is not null)
        {
            SelectedStatement = statement;
        }
    }

    partial void OnParameterInputChanged(string value)
    {
        RefreshSqlPreview();
    }

    public void RefreshSqlPreview()
    {
        ParameterInput = BuildParameterInputText();
        _previewResult = _previewService.Render(SelectedStatement, ParameterInput, _provider);
        PreparedSql = _previewResult.Sql;
        SqlPreviewStatus = $"{_previewResult.MessageSummary} Parameters: {_previewResult.ParameterSummary}";
    }

    public string BuildSelectedSqlExport()
    {
        if (SelectedStatement is null)
        {
            return "";
        }

        RefreshSqlPreview();
        return $"""
        # {SelectedStatement.DisplayName}

        Source: {SelectedStatement.FilePath}
        Kind: {SelectedStatement.Kind}
        Parameters: {_previewResult.ParameterSummary}

        ```sql
        {PreparedSql}
        ```
        """;
    }

    public string BuildAllSqlExport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("# xDev.IBatisNet SQL preview export");
        builder.AppendLine();
        builder.AppendLine($"Config: {ConfigPath}");
        builder.AppendLine($"Provider: {ProviderDetails}");
        builder.AppendLine();

        foreach (var statement in Statements)
        {
            var preview = _previewService.Render(statement, SqlPreviewService.BuildDefaultParameterInput(statement), _provider);
            builder.AppendLine($"## {statement.DisplayName}");
            builder.AppendLine();
            builder.AppendLine($"Source: {statement.FilePath}");
            builder.AppendLine($"Kind: {statement.Kind}");
            builder.AppendLine($"Parameters: {preview.ParameterSummary}");
            builder.AppendLine();
            builder.AppendLine("```sql");
            builder.AppendLine(preview.Sql);
            builder.AppendLine("```");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    public async Task RunExplainPlanAsync()
    {
        RefreshSqlPreview();
        var connectionString = string.IsNullOrWhiteSpace(DbConnectionString)
            ? _resolvedConnectionString
            : DbConnectionString;

        PlanResultText = "Running explain plan...";
        PlanResultText = await _planRunner.ExplainAsync(_provider, connectionString, PreparedSql, _previewResult.Parameters, CommandTimeoutSeconds);
    }

    public void ResetDbConfigFromSqlMap()
    {
        DbConnectionString = _resolvedConnectionString;
        PlanResultText = "Database connection string reset from SqlMap.config.";
    }

    private void RebuildParameterRows(StatementItem? statement)
    {
        _isUpdatingParameterRows = true;
        try
        {
            ClearParameterRows();
            if (statement is null)
            {
                ParameterInput = "";
                return;
            }

            foreach (var parameter in statement.Parameters)
            {
                AddParameterRow(new SqlParameterInputItem(parameter, "Parameter"));
            }

            foreach (var substitution in statement.InlineSubstitutions)
            {
                AddParameterRow(new SqlParameterInputItem(substitution, "Inline SQL"));
            }

            ParameterInput = BuildParameterInputText();
        }
        finally
        {
            _isUpdatingParameterRows = false;
        }
    }

    private void ClearParameterRows()
    {
        foreach (var parameter in SqlParameters)
        {
            parameter.PropertyChanged -= ParameterInputItem_OnPropertyChanged;
        }

        SqlParameters.Clear();
    }

    private void AddParameterRow(SqlParameterInputItem parameter)
    {
        parameter.PropertyChanged += ParameterInputItem_OnPropertyChanged;
        SqlParameters.Add(parameter);
    }

    private void ParameterInputItem_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isUpdatingParameterRows || e.PropertyName != nameof(SqlParameterInputItem.Value))
        {
            return;
        }

        RefreshSqlPreview();
    }

    private string BuildParameterInputText()
    {
        if (SqlParameters.Count == 0)
        {
            return "";
        }

        return string.Join(Environment.NewLine, SqlParameters.Select(x => $"{x.Name}={x.Value}"));
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private StatementItem? FindStatementForDiagnostic(DiagnosticItem issue)
    {
        if (!string.IsNullOrWhiteSpace(issue.Location))
        {
            var byId = Statements.FirstOrDefault(statement =>
                statement.Id.Equals(issue.Location, StringComparison.OrdinalIgnoreCase) ||
                statement.DisplayName.Equals(issue.Location, StringComparison.OrdinalIgnoreCase));
            if (byId is not null)
            {
                return byId;
            }
        }

        if (!string.IsNullOrWhiteSpace(issue.FilePath))
        {
            return Statements.FirstOrDefault(statement =>
                statement.FilePath.Equals(issue.FilePath, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    private static bool MatchesStatement(DiagnosticItem issue, StatementItem statement)
    {
        if (!string.IsNullOrWhiteSpace(issue.FilePath) &&
            !issue.FilePath.Equals(statement.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(issue.Location))
        {
            return true;
        }

        return issue.Location.Equals(statement.Id, StringComparison.OrdinalIgnoreCase) ||
            issue.Location.Equals(statement.DisplayName, StringComparison.OrdinalIgnoreCase) ||
            issue.Location.StartsWith("line ", StringComparison.OrdinalIgnoreCase);
    }

    private void NotifyAnalysisPropertiesChanged()
    {
        OnPropertyChanged(nameof(FilteredStatements));
        OnPropertyChanged(nameof(FilteredDiagnostics));
        OnPropertyChanged(nameof(SelectedStatementDiagnostics));
        OnPropertyChanged(nameof(SelectedStatementIssueText));
        OnPropertyChanged(nameof(AnalysisHealthText));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(SecurityCount));
        OnPropertyChanged(nameof(InfoCount));
        OnPropertyChanged(nameof(StatementCount));
        OnPropertyChanged(nameof(MapCount));
        OnPropertyChanged(nameof(InlineSqlCount));
        OnPropertyChanged(nameof(ParameterCount));
        OnPropertyChanged(nameof(IssueCountText));
        OnPropertyChanged(nameof(MapCountText));
        OnPropertyChanged(nameof(StatementCountText));
        OnPropertyChanged(nameof(InlineSqlCountText));
        OnPropertyChanged(nameof(ParameterCountText));
    }
}
