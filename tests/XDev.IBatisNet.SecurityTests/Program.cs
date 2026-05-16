using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Xml;
using IBatisNet.Common;
using IBatisNet.Common.Exceptions;
using IBatisNet.Common.Utilities;
using IBatisNet.DataMapper.Configuration;
using XDev.IBatisNet.XmlDebugger.Services;

namespace XDev.IBatisNet.SecurityTests;

internal static class Program
{
    private static readonly (string Name, Action Run)[] Tests =
    [
        ("XML loaders reject DTD/XXE", XmlLoadersRejectDtd),
        ("Sensitive strings are redacted", SensitiveStringsAreRedacted),
        ("Analyzer flags inline SQL substitution", AnalyzerFlagsInlineSqlSubstitution),
        ("Analyzer reads direct SQL map XML", AnalyzerReadsDirectSqlMapXml),
        ("Analyzer flags risky compatibility settings", AnalyzerFlagsRiskyCompatibilitySettings),
        ("Analyzer flags legacy serializable cache cloning", AnalyzerFlagsLegacySerializableCacheCloning),
        ("Runtime can block inline SQL substitution", RuntimeCanBlockInlineSqlSubstitution),
        ("Runtime still accepts parameterized SQL when hardened", RuntimeAcceptsParameterizedSqlWhenHardened)
    ];

    public static int Main()
    {
        foreach (var test in Tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"PASS {test.Name}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
                return 1;
            }
        }

