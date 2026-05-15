# xDev.IBatisNet

Compatibility-first iBATIS.NET fork for xDev-maintained legacy systems.

This repository is a maintained fork of `beginor/iBATIS_2010`, which is based on
Apache iBATIS.NET DataMapper 1.6.2 and updated for .NET Framework 4.0.

The goal is deliberately practical: keep old HIS SQL map files,
configuration files, and `IBatisNet.*` APIs working while giving the project a
place to receive fixes, packaging, and modernization work.

## Compatibility Contract

- Keep assembly names such as `IBatisNet.Common` and `IBatisNet.DataMapper`
  unless a migration plan says otherwise.
- Keep namespaces under `IBatisNet.*` for source compatibility.
- Keep XML formats compatible with existing `SqlMap.config`, `providers.config`,
  `SQLMaps/*.xml`, and `OracleMaps/*.xml` files.
- Prefer small, testable fixes over broad rewrites.
- Document behavior changes before consuming this fork from HIS applications.

## Current Baseline

- Source baseline: iBATIS.NET DataMapper 1.6.2.
- Framework target: .NET Framework 4.0.
- Known upstream note: original fork was only tested with MSSQL SqlClient.
- HIS target: replace the legacy `IBatisNet` NuGet/package reference
  without forcing application-level SQL map changes.

## Build

Legacy Visual Studio 2010 solution:

```powershell
DataMapper.2010.sln
```

.NET 10 SDK-style solution:

```powershell
dotnet build xDev.IBatisNet.slnx -c Release
```

Expected .NET 10 release assemblies:

- `IBatisNet.Common\bin\Release\net10.0\IBatisNet.Common.dll`
- `IBatisNet.DataMapper\bin\Release\net10.0\IBatisNet.DataMapper.dll`
- `IBatisNet.Common.Logging.Log4Net\bin\Release\net10.0\IBatisNet.Common.Logging.Log4Net.dll`

## .NET 10 Notes

- `System.Web` session stores are excluded from the .NET 10 build.
- Remoting `CallContext` is replaced with an `AsyncLocal` session store.
- COM+ `System.EnterpriseServices` transactions are replaced with
  `System.Transactions.TransactionScope`.
- Serializable cache cloning uses `DataContractSerializer` on .NET 10 instead
  of `BinaryFormatter`.

## Packaging

`xDev.IBatisNet.nuspec` is the internal package definition for the fork.
The package name is new, but the contained assemblies intentionally keep their
legacy names.

## Original Fork Notes

The inherited `iBATIS_2010` changes included:

- Update to Castle.DynamicProxy in Castle.Core.dll v3.1.0 (.NET 4.0 profile).
- Update to log4net v1.2.11.0 (.NET 4.0 profile).
- Rewrite `IBatisNet.Common.Utilities.Proxy.CachedProxyGenerator`.
- Small modifications to `IBatisNet.DataMapper.Test.2010`.
