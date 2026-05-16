using System;
using System.Collections.Generic;
using System.Linq;
using XDev.IBatisNet.XmlDebugger.Models;

namespace XDev.IBatisNet.XmlDebugger.Services;

public sealed class SqlMapAnalysisResult
{
    public string ConfigPath { get; init; } = "";
    public string WorkingRoot { get; init; } = "";
    public string ProviderName { get; init; } = "";
    public string DataSourceName { get; init; } = "";
    public string ConnectionString { get; init; } = "";
    public string ConnectionStringPreview { get; init; } = "";
    public ProviderInfo Provider { get; init; } = ProviderInfo.Empty();
    public TimeSpan Elapsed { get; init; }
    public IReadOnlyList<PropertyItem> Properties { get; init; } = [];
    public IReadOnlyList<PropertyItem> Settings { get; init; } = [];
    public IReadOnlyList<PropertyItem> Aliases { get; init; } = [];
    public IReadOnlyList<SqlMapFileItem> SqlMapFiles { get; init; } = [];
    public IReadOnlyList<StatementItem> Statements { get; init; } = [];
    public IReadOnlyList<DiagnosticItem> Diagnostics { get; init; } = [];

    public int ErrorCount => Diagnostics.Count(x => x.Severity == "Error");
    public int WarningCount => Diagnostics.Count(x => x.Severity == "Warning");
    public int InfoCount => Diagnostics.Count(x => x.Severity == "Info");
    public string ElapsedText => Elapsed.TotalMilliseconds < 1000
        ? $"{Elapsed.TotalMilliseconds:N0} ms"
        : $"{Elapsed.TotalSeconds:N2} s";
}
