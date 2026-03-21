using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Reports;

namespace System.Data.Async.Benchmarks.Infrastructure;

public sealed class AsyncParityExporter : IExporter
{
    public string Name => "AsyncParity";

    public void ExportToLog(Summary summary, ILogger logger)
    {
        logger.WriteLine();
        logger.WriteLine("# Async vs Raw Parity Summary");
        logger.WriteLine();
        logger.WriteLine("| Operation | Raw Mean (ns) | Async Mean (ns) | Delta % | Raw Alloc (B) | Async Alloc (B) | Alloc Delta (B) | Status |");
        logger.WriteLine("|-----------|---------------|-----------------|---------|---------------|-----------------|-----------------|--------|");

        var groups = summary.BenchmarksCases
            .GroupBy(b =>
            {
                var name = b.Descriptor.WorkloadMethod.Name;
                if (name.StartsWith("Raw_", StringComparison.Ordinal))
                    return name["Raw_".Length..];
                if (name.StartsWith("Async_", StringComparison.Ordinal))
                    return name["Async_".Length..];
                return name;
            }, StringComparer.Ordinal)
            .Where(g => g.Count() >= 2)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var rawCase = group.FirstOrDefault(b => b.Descriptor.WorkloadMethod.Name.StartsWith("Raw_", StringComparison.Ordinal));
            var asyncCase = group.FirstOrDefault(b => b.Descriptor.WorkloadMethod.Name.StartsWith("Async_", StringComparison.Ordinal));

            if (rawCase is null || asyncCase is null) continue;

            var rawReport = summary[rawCase];
            var asyncReport = summary[asyncCase];

            if (rawReport?.ResultStatistics is null || asyncReport?.ResultStatistics is null) continue;

            var rawMean = rawReport.ResultStatistics.Mean;
            var asyncMean = asyncReport.ResultStatistics.Mean;
            var deltaPct = ((asyncMean - rawMean) / rawMean) * 100;

            var rawAlloc = rawReport.GcStats.GetBytesAllocatedPerOperation(rawCase);
            var asyncAlloc = asyncReport.GcStats.GetBytesAllocatedPerOperation(asyncCase);
            var allocDelta = asyncAlloc - rawAlloc;

            var status = deltaPct > 20 ? "WARNING" : "OK";

            var paramSuffix = rawCase.HasParameters ? $" ({rawCase.Parameters.DisplayInfo})" : "";

            logger.WriteLine(
                $"| {group.Key}{paramSuffix} | {rawMean:N0} | {asyncMean:N0} | {deltaPct:+0.0;-0.0}% | {rawAlloc} | {asyncAlloc} | {allocDelta:+0;-0} | {status} |");
        }

        logger.WriteLine();
    }

    public IEnumerable<string> ExportToFiles(Summary summary, ILogger consoleLogger)
    {
        var filePath = Path.Combine(summary.ResultsDirectoryPath, "async-parity-summary.md");
        using var writer = new StreamWriter(filePath);
        var logger = new StreamWriterLogger(writer);
        ExportToLog(summary, logger);
        return [filePath];
    }

    private sealed class StreamWriterLogger(StreamWriter writer) : ILogger
    {
        public string Id => nameof(StreamWriterLogger);
        public int Priority => 0;

        public void Write(LogKind logKind, string text) => writer.Write(text);
        public void WriteLine() => writer.WriteLine();
        public void WriteLine(LogKind logKind, string text) => writer.WriteLine(text);
        public void Flush() => writer.Flush();
    }
}
