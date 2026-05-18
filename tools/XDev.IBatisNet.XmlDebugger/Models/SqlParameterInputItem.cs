using CommunityToolkit.Mvvm.ComponentModel;

namespace XDev.IBatisNet.XmlDebugger.Models;

public sealed partial class SqlParameterInputItem : ObservableObject
{
    public SqlParameterInputItem(string name, string kind, string value = "")
    {
        Name = name;
        Kind = kind;
        _value = value;
    }

    public string Name { get; }
    public string Kind { get; }

    [ObservableProperty]
    private string _value;

    public string Hint => Kind == "Inline SQL"
        ? "Raw SQL text"
        : "DbParameter value";
}
