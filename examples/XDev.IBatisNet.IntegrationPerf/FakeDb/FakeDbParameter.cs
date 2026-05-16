using System.Data;

namespace XDev.IBatisNet.IntegrationPerf.FakeDb;

public sealed class FakeDbParameter : IDbDataParameter
{
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public bool IsNullable => true;
    public string ParameterName { get; set; } = "";
    public string SourceColumn { get; set; } = "";
    public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
    public object? Value { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
}
