using System.Data;
using System.Data.Common;

namespace XDev.IBatisNet.IntegrationPerf.FakeDb;

public sealed class FakeDbDataAdapter : IDbDataAdapter
{
    public IDbCommand? DeleteCommand { get; set; }
    public IDbCommand? InsertCommand { get; set; }
    public IDbCommand? SelectCommand { get; set; }
    public IDbCommand? UpdateCommand { get; set; }
    public MissingMappingAction MissingMappingAction { get; set; }
    public MissingSchemaAction MissingSchemaAction { get; set; }
    public ITableMappingCollection TableMappings { get; } = new DataTableMappingCollection();

    public int Fill(DataSet dataSet) => 0;
    public DataTable[] FillSchema(DataSet dataSet, SchemaType schemaType) => [];
    public IDataParameter[] GetFillParameters() => [];
    public int Update(DataSet dataSet) => 0;
}
