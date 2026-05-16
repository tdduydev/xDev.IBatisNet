using System.Data;

namespace XDev.IBatisNet.IntegrationPerf.FakeDb;

public sealed class FakeDbCommand : IDbCommand
{
    public string CommandText { get; set; } = "";
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; } = CommandType.Text;
    public IDbConnection? Connection { get; set; }
    public IDataParameterCollection Parameters { get; } = new FakeParameterCollection();
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }
    public IDbDataParameter CreateParameter() => new FakeDbParameter();
    public int ExecuteNonQuery() => 0;
    public IDataReader ExecuteReader() => throw new NotSupportedException();
    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotSupportedException();
    public object? ExecuteScalar() => null;
    public void Prepare() { }
    public void Dispose() { }
}
