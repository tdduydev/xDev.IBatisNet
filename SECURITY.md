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

## Auditing Existing Maps

Run the XML debugger against an application's `SqlMap.config`. It flags raw
`$...$` substitutions as `Security` diagnostics and lists the affected
statements so they can be migrated or allow-listed deliberately.
