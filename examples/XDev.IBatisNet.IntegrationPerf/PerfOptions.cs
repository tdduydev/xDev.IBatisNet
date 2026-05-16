namespace XDev.IBatisNet.IntegrationPerf;

internal sealed class PerfOptions
{
    public string? ConfigPath { get; private init; }
    public string? ReportPath { get; private init; }
    public int Iterations { get; private init; } = 20;
    public int WarmupIterations { get; private init; } = 3;
    public double MaxP95Milliseconds { get; private init; }

    public static PerfOptions Parse(string[] args)
    {
        var options = new PerfOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var current = args[i];
            var next = i + 1 < args.Length ? args[i + 1] : null;

            switch (current)
            {
                case "--config" when next is not null:
                    options = options.WithConfig(next);
                    i++;
                    break;
                case "--report" when next is not null:
                    options = options.WithReport(next);
                    i++;
                    break;
                case "--iterations" when next is not null && int.TryParse(next, out var iterations):
                    options = options.WithIterations(Math.Max(1, iterations));
                    i++;
                    break;
                case "--warmup" when next is not null && int.TryParse(next, out var warmup):
                    options = options.WithWarmup(Math.Max(0, warmup));
                    i++;
                    break;
                case "--max-p95-ms" when next is not null && double.TryParse(next, out var maxP95):
                    options = options.WithMaxP95(Math.Max(0, maxP95));
                    i++;
                    break;
            }
        }

        return options;
    }

    private PerfOptions WithConfig(string value) => new()
    {
        ConfigPath = value,
        ReportPath = ReportPath,
        Iterations = Iterations,
        WarmupIterations = WarmupIterations,
        MaxP95Milliseconds = MaxP95Milliseconds
    };

    private PerfOptions WithReport(string value) => new()
    {
        ConfigPath = ConfigPath,
        ReportPath = value,
        Iterations = Iterations,
        WarmupIterations = WarmupIterations,
        MaxP95Milliseconds = MaxP95Milliseconds
    };

    private PerfOptions WithIterations(int value) => new()
    {
        ConfigPath = ConfigPath,
        ReportPath = ReportPath,
        Iterations = value,
        WarmupIterations = WarmupIterations,
        MaxP95Milliseconds = MaxP95Milliseconds
    };

    private PerfOptions WithWarmup(int value) => new()
    {
        ConfigPath = ConfigPath,
        ReportPath = ReportPath,
        Iterations = Iterations,
        WarmupIterations = value,
        MaxP95Milliseconds = MaxP95Milliseconds
    };

    private PerfOptions WithMaxP95(double value) => new()
    {
        ConfigPath = ConfigPath,
        ReportPath = ReportPath,
        Iterations = Iterations,
        WarmupIterations = WarmupIterations,
        MaxP95Milliseconds = value
    };
}
