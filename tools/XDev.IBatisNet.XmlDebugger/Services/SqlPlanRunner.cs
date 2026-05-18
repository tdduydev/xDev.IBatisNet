using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XDev.IBatisNet.XmlDebugger.Services;

public sealed class SqlPlanRunner
{
    public async Task<string> ExplainAsync(
        ProviderInfo provider,
        string connectionString,
        string sql,
        IReadOnlyList<SqlCommandParameter> parameters,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return "No SQL preview is available to run.";
        }

        if (!LooksReadOnly(sql))
        {
            return "Explain plan is limited to SELECT/WITH statements. Mutating statements are not executed by the debugger.";
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return "No connection string is available. Paste a connection override or choose a config with a dataSource connection string.";
        }

        if (string.IsNullOrWhiteSpace(provider.ConnectionClass))
        {
            return "Provider connection class was not resolved from providers.config, so the debugger cannot open a database connection.";
        }

        var planSql = BuildPlanSql(provider, sql);
        if (string.IsNullOrWhiteSpace(planSql))
        {
            return $"Explain plan is not implemented for provider '{provider.Name}' / '{provider.ConnectionClass}'.";
        }

        try
        {
            var connectionType = ResolveType(provider.ConnectionClass, provider.AssemblyName);
            if (connectionType is null)
            {
                return $"Could not load connection type '{provider.ConnectionClass}'. Make sure the provider assembly is available to the debugger process.";
            }

            if (Activator.CreateInstance(connectionType) is not DbConnection connection)
            {
                return $"Connection type '{provider.ConnectionClass}' does not derive from DbConnection.";
            }

            await using (connection)
            {
                connection.ConnectionString = connectionString;
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                await using var command = connection.CreateCommand();
                command.CommandText = planSql;
                command.CommandType = CommandType.Text;
                command.CommandTimeout = Math.Clamp(commandTimeoutSeconds, 1, 600);
                AddParameters(command, parameters);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var output = await ReadResultSetsAsync(reader, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(output))
                {
                    output = "The database returned an empty plan result.";
                }

                return BuildReport(provider, parameters, output);
            }
        }
        catch (Exception ex)
        {
            return $"Explain plan failed: {ex.Message}";
        }
    }

    private static void AddParameters(DbCommand command, IReadOnlyList<SqlCommandParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.CommandName;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }

    private static async Task<string> ReadResultSetsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var resultSet = 0;
        do
        {
            resultSet++;
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine($"Result set {resultSet}");
            var fieldCount = reader.FieldCount;
            if (fieldCount == 0)
            {
                builder.AppendLine("(no columns)");
                continue;
            }

            builder.AppendLine(string.Join(" |", Enumerable.Range(0, fieldCount).Select(reader.GetName)));

            var rowCount = 0;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rowCount++;
                if (rowCount > 200)
                {
                    builder.AppendLine("... truncated after 200 rows");
                    break;
                }

                var values = new string[fieldCount];
                for (var index = 0; index < fieldCount; index++)
                {
                    values[index] = reader.IsDBNull(index) ? "NULL" : Convert.ToString(reader.GetValue(index)) ?? "";
                }

                builder.AppendLine(string.Join(" |", values));
            }
        }
        while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

        return builder.ToString().Trim();
    }

    private static string BuildReport(ProviderInfo provider, IReadOnlyList<SqlCommandParameter> parameters, string planOutput)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Provider: {provider.DisplayText}");
        builder.AppendLine($"Parameters: {(parameters.Count == 0 ? "(none)" : string.Join(", ", parameters.Select(x => x.DisplayName)))}");
        builder.AppendLine();
        builder.AppendLine("Heuristic summary:");
        builder.AppendLine(BuildHeuristicSummary(planOutput));
        builder.AppendLine();
        builder.AppendLine("Plan output:");
        builder.AppendLine(planOutput);
        return builder.ToString().TrimEnd();
    }

    private static string BuildHeuristicSummary(string planOutput)
    {
        var normalized = planOutput.ToLowerInvariant();
        var warnings = new List<string>();
        if (normalized.Contains("table scan", StringComparison.Ordinal) ||
            normalized.Contains("seq scan", StringComparison.Ordinal) ||
            normalized.Contains("full table scan", StringComparison.Ordinal) ||
            normalized.Contains("clustered index scan", StringComparison.Ordinal))
        {
            warnings.Add("- Possible scan found. Check filtering columns, indexes, and row estimates.");
        }

        if (normalized.Contains("missing index", StringComparison.Ordinal))
        {
            warnings.Add("- Missing-index hint found in the plan output.");
        }

        if (normalized.Contains("key lookup", StringComparison.Ordinal))
        {
            warnings.Add("- Key lookup found. A covering index may help if this query is hot.");
        }

        if (warnings.Count > 0)
        {
            warnings.Add("- This is a heuristic check; trust the database plan and real workload metrics over this summary.");
            return string.Join(Environment.NewLine, warnings);
        }

        return "- No obvious scan/missing-index keywords found. Still compare estimated vs. actual workload metrics before changing indexes.";
    }

    private static string BuildPlanSql(ProviderInfo provider, string sql)
    {
        var identity = $"{provider.Name} {provider.ConnectionClass} {provider.AssemblyName}".ToLowerInvariant();
        if (identity.Contains("sqlclient", StringComparison.Ordinal) || identity.Contains("sqlserver", StringComparison.Ordinal))
        {
            return $"SET SHOWPLAN_TEXT ON;{Environment.NewLine}{sql};{Environment.NewLine}SET SHOWPLAN_TEXT OFF;";
        }

        if (identity.Contains("npgsql", StringComparison.Ordinal) || identity.Contains("postgres", StringComparison.Ordinal))
        {
            return $"EXPLAIN (ANALYZE FALSE, COSTS TRUE, VERBOSE FALSE, BUFFERS FALSE){Environment.NewLine}{sql}";
        }

        if (identity.Contains("mysql", StringComparison.Ordinal) || identity.Contains("bytefx", StringComparison.Ordinal))
        {
            return $"EXPLAIN {sql}";
        }

        if (identity.Contains("sqlite", StringComparison.Ordinal))
        {
            return $"EXPLAIN QUERY PLAN {sql}";
        }

        return "";
    }

    private static bool LooksReadOnly(string sql)
    {
        var trimmed = sql.TrimStart();
        return trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }

    private static Type? ResolveType(string typeName, string assemblyName)
    {
        if (!string.IsNullOrWhiteSpace(assemblyName))
        {
            try
            {
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                var type = assembly.GetType(typeName, false, true);
                if (type is not null)
                {
                    return type;
                }
            }
            catch
            {
                // Fall through to already-loaded assemblies and Type.GetType.
            }
        }

        var direct = Type.GetType(typeName, false, true);
        if (direct is not null)
        {
            return direct;
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(x => x.GetType(typeName, false, true))
            .FirstOrDefault(x => x is not null);
    }
}
