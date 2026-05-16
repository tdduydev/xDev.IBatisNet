namespace XDev.IBatisNet.IntegrationPerf;

internal sealed record PerfSample(TimeSpan Elapsed, long AllocatedBytes);

internal sealed record PerfSummary(
    int Count,
    TimeSpan Min,
    TimeSpan Mean,
    TimeSpan P50,
    TimeSpan P95,
    TimeSpan Max,
    long MeanAllocatedBytes)
{
    public static PerfSummary Create(IReadOnlyList<PerfSample> samples)
    {
        var ordered = samples.Select(x => x.Elapsed).OrderBy(x => x).ToArray();
        var meanTicks = (long)samples.Average(x => x.Elapsed.Ticks);
        var meanAllocatedBytes = (long)samples.Average(x => x.AllocatedBytes);

        return new PerfSummary(
            samples.Count,
            ordered.First(),
            TimeSpan.FromTicks(meanTicks),
            Percentile(ordered, 0.50),
            Percentile(ordered, 0.95),
            ordered.Last(),
            meanAllocatedBytes);
    }

    public string ToConsoleText()
    {
        return string.Join(Environment.NewLine, new[]
        {
            $"Count: {Count}",
            $"Min: {Format(Min)}",
            $"Mean: {Format(Mean)}",
            $"P50: {Format(P50)}",
            $"P95: {Format(P95)}",
            $"Max: {Format(Max)}",
            $"Mean allocated: {MeanAllocatedBytes / 1024.0:N1} KB"
        });
    }

    public string ToMarkdown(string configPath, PerfOptions options)
    {
        return $"""
        # xDev.IBatisNet Integration Performance

        - Config: `{configPath}`
        - Warmup iterations: `{options.WarmupIterations}`
        - Measured iterations: `{options.Iterations}`
        - P95 threshold: `{(options.MaxP95Milliseconds > 0 ? options.MaxP95Milliseconds.ToString("N2") + " ms" : "not enforced")}`

        | Metric | Value |
        | --- | ---: |
        | Count | {Count} |
        | Min | {Format(Min)} |
        | Mean | {Format(Mean)} |
        | P50 | {Format(P50)} |
        | P95 | {Format(P95)} |
        | Max | {Format(Max)} |
        | Mean allocated | {MeanAllocatedBytes / 1024.0:N1} KB |
        """;
    }

    private static TimeSpan Percentile(TimeSpan[] ordered, double percentile)
    {
        if (ordered.Length == 0)
        {
            return TimeSpan.Zero;
        }

        var index = (int)Math.Ceiling(percentile * ordered.Length) - 1;
        return ordered[Math.Clamp(index, 0, ordered.Length - 1)];
    }

    private static string Format(TimeSpan value)
    {
        return value.TotalMilliseconds < 1000
            ? $"{value.TotalMilliseconds:N2} ms"
            : $"{value.TotalSeconds:N2} s";
    }
}