        return 0;
    }

    private static void XmlLoadersRejectDtd()
    {
        using var fixture = SecurityFixture.Create(includeInlineSql: false);
        var xmlPath = Path.Combine(fixture.Root, "xxe.xml");
        File.WriteAllText(
            xmlPath,
            """
            <?xml version="1.0" encoding="utf-8"?>
            <!DOCTYPE settings [
              <!ENTITY xxe SYSTEM "file:///c:/windows/win.ini">
            ]>
            <settings><add key="probe" value="&xxe;" /></settings>
            """);

        AssertThrows<ConfigurationException>(() => Resources.GetFileInfoAsXmlDocument(new FileInfo(xmlPath)));
    }

    private static void AnalyzerFlagsInlineSqlSubstitution()
    {
        using var fixture = SecurityFixture.Create(includeInlineSql: true);
        var result = new SqlMapAnalyzer().Analyze(fixture.ConfigPath, fixture.Root);

        Assert(
            result.Diagnostics.Any(x =>
                x.Severity.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
                x.Message.Contains("$OrderBy$", StringComparison.Ordinal)),
            "Expected a Security diagnostic for $OrderBy$ inline substitution.");

        Assert(
            result.Statements.Any(x => x.InlineSubstitutions.Contains("OrderBy")),
            "Expected statement inventory to include the raw inline substitution.");
    }

    private static void AnalyzerReadsDirectSqlMapXml()
    {
        var root = Path.Combine(Path.GetTempPath(), "xdev-ibatis-direct-map-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var mapPath = Path.Combine(root, "Direct.xml");
            File.WriteAllText(
                mapPath,
                """
                <?xml version="1.0" encoding="utf-8" ?>
                <sqlMap namespace="Direct" xmlns="http://ibatis.apache.org/mapping">
                  <statements>
                    <select id="FindDirect">
                      select Id, Code from Patient where Id = #Id#
                    </select>
                    <select id="UnsafeDirect">
                      select Id, Code from Patient order by $OrderBy$
                    </select>
                  </statements>
                </sqlMap>
                """);

            var result = new SqlMapAnalyzer().Analyze(mapPath, root);

            Assert(result.SqlMapFiles.Count == 1, "Expected the direct SQL map file to be listed.");
            Assert(result.SqlMapFiles[0].Status == "Loaded", "Expected the direct SQL map file to load.");
            Assert(result.SqlMapFiles[0].StatementCount == 2, "Expected both direct SQL map statements to be counted.");
            Assert(result.Statements.Any(x => x.Id == "FindDirect" && x.Parameters.Contains("Id")), "Expected direct map parameter inventory.");
            Assert(result.Statements.Any(x => x.Id == "UnsafeDirect" && x.InlineSubstitutions.Contains("OrderBy")), "Expected direct map inline substitution inventory.");
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void SensitiveStringsAreRedacted()
    {
        const string connectionString = "Server=.;Database=AppDb;User ID=sa;Password=secret;Token=abc;Application Name=xDev.IBatisNet";

        var masked = SecurityStringHelper.MaskConnectionString(connectionString);
        Assert(!masked.Contains("secret", StringComparison.Ordinal), "Expected password to be masked.");
        Assert(!masked.Contains("Token=abc", StringComparison.Ordinal), "Expected token to be masked.");
        Assert(!masked.Contains("User ID=sa", StringComparison.OrdinalIgnoreCase), "Expected user id to be masked.");

        var dataSource = new DataSource
        {
            Name = "default",
            ConnectionString = connectionString
        };
        Assert(!dataSource.ToString().Contains("secret", StringComparison.Ordinal), "Expected DataSource.ToString to mask secrets.");

        var cacheKey = SecurityStringHelper.CreateCacheKey(connectionString, "dbo.FindPatient");
        Assert(!cacheKey.Contains("secret", StringComparison.Ordinal), "Expected cache key to omit raw connection string.");
        Assert(!cacheKey.Contains("Server=", StringComparison.Ordinal), "Expected cache key to be opaque.");

        var logValue = SecurityStringHelper.FormatLogParameterValue("@PatientName", "PatientName", "Nguyen Van A");
        Assert(!logValue.Contains("Nguyen", StringComparison.Ordinal), "Expected debug log value to omit raw parameter data.");
        Assert(logValue.Contains("length=", StringComparison.Ordinal), "Expected debug log value to keep safe shape metadata.");
    }

    private static void AnalyzerFlagsRiskyCompatibilitySettings()
    {
        using var fixture = SecurityFixture.Create(
            includeInlineSql: false,
            settings:
            """
            <setting allowInlineSqlParameters="true" />
            <setting useEmbedStatementParams="true" />
            """);

        var result = new SqlMapAnalyzer().Analyze(fixture.ConfigPath, fixture.Root);
        Assert(
            result.Diagnostics.Any(x =>
                x.Severity.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
                x.Message.Contains("allowInlineSqlParameters=true", StringComparison.Ordinal)),
            "Expected a Security diagnostic for allowInlineSqlParameters=true.");
        Assert(
            result.Diagnostics.Any(x =>
                x.Severity.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
                x.Message.Contains("useEmbedStatementParams=true", StringComparison.Ordinal)),
            "Expected a Security diagnostic for useEmbedStatementParams=true.");
    }

    private static void AnalyzerFlagsLegacySerializableCacheCloning()
    {
        using var fixture = SecurityFixture.Create(includeInlineSql: false, includeSerializableCache: true);
        var result = new SqlMapAnalyzer().Analyze(fixture.ConfigPath, fixture.Root);

        Assert(
            result.Diagnostics.Any(x =>
                x.Severity.Equals("Security", StringComparison.OrdinalIgnoreCase) &&
                x.Message.Contains("Serializable read-write cache", StringComparison.Ordinal)),
            "Expected a Security diagnostic for serialize=true/readOnly=false cache cloning.");
    }

    private static void RuntimeCanBlockInlineSqlSubstitution()
    {
        using var fixture = SecurityFixture.Create(includeInlineSql: true);
        var builder = new DomSqlMapBuilder
        {
            ValidateSqlMapConfig = false
        };

        AssertThrows<ConfigurationException>(() => builder.Configure(new FileInfo(fixture.ConfigPath)));
    }

    private static void RuntimeAcceptsParameterizedSqlWhenHardened()
    {
        using var fixture = SecurityFixture.Create(includeInlineSql: false);
        var builder = new DomSqlMapBuilder
        {
            ValidateSqlMapConfig = false
        };

        var mapper = builder.Configure(new FileInfo(fixture.ConfigPath));
        Assert(mapper != null, "Expected mapper to build with #...# parameters.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Expected {typeof(TException).Name}, got {ex.GetType().Name}.", ex);
        }

        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}

internal sealed class SecurityFixture : IDisposable
{
    private SecurityFixture(string root, string configPath)
    {
        Root = root;
        ConfigPath = configPath;
    }

    public string Root { get; }
    public string ConfigPath { get; }

    public static SecurityFixture Create(bool includeInlineSql, string? settings = null, bool includeSerializableCache = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "xdev-ibatis-security-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var providersPath = Path.Combine(root, "providers.config");
        var mapPath = Path.Combine(root, "Patient.xml");
        var configPath = Path.Combine(root, "SqlMap.config");
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;

        File.WriteAllText(
            providersPath,
            $$"""
            <?xml version="1.0" encoding="utf-8" ?>
            <providers xmlns="http://ibatis.apache.org/providers">
              <clear />
              <provider
                  name="securityFake"
                  enabled="true"
                  assemblyName="{{assemblyName}}"
                  connectionClass="XDev.IBatisNet.SecurityTests.FakeDbConnection"
                  commandClass="XDev.IBatisNet.SecurityTests.FakeDbCommand"
                  parameterClass="XDev.IBatisNet.SecurityTests.FakeDbParameter"
                  parameterDbTypeClass="System.Data.DbType, System.Data.Common"
                  parameterDbTypeProperty="DbType"
                  dataAdapterClass="XDev.IBatisNet.SecurityTests.FakeDbDataAdapter"
                  commandBuilderClass="XDev.IBatisNet.SecurityTests.FakeDbCommandBuilder"
                  usePositionalParameters="false"
                  useParameterPrefixInSql="true"
                  useParameterPrefixInParameter="true"
                  parameterPrefix="@"
                  allowMARS="false" />
            </providers>
            """);

        var cacheModels = includeSerializableCache
            ? """
                <cacheModels>
                  <cacheModel id="PatientCache" implementation="MEMORY" readOnly="false" serialize="true" />
                </cacheModels>
              """
            : "";

        File.WriteAllText(
            mapPath,
            includeInlineSql
                ? $$"""
                  <?xml version="1.0" encoding="utf-8" ?>
                  <sqlMap namespace="Patient" xmlns="http://ibatis.apache.org/mapping">
                    {{cacheModels}}
                    <statements>
                      <select id="FindUnsafe">
                        select Id, Code from Patient where Id = #Id# order by $OrderBy$
                      </select>
                    </statements>
                  </sqlMap>
                  """
                : $$"""
                  <?xml version="1.0" encoding="utf-8" ?>
                  <sqlMap namespace="Patient" xmlns="http://ibatis.apache.org/mapping">
                    {{cacheModels}}
                    <statements>
                      <select id="FindSafe">
                        select Id, Code from Patient where Id = #Id#
                      </select>
                    </statements>
                  </sqlMap>
                  """);

        settings ??= """<setting allowInlineSqlParameters="false" />""";

        File.WriteAllText(
            configPath,
            $$"""
            <?xml version="1.0" encoding="utf-8" ?>
            <sqlMapConfig xmlns="http://ibatis.apache.org/dataMapper">
              <settings>
                {{settings}}
              </settings>
              <providers url="{{new Uri(providersPath).AbsoluteUri}}" />
              <database>
                <provider name="securityFake" />
                <dataSource name="securityFake" connectionString="fake=true" />
              </database>
              <sqlMaps>
                <sqlMap url="{{new Uri(mapPath).AbsoluteUri}}" />
              </sqlMaps>
            </sqlMapConfig>
            """);

        return new SecurityFixture(root, configPath);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch
        {
        }
    }
}

public sealed class FakeDbConnection : IDbConnection
{
    public string? ConnectionString { get; set; } = "";
    public int ConnectionTimeout => 0;
    public string Database => "security";
    public ConnectionState State => ConnectionState.Closed;
    public IDbTransaction BeginTransaction() => throw new NotSupportedException();
    public IDbTransaction BeginTransaction(IsolationLevel il) => throw new NotSupportedException();
    public void ChangeDatabase(string databaseName) { }
    public void Close() { }
    public IDbCommand CreateCommand() => new FakeDbCommand();
    public void Open() { }
    public void Dispose() { }
}

public sealed class FakeDbCommand : IDbCommand
{
    public string? CommandText { get; set; } = "";
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

public sealed class FakeDbParameter : IDbDataParameter
{
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
    public bool IsNullable => true;
    public string? ParameterName { get; set; } = "";
    public string? SourceColumn { get; set; } = "";
    public DataRowVersion SourceVersion { get; set; } = DataRowVersion.Current;
    public object? Value { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
}

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

public sealed class FakeDbCommandBuilder
{
}

public sealed class FakeParameterCollection : IDataParameterCollection
{
    private readonly ArrayList _items = [];

    public object this[string parameterName]
    {
        get => _items.Cast<IDataParameter>().FirstOrDefault(x => x.ParameterName == parameterName) ?? null!;
        set
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                _items.Add(value);
            }
            else
            {
                _items[index] = value;
            }
        }
    }

    public object this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public bool IsFixedSize => false;
    public bool IsReadOnly => false;
    public int Count => _items.Count;
    public bool IsSynchronized => false;
    public object SyncRoot => this;
    public int Add(object? value) => _items.Add(value);
    public void Clear() => _items.Clear();
    public bool Contains(string parameterName) => IndexOf(parameterName) >= 0;
    public bool Contains(object? value) => _items.Contains(value);
    public void CopyTo(Array array, int index) => _items.CopyTo(array, index);
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(string parameterName)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (_items[i] is IDataParameter parameter && parameter.ParameterName == parameterName)
            {
                return i;
            }
        }

        return -1;
    }

    public int IndexOf(object? value) => _items.IndexOf(value);
    public void Insert(int index, object? value) => _items.Insert(index, value);
    public void Remove(object? value) => _items.Remove(value);
    public void RemoveAt(string parameterName)
    {
        var index = IndexOf(parameterName);
        if (index >= 0)
        {
            _items.RemoveAt(index);
        }
    }

    public void RemoveAt(int index) => _items.RemoveAt(index);
}
