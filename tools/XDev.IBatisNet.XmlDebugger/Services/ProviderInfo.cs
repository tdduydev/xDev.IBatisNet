namespace XDev.IBatisNet.XmlDebugger.Services;

public sealed record ProviderInfo(
    string Name,
    string AssemblyName,
    string ConnectionClass,
    string CommandClass,
    string ParameterPrefix,
    bool UsePositionalParameters,
    bool UseParameterPrefixInSql,
    bool UseParameterPrefixInParameter)
{
    public static ProviderInfo Empty(string name = "") => new(
        name,
        "",
        "",
        "",
        "@",
        false,
        true,
        true);

    public string DisplayText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ConnectionClass))
            {
                return string.IsNullOrWhiteSpace(Name) ? "(not resolved)" : Name;
            }

            return $"{Name} ({ConnectionClass})";
        }
    }
}
