using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using XDev.IBatisNet.XmlDebugger.Models;

namespace XDev.IBatisNet.XmlDebugger.Services;

public sealed partial class SqlMapAnalyzer
{
    private static readonly HashSet<string> StatementElementNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "select",
        "insert",
        "update",
        "delete",
        "statement",
        "procedure"
    };

    public SqlMapAnalysisResult Analyze(string configPath, string? workingRoot)
    {
        var stopwatch = Stopwatch.StartNew();
        var diagnostics = new List<DiagnosticItem>();
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var settingValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var settings = new List<PropertyItem>();
        var aliases = new List<PropertyItem>();
        var sqlMapFiles = new List<SqlMapFileItem>();
        var statements = new List<StatementItem>();
        var sqlFragments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        configPath = NormalizePath(configPath);
        if (string.IsNullOrWhiteSpace(configPath))
        {
            diagnostics.Add(new DiagnosticItem("Error", "Choose a SqlMap.config file."));
            return new SqlMapAnalysisResult { Diagnostics = diagnostics, Elapsed = stopwatch.Elapsed };
        }

        if (!File.Exists(configPath))
        {
            diagnostics.Add(new DiagnosticItem("Error", "SqlMap.config was not found.", configPath));
            return new SqlMapAnalysisResult { ConfigPath = configPath, Diagnostics = diagnostics, Elapsed = stopwatch.Elapsed };
        }

        var configDir = Path.GetDirectoryName(configPath) ?? Environment.CurrentDirectory;
        var root = string.IsNullOrWhiteSpace(workingRoot) ? configDir : NormalizePath(workingRoot);
        if (!Directory.Exists(root))
        {
            diagnostics.Add(new DiagnosticItem("Warning", "Working root does not exist; relative resources fall back to the config folder.", root));
            root = configDir;
        }

        XDocument config;
        try
        {
            config = LoadSafeXDocument(configPath, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            diagnostics.Add(new DiagnosticItem("Error", $"SqlMap.config is not valid XML: {ex.Message}", configPath));
            return new SqlMapAnalysisResult
            {
                ConfigPath = configPath,
                WorkingRoot = root,
                Diagnostics = diagnostics,
                Elapsed = stopwatch.Elapsed
            };
        }

        var ns = config.Root?.Name.Namespace ?? XNamespace.None;
        LoadProperties(config, ns, configDir, properties, diagnostics);
        properties.TryAdd("root", root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar);

        foreach (var setting in config.Descendants(ns + "setting"))
        {
            var key = Attr(setting, "name");
            if (!string.IsNullOrWhiteSpace(key))
            {
                var value = Attr(setting, "value") ?? "";
                settingValues[key] = value;
                settings.Add(new PropertyItem(key, value));
                continue;
            }

            foreach (var attribute in setting.Attributes().Where(x => !x.IsNamespaceDeclaration))
            {
                settingValues[attribute.Name.LocalName] = attribute.Value;
                settings.Add(new PropertyItem(attribute.Name.LocalName, attribute.Value));
            }
        }

        var useStatementNamespaces = settingValues.TryGetValue("useStatementNamespaces", out var useNamespacesValue) &&
            bool.TryParse(useNamespacesValue, out var parsedUseNamespaces) &&
            parsedUseNamespaces;

        AddConfigurationSecurityDiagnostics(settingValues, configPath, diagnostics);

        foreach (var alias in config.Descendants(ns + "typeAlias"))
        {
            aliases.Add(new PropertyItem(Attr(alias, "alias") ?? "(unnamed)", Attr(alias, "type") ?? ""));
        }

        var providerDefinitions = LoadProviderDefinitions(config, ns, configDir, properties, diagnostics);
        var database = config.Descendants(ns + "database").FirstOrDefault();
        var databaseProvider = database?.Elements().FirstOrDefault(x => x.Name.LocalName.Equals("provider", StringComparison.OrdinalIgnoreCase));
        var providerName = ResolveValue(Attr(databaseProvider, "name") ?? "", properties, diagnostics, configPath);
        var provider = providerDefinitions.TryGetValue(providerName, out var resolvedProvider)
            ? resolvedProvider
            : ProviderInfo.Empty(providerName);
        if (!string.IsNullOrWhiteSpace(providerName) && string.IsNullOrWhiteSpace(provider.ConnectionClass))
        {
            diagnostics.Add(new DiagnosticItem("Warning", $"Provider '{providerName}' was not resolved from providers.config. SQL preview can still run, but explain-plan execution needs a provider connection class.", configPath));
        }

        var dataSource = config.Descendants(ns + "dataSource").FirstOrDefault();
        var dataSourceName = ResolveValue(Attr(dataSource, "name") ?? "", properties, diagnostics, configPath);
        var connectionString = ResolveValue(Attr(dataSource, "connectionString") ?? "", properties, diagnostics, configPath);

        foreach (var sqlMap in config.Descendants(ns + "sqlMap"))
        {
            AnalyzeSqlMapElement(sqlMap, configDir, root, properties, useStatementNamespaces, sqlMapFiles, statements, sqlFragments, diagnostics);
        }

        foreach (var group in statements.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Count() > 1))
        {
            diagnostics.Add(new DiagnosticItem("Warning", $"Duplicate statement id '{group.Key}' appears {group.Count()} times.", group.First().FilePath));
        }

        var statementIds = statements
            .Select(x => x.Id)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var statement in statements)
        {
            foreach (var include in statement.Includes)
            {
                if (!sqlFragments.ContainsKey(include) && !statementIds.Contains(include))
                {
                    diagnostics.Add(new DiagnosticItem("Warning", $"Include refid '{include}' was not found in loaded SQL fragments/statements.", statement.FilePath, statement.Id));
                }
            }
        }

        if (statements.Count == 0 && diagnostics.All(x => x.Severity != "Error"))
        {
            diagnostics.Add(new DiagnosticItem("Info", "No statements were found. This can be valid for a base config that loads maps at runtime.", configPath));
        }

        return new SqlMapAnalysisResult
        {
            ConfigPath = configPath,
            WorkingRoot = root,
            ProviderName = providerName,
            DataSourceName = dataSourceName,
            ConnectionString = connectionString,
            ConnectionStringPreview = MaskConnectionString(connectionString),
            Provider = provider,
            Properties = properties.OrderBy(x => x.Key).Select(x => new PropertyItem(x.Key, x.Value)).ToList(),
            Settings = settings.OrderBy(x => x.Key).ToList(),
            Aliases = aliases.OrderBy(x => x.Key).ToList(),
            SqlMapFiles = sqlMapFiles,
            Statements = statements.OrderBy(x => x.Id).ThenBy(x => x.Kind).ToList(),
            Diagnostics = diagnostics,
            Elapsed = stopwatch.Elapsed
        };
    }

    private static void LoadProperties(
        XDocument config,
        XNamespace ns,
        string configDir,
        IDictionary<string, string> properties,
        ICollection<DiagnosticItem> diagnostics)
    {
        foreach (var propertiesElement in config.Descendants(ns + "properties"))
        {
            LoadPropertiesResource(Attr(propertiesElement, "resource"), configDir, properties, diagnostics);

            foreach (var property in propertiesElement.Elements().Where(x => x.Name.LocalName.Equals("property", StringComparison.OrdinalIgnoreCase)))
            {
                var resource = Attr(property, "resource");
                if (!string.IsNullOrWhiteSpace(resource))
                {
                    LoadPropertiesResource(resource, configDir, properties, diagnostics);
                    continue;
                }

                var key = Attr(property, "key") ?? Attr(property, "name");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                properties[key] = Attr(property, "value") ?? "";
            }
        }
    }

    private static void LoadPropertiesResource(
        string? resource,
        string configDir,
        IDictionary<string, string> properties,
        ICollection<DiagnosticItem> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return;
        }

        var resolved = ResolveConfigRelativePath(resource, configDir, properties, diagnostics, null);
        if (!File.Exists(resolved))
        {
            diagnostics.Add(new DiagnosticItem("Warning", "Properties resource was not found.", resolved));
            return;
        }

        try
        {
            var doc = LoadSafeXDocument(resolved, LoadOptions.SetLineInfo);
            foreach (var add in doc.Descendants().Where(x => x.Name.LocalName == "add"))
            {
                var key = Attr(add, "key") ?? Attr(add, "name");
                var value = Attr(add, "value") ?? "";
                if (!string.IsNullOrWhiteSpace(key))
                {
                    properties[key] = value;
                }
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new DiagnosticItem("Error", $"Properties resource is not valid XML: {ex.Message}", resolved));
        }
    }

    private static IReadOnlyDictionary<string, ProviderInfo> LoadProviderDefinitions(
        XDocument config,
        XNamespace ns,
        string configDir,
        IDictionary<string, string> properties,
        ICollection<DiagnosticItem> diagnostics)
    {
        var providers = new Dictionary<string, ProviderInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var providersElement in config.Descendants(ns + "providers"))
        {
            LoadProviderResource(Attr(providersElement, "resource"), configDir, properties, providers, diagnostics);
            foreach (var providerElement in providersElement.Elements().Where(x => x.Name.LocalName.Equals("provider", StringComparison.OrdinalIgnoreCase)))
            {
                AddProviderDefinition(providerElement, providers);
            }
        }

        return providers;
    }

    private static void LoadProviderResource(
        string? resource,
        string configDir,
        IDictionary<string, string> properties,
        IDictionary<string, ProviderInfo> providers,
        ICollection<DiagnosticItem> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(resource))
        {
            return;
        }

        var resolved = ResolveConfigRelativePath(resource, configDir, properties, diagnostics, null);
        if (!File.Exists(resolved))
        {
            diagnostics.Add(new DiagnosticItem("Warning", "Providers resource was not found.", resolved));
            return;
        }

        try
        {
            var doc = LoadSafeXDocument(resolved, LoadOptions.SetLineInfo);
            foreach (var providerElement in doc.Descendants().Where(x => x.Name.LocalName.Equals("provider", StringComparison.OrdinalIgnoreCase)))
            {
                AddProviderDefinition(providerElement, providers);
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add(new DiagnosticItem("Error", $"Providers resource is not valid XML: {ex.Message}", resolved));
        }
    }

    private static void AddProviderDefinition(XElement providerElement, IDictionary<string, ProviderInfo> providers)
    {
        var name = Attr(providerElement, "name") ?? "";
        var connectionClass = Attr(providerElement, "connectionClass") ?? "";
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(connectionClass))
        {
            return;
        }

        providers[name] = new ProviderInfo(
            name,
            Attr(providerElement, "assemblyName") ?? "",
            connectionClass.Trim(),
            Attr(providerElement, "commandClass") ?? "",
            Attr(providerElement, "parameterPrefix") ?? "@",
            IsTrue(Attr(providerElement, "usePositionalParameters")),
            !IsFalse(Attr(providerElement, "useParameterPrefixInSql")),
            !IsFalse(Attr(providerElement, "useParameterPrefixInParameter")));
    }

    private static void AnalyzeSqlMapElement(
        XElement sqlMap,
        string configDir,
        string root,
        IDictionary<string, string> properties,
        bool useStatementNamespaces,
        ICollection<SqlMapFileItem> sqlMapFiles,
        ICollection<StatementItem> statements,
        IDictionary<string, string> sqlFragments,
        ICollection<DiagnosticItem> diagnostics)
    {
        var embedded = Attr(sqlMap, "embedded");
        if (!string.IsNullOrWhiteSpace(embedded))
        {
            sqlMapFiles.Add(new SqlMapFileItem("embedded", embedded, "", 0, "Not loaded"));
            diagnostics.Add(new DiagnosticItem("Info", "Embedded sqlMap entries are listed but not loaded by this XML-only analyzer.", null, embedded));
            return;
        }

        var resource = Attr(sqlMap, "resource") ?? Attr(sqlMap, "url");
        if (string.IsNullOrWhiteSpace(resource))
        {
            diagnostics.Add(new DiagnosticItem("Warning", "sqlMap entry has no resource, url, or embedded attribute.", null, Line(sqlMap)));
            return;
        }

        var resolved = ResolvePath(resource, configDir, root, properties, diagnostics, null);
        if (!File.Exists(resolved))
        {
            sqlMapFiles.Add(new SqlMapFileItem("resource", resource, resolved, 0, "Missing"));
            diagnostics.Add(new DiagnosticItem("Error", "SQL map resource was not found.", resolved));
            return;
        }

        try
        {
            var doc = LoadSafeXDocument(resolved, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            var mapNamespace = Attr(doc.Root, "namespace");
            var fileStatements = new List<StatementItem>();

            var localSqlFragments = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var sql in doc.Descendants().Where(x => x.Name.LocalName.Equals("sql", StringComparison.OrdinalIgnoreCase)))
            {
                var id = QualifyId(Attr(sql, "id") ?? "", mapNamespace, useStatementNamespaces);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    localSqlFragments[id] = sql;
                    sqlFragments[id] = InnerXml(sql).Trim();
                }
            }

            var resultMaps = doc.Descendants().Where(x => x.Name.LocalName == "resultMap")
                .Select(x => QualifyId(Attr(x, "id") ?? "", mapNamespace, useStatementNamespaces))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var parameterMaps = doc.Descendants().Where(x => x.Name.LocalName == "parameterMap")
                .Select(x => QualifyId(Attr(x, "id") ?? "", mapNamespace, useStatementNamespaces))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var cacheModel in doc.Descendants().Where(x => x.Name.LocalName.Equals("cacheModel", StringComparison.OrdinalIgnoreCase)))
            {
                var cacheId = QualifyId(Attr(cacheModel, "id") ?? "", mapNamespace, useStatementNamespaces);
                if (IsTrue(Attr(cacheModel, "serialize")) && IsFalse(Attr(cacheModel, "readOnly")))
                {
                    diagnostics.Add(new DiagnosticItem(
                        "Security",
                        "Serializable read-write cache cloning can deserialize cached payloads. Avoid serialize=\"true\" with readOnly=\"false\" on legacy targets, or keep cache data fully trusted.",
                        resolved,
                        string.IsNullOrWhiteSpace(cacheId) ? Line(cacheModel) : cacheId));
                }
            }

            foreach (var element in doc.Descendants().Where(x => StatementElementNames.Contains(x.Name.LocalName)))
            {
                var id = QualifyId(Attr(element, "id") ?? "", mapNamespace, useStatementNamespaces);
                var resultMap = ResolveRef(Attr(element, "resultMap"), mapNamespace, useStatementNamespaces);
                var parameterMap = ResolveRef(Attr(element, "parameterMap"), mapNamespace, useStatementNamespaces);
                var template = InnerXmlWithExpandedIncludes(element, localSqlFragments, mapNamespace, useStatementNamespaces, diagnostics, resolved).Trim();
                var inlineSubstitutions = ExtractInlineSubstitutions(template);
                var includes = element.Descendants()
                    .Where(x => x.Name.LocalName.Equals("include", StringComparison.OrdinalIgnoreCase))
                    .Select(x => ResolveRef(Attr(x, "refid") ?? Attr(x, "refId") ?? "", mapNamespace, useStatementNamespaces))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                var item = new StatementItem(
                    id,
                    element.Name.LocalName,
                    resolved,
                    Attr(element, "parameterClass") ?? parameterMap,
                    Attr(element, "resultClass"),
                    resultMap,
                    template,
                    ExtractParameters(template),
                    inlineSubstitutions,
                    includes);

                fileStatements.Add(item);

                if (string.IsNullOrWhiteSpace(id))
                {
                    diagnostics.Add(new DiagnosticItem("Warning", "Statement is missing an id.", resolved, Line(element)));
                }

                if (!string.IsNullOrWhiteSpace(resultMap) && !resultMaps.Contains(resultMap))
                {
                    diagnostics.Add(new DiagnosticItem("Warning", $"resultMap '{resultMap}' was not found in the same map file.", resolved, id));
                }

                if (!string.IsNullOrWhiteSpace(parameterMap) && !parameterMaps.Contains(parameterMap))
                {
                    diagnostics.Add(new DiagnosticItem("Warning", $"parameterMap '{parameterMap}' was not found in the same map file.", resolved, id));
                }

                foreach (var substitution in inlineSubstitutions)
                {
                    diagnostics.Add(new DiagnosticItem(
                        "Security",
                        $"SQL injection risk: '${substitution}$' injects raw text into the SQL statement. Use '#{substitution}#' for values, or allow-list identifier/order-by values before using inline substitution.",
                        resolved,
                        string.IsNullOrWhiteSpace(id) ? Line(element) : id));
                }
            }

            foreach (var item in fileStatements)
            {
                statements.Add(item);
            }

            sqlMapFiles.Add(new SqlMapFileItem("resource", resource, resolved, fileStatements.Count, "Loaded"));

            if (fileStatements.Count == 0)
            {
                diagnostics.Add(new DiagnosticItem("Info", "SQL map loaded but contains no executable statements.", resolved));
            }
        }
        catch (Exception ex)
        {
            sqlMapFiles.Add(new SqlMapFileItem("resource", resource, resolved, 0, "Invalid XML"));
            diagnostics.Add(new DiagnosticItem("Error", $"SQL map is not valid XML: {ex.Message}", resolved));
        }
    }

    private static string ResolveValue(string value, IDictionary<string, string> properties, ICollection<DiagnosticItem> diagnostics, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return PlaceholderRegex().Replace(value, match =>
        {
            var key = match.Groups[1].Value;
            if (properties.TryGetValue(key, out var propertyValue))
            {
                return propertyValue;
            }

            diagnostics.Add(new DiagnosticItem("Warning", $"Unresolved property placeholder '{key}'.", filePath));
            return match.Value;
        });
    }

    private static string ResolvePath(
        string value,
        string configDir,
        string root,
        IDictionary<string, string> properties,
        ICollection<DiagnosticItem> diagnostics,
        string? filePath)
    {
        var resolvedValue = ResolveValue(value, properties, diagnostics, filePath);
        if (TryGetLocalFilePathFromUri(resolvedValue, out var localFilePath))
        {
            return NormalizePath(localFilePath);
        }

        if (IsRemoteUri(resolvedValue))
        {
            diagnostics.Add(new DiagnosticItem("Security", "Remote XML resource URIs are not loaded by the analyzer. Keep SqlMap/properties resources local and trusted.", resolvedValue, filePath));
            return resolvedValue;
        }

        var resolved = resolvedValue
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(resolved))
        {
            return NormalizePath(resolved);
        }

        var baseDir = resolved.StartsWith("." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? configDir
            : root;

        return NormalizePath(Path.Combine(baseDir, resolved));
    }

    private static string ResolveConfigRelativePath(
        string value,
        string configDir,
        IDictionary<string, string> properties,
        ICollection<DiagnosticItem> diagnostics,
        string? filePath)
    {
        var resolvedValue = ResolveValue(value, properties, diagnostics, filePath);
        if (TryGetLocalFilePathFromUri(resolvedValue, out var localFilePath))
        {
            return NormalizePath(localFilePath);
        }

        if (IsRemoteUri(resolvedValue))
        {
            diagnostics.Add(new DiagnosticItem("Security", "Remote XML resource URIs are not loaded by the analyzer. Keep SqlMap/properties resources local and trusted.", resolvedValue, filePath));
            return resolvedValue;
        }

        var resolved = resolvedValue
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (Path.IsPathRooted(resolved))
        {
            return NormalizePath(resolved);
        }

        return NormalizePath(Path.Combine(configDir, resolved));
    }

    private static IReadOnlyList<string> ExtractParameters(string sqlTemplate)
    {
        return ParameterRegex().Matches(sqlTemplate)
            .Where(x => x.Groups[1].Value == "#")
            .Select(x => ExtractTokenName(x.Groups[2].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static IReadOnlyList<string> ExtractInlineSubstitutions(string sqlTemplate)
    {
        return ParameterRegex().Matches(sqlTemplate)
            .Where(x => x.Groups[1].Value == "$")
            .Select(x => ExtractTokenName(x.Groups[2].Value))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    private static string ExtractTokenName(string token)
    {
        var name = token.Split(',', 2)[0].Trim();
        name = name.Split(':', 2)[0].Trim();
        if (name.EndsWith("[]", StringComparison.Ordinal))
        {
            name = name[..^2];
        }

        return string.IsNullOrWhiteSpace(name) || name == "[]" ? "value" : name;
    }

    private static XDocument LoadSafeXDocument(string path, LoadOptions options)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

        using var reader = XmlReader.Create(path, settings);
        return XDocument.Load(reader, options);
    }

    private static string InnerXml(XElement element)
    {
        var builder = new StringBuilder();
        foreach (var node in element.Nodes())
        {
            builder.AppendLine(node.ToString(SaveOptions.DisableFormatting));
        }

        return builder.ToString();
    }

    private static string InnerXmlWithExpandedIncludes(
        XElement element,
        IReadOnlyDictionary<string, XElement> sqlFragments,
        string? mapNamespace,
        bool useStatementNamespaces,
        ICollection<DiagnosticItem> diagnostics,
        string filePath)
    {
        var builder = new StringBuilder();
        AppendExpandedNodes(
            builder,
            element.Nodes(),
            sqlFragments,
            mapNamespace,
            useStatementNamespaces,
            diagnostics,
            filePath,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return builder.ToString();
    }

    private static void AppendExpandedNodes(
        StringBuilder builder,
        IEnumerable<XNode> nodes,
        IReadOnlyDictionary<string, XElement> sqlFragments,
        string? mapNamespace,
        bool useStatementNamespaces,
        ICollection<DiagnosticItem> diagnostics,
        string filePath,
        ISet<string> includeStack)
    {
        foreach (var node in nodes)
        {
            if (node is XElement element && element.Name.LocalName.Equals("include", StringComparison.OrdinalIgnoreCase))
            {
                var refId = ResolveRef(Attr(element, "refid") ?? Attr(element, "refId") ?? "", mapNamespace, useStatementNamespaces);
                if (string.IsNullOrWhiteSpace(refId) || !sqlFragments.TryGetValue(refId, out var fragment))
                {
                    builder.AppendLine(node.ToString(SaveOptions.DisableFormatting));
                    continue;
                }

                if (!includeStack.Add(refId))
                {
                    diagnostics.Add(new DiagnosticItem("Warning", $"Include refid '{refId}' is recursive and was not expanded.", filePath));
                    continue;
                }

                AppendExpandedNodes(builder, fragment.Nodes(), sqlFragments, mapNamespace, useStatementNamespaces, diagnostics, filePath, includeStack);
                includeStack.Remove(refId);
                continue;
            }

            builder.AppendLine(node.ToString(SaveOptions.DisableFormatting));
        }
    }

    private static string? Attr(XElement? element, string name)
    {
        return element?.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        }
        catch
        {
            return path;
        }
    }

    private static string QualifyId(string id, string? mapNamespace, bool useStatementNamespaces)
    {
        if (!useStatementNamespaces || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(mapNamespace) || id.Contains('.'))
        {
            return id;
        }

        return $"{mapNamespace}.{id}";
    }

    private static string ResolveRef(string? id, string? mapNamespace, bool useStatementNamespaces)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        return QualifyId(id, mapNamespace, useStatementNamespaces);
    }

    private static bool TryGetLocalFilePathFromUri(string value, out string localFilePath)
    {
        localFilePath = "";
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            localFilePath = uri.LocalPath;
            return true;
        }

        return false;
    }

    private static bool IsRemoteUri(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile;
    }

    private static void AddConfigurationSecurityDiagnostics(
        IReadOnlyDictionary<string, string> settingValues,
        string configPath,
        ICollection<DiagnosticItem> diagnostics)
    {
        if (settingValues.TryGetValue("useEmbedStatementParams", out var embedValue) && IsTrue(embedValue))
        {
            diagnostics.Add(new DiagnosticItem(
                "Security",
                "useEmbedStatementParams=true embeds parameter values into SQL text. Prefer prepared #...# parameters and leave this disabled.",
                configPath,
                "settings"));
        }

        if (settingValues.TryGetValue("allowInlineSqlParameters", out var inlineValue) && IsTrue(inlineValue))
        {
            diagnostics.Add(new DiagnosticItem(
                "Security",
                "allowInlineSqlParameters=true allows raw $...$ SQL substitutions. Use it only with allow-listed identifiers, never with request/user values.",
                configPath,
                "settings"));
        }
    }

    private static bool IsTrue(string? value)
    {
        return bool.TryParse(value, out var result)
            ? result
            : string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFalse(string? value)
    {
        return bool.TryParse(value, out var result) && !result;
    }

    private static string Line(XElement element)
    {
        if (element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            return $"line {lineInfo.LineNumber}";
        }

        return "";
    }

    private static string MaskConnectionString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var parts = value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part =>
            {
                var split = part.Split('=', 2);
                if (split.Length != 2)
                {
                    return part;
                }

                var key = split[0].Trim();
                if (key.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("user id", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("uid", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("user", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("username", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                    key.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("key", StringComparison.OrdinalIgnoreCase) ||
                    key.EndsWith(" key", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{key}=***";
                }

                return part;
            });

        return string.Join("; ", parts);
    }

    [GeneratedRegex(@"\$\{([^}]+)\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"([#$])([^#$]+)\1")]
    private static partial Regex ParameterRegex();
}
