# xDev.IBatisNet Integration Performance Example

Console smoke project for checking that the .NET 10 DataMapper can load a real
`SqlMap.config`, provider definition, aliases, result maps, includes, and
statement parameter mappings.

The default configuration uses a fake ADO.NET provider. It exercises iBATIS.NET
configuration and SQL map parsing without requiring a live database.

## Run

```powershell
dotnet run --project examples\XDev.IBatisNet.IntegrationPerf\XDev.IBatisNet.IntegrationPerf.csproj -c Release -- --iterations 20 --warmup 3 --report artifacts\integration-perf.md
```

Useful switches:

- `--config <path>`: use another `SqlMap.config`.
- `--iterations <n>`: measured mapper build iterations.
- `--warmup <n>`: warmup iterations before measuring.
- `--max-p95-ms <n>`: fail the process if P95 mapper build time exceeds the threshold.
- `--report <path>`: write a markdown report.
