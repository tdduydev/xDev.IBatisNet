using System.Data;

namespace XDev.IBatisNet.IntegrationPerf.FakeDb;

public sealed class FakeDbConnection : IDbConnection
{
    public string ConnectionString { get; set; } = "";
    public int ConnectionTimeout => 0;
    public string Database => "Fake";
    public ConnectionState State { get; private set; } = ConnectionState.Closed;

    public IDbTransaction BeginTransaction() => throw new NotSupportedException();
    public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
    public void ChangeDatabase(string databaseName) { }
    public void Close() => State = ConnectionState.Closed;
    public IDbCommand CreateCommand() => new FakeDbCommand { Connection = this };
    public void Open() => State = ConnectionState.Open;
    public void Dispose() => Close();
}
