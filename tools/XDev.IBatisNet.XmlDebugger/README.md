# xDev iBATIS XML Debugger

Avalonia desktop tool for inspecting iBATIS.NET `SqlMap.config` and SQL map XML
files without starting the host application.

## What It Checks

- Missing `SqlMap.config`, `properties`, and `sqlMap` resources.
- Invalid XML in the root config and loaded SQL map files.
- Unresolved `${...}` placeholders.
- Duplicate statement ids across loaded maps.
- Missing local `resultMap`, `parameterMap`, and `<include refid="...">`
  references.
- SQL injection risk from `$...$` inline substitutions.
- XML files are parsed with DTD/external entity resolution disabled.
- Statement inventory for `select`, `insert`, `update`, `delete`, `statement`,
  and `procedure` nodes.
- Parameter tokens from safe `#...#` values and raw `$...$` substitutions.
- SQL preview that expands local `<include>` fragments, evaluates common dynamic
  tags from sample parameters, and converts `#...#` tokens into provider
  parameters.
- SQL export for the selected statement or the whole loaded map inventory.
- Read-only explain-plan execution for supported SELECT/WITH statements.

By default the debugger only reads XML. Explain-plan execution is opt-in, uses
the configured provider metadata, and blocks mutating statements such as
`insert`, `update`, and `delete`.

## Run Locally

```powershell
dotnet run --project tools\XDev.IBatisNet.XmlDebugger\XDev.IBatisNet.XmlDebugger.csproj -c Release
```

Choose the application's `SqlMap.config`, or choose one SQL map XML file
directly for a single-file inspection. Optionally choose a working root, then
press `Analyze`.

## SQL Preview And Explain Plan

After analysis, choose a statement and open the `SQL Preview` tab. The sample
parameter box accepts `Name=value` lines. Those values decide which common
dynamic tags are included and become DbParameter values when the preview SQL is
planned.

The `Explain Plan` tab uses the `dataSource` connection string from
`SqlMap.config` unless a connection override is supplied. The debugger currently
generates explain-plan SQL for SQL Server, PostgreSQL, MySQL, and SQLite-style
providers when their connection assembly is available to the process.

## Publish A Portable Windows Build

```powershell
dotnet publish tools\XDev.IBatisNet.XmlDebugger\XDev.IBatisNet.XmlDebugger.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true `
  -o artifacts\xml-debugger\win-x64
```

The GitHub Actions workflow `XML Debugger Desktop` builds installable/portable
artifacts for Windows x64, Windows ARM64, macOS Intel, macOS Apple Silicon, and
Linux x64/ARM64 on pushes to `master`/`codex/**`, package tags (`v*` and
`test-v*`), desktop-only tags (`xml-debugger-v*`), and manual dispatches.
Each run uploads generated release notes. Tag builds also create or update a
GitHub Release and attach the desktop assets.
