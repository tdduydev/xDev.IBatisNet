using System.Collections.Generic;
using System.Linq;

namespace XDev.IBatisNet.XmlDebugger.Services;

public sealed record SqlCommandParameter(
    string TokenName,
    string CommandName,
    object? Value,
    string RawValue,
    bool IsMissing)
{
    public string DisplayName => string.IsNullOrWhiteSpace(CommandName) ? TokenName : CommandName;
}

public sealed record SqlPreviewResult(
    string Sql,
    IReadOnlyList<SqlCommandParameter> Parameters,
    IReadOnlyList<string> Messages)
{
    public string ParameterSummary => Parameters.Count == 0
        ? "(none)"
        : string.Join(", ", Parameters.Select(x => x.DisplayName));

    public string MessageSummary => Messages.Count == 0
        ? "SQL preview ready."
        : string.Join(" ", Messages);
}
