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
- Statement inventory for `select`, `insert`, `update`, `delete`, `statement`,
  and `procedure` nodes.
- Parameter tokens from `#...#` and `$...$`.

This debugger is XML-only by design. It does not execute SQL or open database
connections, so it is safe to run against production map folders for inspection.

## Run Locally

```powershell
dotnet run --project tools\XDev.IBatisNet.XmlDebugger\XDev.IBatisNet.XmlDebugger.csproj -c Release
```

Choose the application's `SqlMap.config`, optionally choose a working root, then
press `Analyze`.

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
Linux x64/ARM64.
