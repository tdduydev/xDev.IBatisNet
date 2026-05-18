namespace XDev.IBatisNet.XmlDebugger.Models;

public sealed record DiagnosticItem(
    string Severity,
    string Message,
    string? FilePath = null,
    string? Location = null)
{
    public string DisplayPath => string.IsNullOrWhiteSpace(FilePath) ? "" : FilePath;
    public string ShortPath => string.IsNullOrWhiteSpace(FilePath) ? "" : System.IO.Path.GetFileName(FilePath);
    public string Summary => string.IsNullOrWhiteSpace(Location) ? Severity : $"{Severity} · {Location}";
}
