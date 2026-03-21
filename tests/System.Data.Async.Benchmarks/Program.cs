using System.Data.Async.Benchmarks.Infrastructure;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var config = ManualConfig.CreateMinimumViable()
    .AddExporter(new AsyncParityExporter());

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
