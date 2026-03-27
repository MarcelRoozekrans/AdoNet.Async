using System.Data.Async.Converters;
using System.Data.Async.Converters.SystemTextJson;
using System.Data.Async.DataSet;
using BenchmarkDotNet.Attributes;
using Newtonsoft.Json;
using STJ = System.Text.Json;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public sealed class SerializationBenchmarks : IDisposable
{
    private AsyncDataTable _table = null!;
    private string _newtonsoftJson = null!;
    private string _stjJson = null!;
    private JsonSerializerSettings _newtonsoftSettings = null!;
    private STJ.JsonSerializerOptions _stjOptions = null!;

    [Params(10, 100)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _newtonsoftSettings = new JsonSerializerSettings();
        _newtonsoftSettings.Converters.Add(new AsyncDataTableConverter());

        _stjOptions = new STJ.JsonSerializerOptions();
        _stjOptions.Converters.Add(new AsyncDataTableJsonConverter());

        _table = new AsyncDataTable("Orders");
        _table.Columns.Add("Id", typeof(int));
        _table.Columns.Add("Product", typeof(string));
        _table.Columns.Add("Quantity", typeof(int));
        _table.Columns.Add("Price", typeof(decimal));
        _table.Columns.Add("OrderDate", typeof(DateTime));
        _table.Columns.Add("IsActive", typeof(bool));

        var baseDate = DateTime.UtcNow;
        for (int i = 1; i <= RowCount; i++)
        {
            var row = _table.NewRow();
            row["Id"] = i;
            row["Product"] = $"Product{i}";
            row["Quantity"] = i % 10 + 1;
            row["Price"] = (decimal)(i * 9.99);
            row["OrderDate"] = baseDate.AddDays(-i);
            row["IsActive"] = i % 3 != 0;
            _table.Rows.Add(row);
        }
        _table.AcceptChanges();

        _newtonsoftJson = JsonConvert.SerializeObject(_table, _newtonsoftSettings);
        _stjJson = STJ.JsonSerializer.Serialize(_table, _stjOptions);
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Dispose();

    public void Dispose() => _table?.Dispose();

    [Benchmark(Baseline = true)]
    public string Newtonsoft_Serialize() =>
        JsonConvert.SerializeObject(_table, _newtonsoftSettings);

    [Benchmark]
    public string STJ_Serialize() =>
        STJ.JsonSerializer.Serialize(_table, _stjOptions);

    [Benchmark]
    public AsyncDataTable? Newtonsoft_Deserialize() =>
        JsonConvert.DeserializeObject<AsyncDataTable>(_newtonsoftJson, _newtonsoftSettings);

    [Benchmark]
    public AsyncDataTable? STJ_Deserialize() =>
        STJ.JsonSerializer.Deserialize<AsyncDataTable>(_stjJson, _stjOptions);
}
