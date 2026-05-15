namespace XDev.IBatisNet.XmlDebugger.Models;

public sealed record SqlMapFileItem(
    string Kind,
    string Source,
    string ResolvedPath,
    int StatementCount,
    string Status);
