namespace XDev.IBatisNet.XmlDebugger.Models;

public sealed record DiagnosticItem(
    string Severity,
    string Message,
    string? FilePath = null,
    string? Location = null)
{
    public string DisplayPath => string.IsNullOrWhiteSpace(FilePath) ? "" : FilePath;
}
