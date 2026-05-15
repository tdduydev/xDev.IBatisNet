# xDev.IBatisNet

Compatibility-first iBATIS.NET package for xDev-maintained legacy systems.

This repository is an xDev-maintained continuation of Apache iBATIS.NET
DataMapper 1.6.2 for current xDev systems.

The goal is deliberately practical: keep old SQL map files,
configuration files, and `IBatisNet.*` APIs working while giving the project a
place to receive fixes, packaging, and modernization work.

## Compatibility Contract

- Keep assembly names such as `IBatisNet.Common` and `IBatisNet.DataMapper`
  unless a migration plan says otherwise.
- Keep namespaces under `IBatisNet.*` for source compatibility.
- Keep XML formats compatible with existing `SqlMap.config`, `providers.config`,
  `SQLMaps/*.xml`, and `OracleMaps/*.xml` files.
- Prefer small, testable fixes over broad rewrites.
- Document behavior changes before consuming this package from existing
  applications.

## Current Baseline

- Source baseline: iBATIS.NET DataMapper 1.6.2.
- Legacy package assets: .NET Framework 4.0, 4.5.2, 4.7.2, and 4.8.
- Modern package assets: .NET 10.
- Known baseline note: the inherited code path was only tested with MSSQL
  SqlClient before xDev maintenance.
- Compatibility target: replace the legacy `IBatisNet` NuGet/package reference
  without forcing application-level SQL map changes.

## Build

Legacy Visual Studio 2010 solution:

```powershell
DataMapper.2010.sln
```

Visual Studio 2003 project files are preserved with the `.vs2003proj`
extension so modern dependency scanners do not try to restore them as MSBuild
projects.

.NET 10 SDK-style solution:

```powershell
dotnet build xDev.IBatisNet.slnx -c Release
```

Expected .NET 10 release assemblies:

- `IBatisNet.Common\bin\Release\net10.0\IBatisNet.Common.dll`
- `IBatisNet.DataMapper\bin\Release\net10.0\IBatisNet.DataMapper.dll`
- `IBatisNet.Common.Logging.Log4Net\bin\Release\net10.0\IBatisNet.Common.Logging.Log4Net.dll`

CI builds the legacy solution as a .NET Framework 4.0 compatibility asset and
packs it under `net40`, `net452`, `net472`, and `net48` so existing
projects can install the package without retargeting first.
The legacy package assets intentionally mirror the old package surface:
`IBatisNet.Common` and `IBatisNet.DataMapper`. They do not pull `log4net`, so
existing application logging packages remain under the host application's
control.

## .NET 10 Notes

- `System.Web` session stores are excluded from the .NET 10 build.
- Remoting `CallContext` is replaced with an `AsyncLocal` session store.
- COM+ `System.EnterpriseServices` transactions are replaced with
  `System.Transactions.TransactionScope`.
- Serializable cache cloning uses `DataContractSerializer` on .NET 10 instead
  of `BinaryFormatter`.

## Packaging

`xDev.IBatisNet.nuspec` is the internal package definition.
The package name is new, but the contained assemblies intentionally keep their
legacy names.

GitHub Actions:

- `CI` builds the .NET 10 solution and the legacy .NET Framework compatibility
  solution.
- `CI` automatically packs and publishes to GitHub Packages on pushes to
  `master` using an auto-generated prerelease version.
- `CI` packs and publishes to NuGet.org and GitHub Packages when a `v*` tag is
  pushed.
- `CI` packs and publishes to the NuGet test gallery when a `test-v*` tag is
  pushed. The same package is also published to GitHub Packages.
- Manual workflow runs can publish to GitHub Packages, NuGet test, NuGet.org, or
  both a NuGet feed and GitHub Packages. Leave the package version blank to use
  the auto-generated version.
- NuGet.org publishing requires a repository secret named `NUGET_API_KEY`.
- NuGet test publishing requires a repository secret named `NUGET_TEST_API_KEY`.
- GitHub Packages publishing uses the workflow `GITHUB_TOKEN`.

## Baseline Notes

The inherited legacy changes included:

- Update to Castle.DynamicProxy in Castle.Core.dll v3.1.0 (.NET 4.0 profile).
- Update to log4net v1.2.11.0 (.NET 4.0 profile).
- Rewrite `IBatisNet.Common.Utilities.Proxy.CachedProxyGenerator`.
- Small modifications to `IBatisNet.DataMapper.Test.2010`.
