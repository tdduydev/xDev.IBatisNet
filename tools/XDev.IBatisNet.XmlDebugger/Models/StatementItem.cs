using System.Collections.Generic;

namespace XDev.IBatisNet.XmlDebugger.Models;

public sealed record StatementItem(
    string Id,
    string Kind,
    string FilePath,
    string? ParameterClass,
    string? ResultClass,
    string? ResultMap,
    string SqlTemplate,
    IReadOnlyList<string> Parameters,
    IReadOnlyList<string> Includes)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Id) ? $"({Kind})" : Id;
    public string ParameterText => Parameters.Count == 0 ? "(none)" : string.Join(", ", Parameters);
    public string IncludeText => Includes.Count == 0 ? "(none)" : string.Join(", ", Includes);
}
