using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XDev.IBatisNet.XmlDebugger.Models;

namespace XDev.IBatisNet.XmlDebugger.Services;

public sealed partial class SqlPreviewService
{
    public SqlPreviewResult Render(StatementItem? statement, string parameterInput, ProviderInfo provider)
    {
        if (statement is null)
        {
            return new SqlPreviewResult("", [], ["Choose a statement to preview SQL."]);
        }

        var messages = new List<string>();
        var values = ParseParameterInput(parameterInput);
        var sql = RenderTemplate(statement.SqlTemplate, values, messages);
        sql = NormalizeSql(sql);
        var parameters = new List<SqlCommandParameter>();
        var preparedSql = ReplaceTokens(sql, values, provider, parameters, messages);

        return new SqlPreviewResult(preparedSql, parameters, messages);
    }

    public static IReadOnlyDictionary<string, ParsedParameterValue> ParseParameterInput(string input)
    {
        var values = new Dictionary<string, ParsedParameterValue>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input))
        {
            return values;
        }

        foreach (var rawLine in input.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var split = line.Split('=', 2);
            var name = split[0].Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var rawValue = split.Length == 2 ? split[1].Trim() : "";
            values[name] = new ParsedParameterValue(name, ParseScalar(rawValue), rawValue);
        }

        return values;
    }

    public static string BuildDefaultParameterInput(StatementItem? statement)
    {
        if (statement is null || statement.Parameters.Count == 0)
        {
            return "";
        }

        return string.Join(Environment.NewLine, statement.Parameters.Select(x => $"{x}="));
    }

    private static string RenderTemplate(
        string template,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ICollection<string> messages)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return "";
        }

        try
        {
            var document = XDocument.Parse($"<root>{template}</root>", LoadOptions.PreserveWhitespace);
            return RenderNodes(document.Root?.Nodes() ?? [], values, messages);
        }
        catch (Exception ex)
        {
            messages.Add($"Could not parse dynamic SQL XML; showing raw template. {ex.Message}");
            return template;
        }
    }

    private static string RenderNodes(
        IEnumerable<XNode> nodes,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ICollection<string> messages)
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            builder.Append(RenderNode(node, values, messages));
        }

        return builder.ToString();
    }

    private static string RenderNode(
        XNode node,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ICollection<string> messages)
    {
        return node switch
        {
            XCData cdata => cdata.Value,
            XText text => text.Value,
            XElement element => RenderElement(element, values, messages),
            _ => ""
        };
    }

    private static string RenderElement(
        XElement element,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ICollection<string> messages)
    {
        var name = element.Name.LocalName;
        if (name.Equals("selectKey", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        if (name.Equals("include", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("An include tag could not be expanded in the preview.");
            return "";
        }

        if (name.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
        {
            return RenderDynamicElement(element, values, messages);
        }

        if (IsConditionalTag(name) && !EvaluateCondition(element, values))
        {
            return "";
        }

        if (name.Equals("iterate", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add("Iterate tags are shown as a single representative SQL fragment.");
        }

        return AddPrepend(element, RenderNodes(element.Nodes(), values, messages));
    }

    private static string RenderDynamicElement(
        XElement element,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ICollection<string> messages)
    {
        var segments = new List<DynamicSegment>();
        foreach (var node in element.Nodes())
        {
            if (node is XElement child)
            {
                var childName = child.Name.LocalName;
                if (IsConditionalTag(childName) && !EvaluateCondition(child, values))
                {
                    continue;
                }

                var text = childName.Equals("dynamic", StringComparison.OrdinalIgnoreCase)
                    ? RenderDynamicElement(child, values, messages)
                    : RenderNodes(child.Nodes(), values, messages);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new DynamicSegment(Attr(child, "prepend") ?? "", text));
                }

                continue;
            }

            var rendered = RenderNode(node, values, messages);
            if (!string.IsNullOrWhiteSpace(rendered))
            {
                segments.Add(new DynamicSegment("", rendered));
            }
        }

        if (segments.Count == 0)
        {
            return "";
        }

        var builder = new StringBuilder();
        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];
            var prepend = index == 0 ? Attr(element, "prepend") ?? segment.Prepend : segment.Prepend;
            AppendSegment(builder, prepend, segment.Sql);
        }

        return builder.ToString();
    }

    private static string AddPrepend(XElement element, string sql)
    {
        return string.IsNullOrWhiteSpace(sql) ? "" : JoinPrepend(Attr(element, "prepend"), sql);
    }

    private static void AppendSegment(StringBuilder builder, string? prepend, string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return;
        }

        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(JoinPrepend(prepend, sql));
    }

    private static string JoinPrepend(string? prepend, string sql)
    {
        if (string.IsNullOrWhiteSpace(prepend))
        {
            return sql;
        }

        return $"{prepend.Trim()} {sql.TrimStart()}";
    }

    private static bool EvaluateCondition(XElement element, IReadOnlyDictionary<string, ParsedParameterValue> values)
    {
        var name = element.Name.LocalName;
        var property = Attr(element, "property");
        ParsedParameterValue? value = null;
        var hasValue = !string.IsNullOrWhiteSpace(property) && values.TryGetValue(property, out value);
        var raw = hasValue ? value!.RawValue : "";

        return name.ToLowerInvariant() switch
        {
            "isnotempty" => hasValue && !string.IsNullOrWhiteSpace(raw) && value!.Value is not null,
            "isempty" => !hasValue || string.IsNullOrWhiteSpace(raw) || value!.Value is null,
            "isnotnull" => hasValue && value!.Value is not null,
            "isnull" => !hasValue || value!.Value is null,
            "ispropertyavailable" => hasValue,
            "isnotpropertyavailable" => !hasValue,
            "isparameterpresent" => hasValue,
            "isnotparameterpresent" => !hasValue,
            "isequal" => hasValue && Compare(element, values, value!) == 0,
            "isnotequal" => hasValue && Compare(element, values, value!) != 0,
            "isgreaterthan" => hasValue && Compare(element, values, value!) > 0,
            "isgreaterequal" => hasValue && Compare(element, values, value!) >= 0,
            "islessthan" => hasValue && Compare(element, values, value!) < 0,
            "islessequal" => hasValue && Compare(element, values, value!) <= 0,
            _ => true
        };
    }

    private static int Compare(
        XElement element,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ParsedParameterValue current)
    {
        var compareValue = Attr(element, "compareValue");
        var compareProperty = Attr(element, "compareProperty");
        object? otherValue = compareValue;
        if (!string.IsNullOrWhiteSpace(compareProperty) && values.TryGetValue(compareProperty, out var compareParameter))
        {
            otherValue = compareParameter.Value;
        }

        if (TryConvertDecimal(current.Value, out var leftNumber) && TryConvertDecimal(otherValue, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(
            Convert.ToString(current.Value, CultureInfo.InvariantCulture),
            Convert.ToString(otherValue, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceTokens(
        string sql,
        IReadOnlyDictionary<string, ParsedParameterValue> values,
        ProviderInfo provider,
        ICollection<SqlCommandParameter> parameters,
        ICollection<string> messages)
    {
        var namedParameters = new Dictionary<string, SqlCommandParameter>(StringComparer.OrdinalIgnoreCase);
        var positionalIndex = 0;

        return ParameterRegex().Replace(sql, match =>
        {
            var tokenKind = match.Groups[1].Value;
            var token = match.Groups[2].Value;
            var name = GetParameterName(token);
            if (tokenKind == "$")
            {
                messages.Add($"Raw SQL substitution '${name}$' is inserted directly. Use only allow-listed values.");
                return values.TryGetValue(name, out var inlineValue) ? inlineValue.RawValue : $"{{{name}}}";
            }

            values.TryGetValue(name, out var value);
            var isMissing = value is null;
            if (isMissing)
            {
                messages.Add($"Parameter '{name}' has no sample value; DBNull will be used for explain-plan runs.");
            }

            var commandName = provider.UsePositionalParameters
                ? $"p{positionalIndex++}"
                : SanitizeParameterName(name);
            var sqlName = provider.UsePositionalParameters || !provider.UseParameterPrefixInSql
                ? "?"
                : $"{provider.ParameterPrefix}{commandName}";
            var parameterName = provider.UseParameterPrefixInParameter
                ? $"{provider.ParameterPrefix}{commandName}"
                : commandName;
            var parameter = new SqlCommandParameter(name, parameterName, value?.Value, value?.RawValue ?? "", isMissing);

            if (provider.UsePositionalParameters)
            {
                parameters.Add(parameter);
            }
            else if (!namedParameters.ContainsKey(name))
            {
                namedParameters[name] = parameter;
                parameters.Add(parameter);
            }

            return sqlName;
        });
    }

    private static string NormalizeSql(string sql)
    {
        var lines = sql
            .Replace("\r", "")
            .Split('\n')
            .Select(x => WhitespaceRegex().Replace(x.Trim(), " "))
            .Where(x => x.Length > 0);

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetParameterName(string token)
    {
        var name = token.Split(',', 2)[0].Trim();
        name = name.Split(':', 2)[0].Trim();
        if (name.EndsWith("[]", StringComparison.Ordinal))
        {
            name = name[..^2];
        }

        if (name.Length == 0 || name == "[]")
        {
            return "value";
        }

        return name;
    }

    private static string SanitizeParameterName(string name)
    {
        var builder = new StringBuilder();
        foreach (var ch in name)
        {
            builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        }

        var sanitized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "value" : sanitized;
    }

    private static object? ParseScalar(string value)
    {
        if (value.Equals("null", StringComparison.OrdinalIgnoreCase) || value.Equals("<null>", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if ((value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) ||
            (value.StartsWith("'", StringComparison.Ordinal) && value.EndsWith("'", StringComparison.Ordinal)))
        {
            return value[1..^1];
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
        {
            return dateTime;
        }

        return value;
    }

    private static bool TryConvertDecimal(object? value, out decimal result)
    {
        switch (value)
        {
            case decimal decimalValue:
                result = decimalValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case string stringValue:
                return decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
            default:
                result = 0;
                return false;
        }
    }

    private static bool IsConditionalTag(string name)
    {
        return name.Equals("isNotEmpty", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isEmpty", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isNotNull", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isNull", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isEqual", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isNotEqual", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isGreaterThan", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isGreaterEqual", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isLessThan", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isLessEqual", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isPropertyAvailable", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isNotPropertyAvailable", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isParameterPresent", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("isNotParameterPresent", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Attr(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(x => x.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private sealed record DynamicSegment(string Prepend, string Sql);

    public sealed record ParsedParameterValue(string Name, object? Value, string RawValue);

    [GeneratedRegex(@"([#$])([^#$]+)\1")]
    private static partial Regex ParameterRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
