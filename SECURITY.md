# Security Notes

## SQL Injection

Use `#Property#` for values. iBATIS converts these tokens to database
parameters, so user input is bound through `IDbDataParameter`.

Avoid `$Property$` for user-controlled values. `$...$` performs raw text
substitution and can create SQL injection if the value comes from a request,
screen input, API payload, or untrusted integration.

If a legacy map needs `$...$` for identifiers such as `ORDER BY`, table names,
or column names, validate the value against an allow-list before calling the
mapper.

For hardened applications, disable raw inline substitution:

```xml
<settings>
  <setting allowInlineSqlParameters="false" />
</settings>
```

When disabled, configuration or execution fails if a statement uses `$...$`.
This is opt-in to preserve compatibility with existing iBATIS.NET maps.

## XML Config Loading

XML resource loading disables DTD and external entity resolution. This reduces
XXE and XML entity expansion risk when loading `SqlMap.config`,
`providers.config`, properties XML, and SQL map XML through the built-in
resource helpers.

Keep `providers.config`, `SqlMap.config`, SQL maps, and property files under
trusted deployment control. Provider classes, type aliases, type handlers, and
logging adapters are loaded by type name from configuration, so untrusted XML
configuration should be treated like untrusted code.

## Logging and Connection Strings

Debug logging no longer writes raw parameter values. Non-null values are logged
as shape metadata, sensitive parameter names are masked, and `DataSource`
diagnostics mask credentials in connection strings.

The stored procedure parameter cache also uses opaque hashed keys instead of
keeping raw connection strings as cache keys.

## Legacy Serializable Caches

On .NET 10, read-write serializable cache cloning uses
`DataContractSerializer`.

On legacy .NET Framework package assets, iBATIS.NET-compatible
`serialize="true"` plus `readOnly="false"` cache models still use the inherited
binary serialization behavior for compatibility. Avoid that combination unless
the cached data is fully trusted and process-local; prefer `readOnly="true"` or
non-serializable cache models for hardened deployments.

## Auditing Existing Maps

Run the XML debugger against an application's `SqlMap.config`. It flags raw
`$...$` substitutions as `Security` diagnostics and lists the affected
statements so they can be migrated or allow-listed deliberately. It also flags
risky compatibility settings and legacy serializable cache cloning patterns.
