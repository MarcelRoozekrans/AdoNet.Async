using System.Data.Async.DataSet;
using BenchmarkDotNet.Attributes;

namespace System.Data.Async.Benchmarks;

[MemoryDiagnoser]
[RankColumn]
public class TypedDataSetBenchmarks : IDisposable
{
    private AsyncOrdersDS _typedDs = null!;
    private AsyncDataSet _untypedDs = null!;
    private System.Data.DataTable _untypedCustomerTable = null!;

    [Params(10, 100)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _typedDs = new AsyncOrdersDS();
        for (int i = 0; i < RowCount; i++)
            await _typedDs.Customer.AddCustomerRowAsync(i, $"Customer_{i}", $"c{i}@example.com");

        _untypedDs = new AsyncDataSet("OrdersDS");
        var table = new AsyncDataTable("Customer");
        table.Columns.Add("CustomerId", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Email", typeof(string));
        table.PrimaryKey = [table.Columns["CustomerId"]!];
        System.Data.DataSet innerDs = _untypedDs;
        System.Data.DataTable innerDt = table;
        innerDs.Tables.Add(innerDt);
        for (int i = 0; i < RowCount; i++)
            await table.Rows.AddAsync(new object?[] { i, $"Customer_{i}", $"c{i}@example.com" });
        _untypedCustomerTable = innerDt;
    }

    [GlobalCleanup]
    public void GlobalCleanup() => Dispose();

    public void Dispose()
    {
        _typedDs?.Dispose();
        _untypedDs?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public string Untyped_ReadAll()
    {
        string last = "";
        for (int i = 0; i < _untypedCustomerTable.Rows.Count; i++)
            last = (string)_untypedCustomerTable.Rows[i]["Name"];
        return last;
    }

    [Benchmark]
    public string Typed_ReadAll()
    {
        string last = "";
        for (int i = 0; i < _typedDs.Customer.Count; i++)
            last = _typedDs.Customer[i].Name;
        return last;
    }

    [Benchmark]
    public AsyncCustomerRow? Typed_FindByPK()
    {
        return _typedDs.Customer.FindByCustomerId(RowCount / 2);
    }

    [Benchmark]
    public System.Data.DataRow? Untyped_FindByPK()
    {
        return _untypedCustomerTable.Rows.Find(RowCount / 2);
    }

    [Benchmark]
    public async Task<AsyncCustomerRow> Typed_AddRow()
    {
        var row = await _typedDs.Customer.AddCustomerRowAsync(RowCount + 1000, "New", "new@example.com");
        await _typedDs.Customer.RemoveCustomerRowAsync(row);
        return row;
    }

    [Benchmark]
    public async Task<System.Data.DataRow> Untyped_AddRow()
    {
        var row = _untypedCustomerTable.NewRow();
        row["CustomerId"] = RowCount + 1000;
        row["Name"] = "New";
        row["Email"] = "new@example.com";
        _untypedCustomerTable.Rows.Add(row);
        _untypedCustomerTable.Rows.Remove(row);
        await Task.CompletedTask;
        return row;
    }
}
