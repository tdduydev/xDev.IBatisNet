using System.Collections.Generic;
using System.IO;

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
    IReadOnlyList<string> InlineSubstitutions,
    IReadOnlyList<string> Includes)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Id) ? $"({Kind})" : Id;
    public string ShortFileName => string.IsNullOrWhiteSpace(FilePath) ? "" : Path.GetFileName(FilePath);
    public string Signature => $"{Kind} · {Parameters.Count} params · {InlineSubstitutions.Count} inline";
    public string RiskSummary => InlineSubstitutions.Count == 0 ? "Prepared" : $"{InlineSubstitutions.Count} raw inline";
    public string ParameterText => Parameters.Count == 0 ? "(none)" : string.Join(", ", Parameters);
    public string InlineSubstitutionText => InlineSubstitutions.Count == 0 ? "(none)" : string.Join(", ", InlineSubstitutions);
    public string IncludeText => Includes.Count == 0 ? "(none)" : string.Join(", ", Includes);
}
