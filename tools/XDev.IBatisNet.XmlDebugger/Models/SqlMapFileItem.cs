namespace XDev.IBatisNet.XmlDebugger.Models;

public sealed record SqlMapFileItem(
    string Kind,
    string Source,
    string ResolvedPath,
    int StatementCount,
    string Status)
{
    public string DisplayName => string.IsNullOrWhiteSpace(ResolvedPath)
        ? Source
        : System.IO.Path.GetFileName(ResolvedPath);

    public string Summary => $"{Status} · {StatementCount} statements";
}
