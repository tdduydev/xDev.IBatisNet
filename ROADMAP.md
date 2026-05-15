# xDev.IBatisNet Roadmap

## Phase 1 - Stabilize The Fork

- Build the inherited VS2010 solution reproducibly on a current Windows machine.
- Keep the .NET 10 SDK-style solution building with zero warnings.
- Produce a versioned internal NuGet package.
- Compare generated binaries against the `IBatisNet.1.0.0` package currently
  used by HIS deployments.
- Run a smoke test with representative HIS `SqlMap.config` and SQL map XML
  files.

## Phase 2 - HIS Compatibility

- Add tests for dynamic SQL tags used by HIS maps.
- Add tests for SQL Server and Oracle provider configuration paths.
- Verify logging behavior with the log4net version used by HIS.
- Smoke test real HIS `SqlMap.config`, `providers.config`, and representative
  SQL map XML files on .NET 10.
- Document binding redirect requirements, if any.

## Phase 3 - Maintenance Fixes

- Fix compatibility bugs found while replacing the legacy package.
- Reduce reflection/proxy runtime failures on newer Windows/.NET Framework
  installs.
- Add packaging and build automation that does not require Visual Studio 2010.

## Phase 4 - Optional Modernization

- Evaluate multi-targeting after the legacy package is stable.
- Consider a separate modern package only if it can coexist with the legacy
  `IBatisNet.*` assembly identity.
- Keep migration guides explicit and reversible.
