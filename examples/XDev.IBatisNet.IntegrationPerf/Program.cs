using System.Diagnostics;
using IBatisNet.DataMapper;
using IBatisNet.DataMapper.Configuration;

namespace XDev.IBatisNet.IntegrationPerf;

internal static class Program
{
    private static int Main(string[] args)
    {
        var options = PerfOptions.Parse(args);
        var configPath = Path.GetFullPath(options.ConfigPath ?? Path.Combine(AppContext.BaseDirectory, "SqlMap.config"));

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"SqlMap.config was not found: {configPath}");
            return 2;
        }

        Console.WriteLine("xDev.IBatisNet integration performance smoke");
        Console.WriteLine($"Config: {configPath}");
        Console.WriteLine($"Warmup: {options.WarmupIterations}, iterations: {options.Iterations}");

        for (var i = 0; i < options.WarmupIterations; i++)
        {
            BuildMapper(configPath);
        }

        var samples = new List<PerfSample>(options.Iterations);
        for (var i = 0; i < options.Iterations; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var mapper = BuildMapper(configPath);
            stopwatch.Stop();

            if (mapper is null)
            {
                Console.Error.WriteLine("DomSqlMapBuilder returned null.");
                return 3;
            }

            samples.Add(new PerfSample(stopwatch.Elapsed, GC.GetAllocatedBytesForCurrentThread() - beforeAllocated));
        }

        var summary = PerfSummary.Create(samples);
        Console.WriteLine(summary.ToConsoleText());

        if (!string.IsNullOrWhiteSpace(options.ReportPath))
        {
            var reportPath = Path.GetFullPath(options.ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath) ?? Environment.CurrentDirectory);
            File.WriteAllText(reportPath, summary.ToMarkdown(configPath, options));
            Console.WriteLine($"Report: {reportPath}");
        }

        if (options.MaxP95Milliseconds > 0 && summary.P95.TotalMilliseconds > options.MaxP95Milliseconds)
        {
            Console.Error.WriteLine($"P95 {summary.P95.TotalMilliseconds:N2} ms exceeded threshold {options.MaxP95Milliseconds:N2} ms.");
            return 4;
        }

        return 0;
    }

    private static ISqlMapper BuildMapper(string configPath)
    {
        var builder = new DomSqlMapBuilder
        {
            ValidateSqlMapConfig = false
        };

        return builder.Configure(new FileInfo(configPath));
    }
}
