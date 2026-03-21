using FluentAssertions;
using Xunit;

namespace System.Data.Async.Tests;

public class AsyncDbDataReaderTests
{
    private sealed class TestDbDataReader : AsyncDbDataReader
    {
        private readonly List<Dictionary<string, object>> _rows;
        private int _currentRow = -1;
        private bool _closed;

        public TestDbDataReader(List<Dictionary<string, object>> rows)
        {
            _rows = rows;
        }

        public override int FieldCount => _rows.Count > 0 ? _rows[0].Count : 0;
        public override int Depth => 0;
        public override bool IsClosed => _closed;
        public override int RecordsAffected => -1;
        public override bool HasRows => _rows.Count > 0;

        public override object this[int i] => _rows[_currentRow].Values.ElementAt(i);
        public override object this[string name] => _rows[_currentRow][name];

        public override bool GetBoolean(int i) => (bool)this[i];
        public override byte GetByte(int i) => (byte)this[i];
        public override long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int i) => (char)this[i];
        public override long GetChars(int i, long fieldOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override Guid GetGuid(int i) => (Guid)this[i];
        public override short GetInt16(int i) => (short)this[i];
        public override int GetInt32(int i) => (int)this[i];
        public override long GetInt64(int i) => (long)this[i];
        public override float GetFloat(int i) => (float)this[i];
        public override double GetDouble(int i) => (double)this[i];
        public override string GetString(int i) => (string)this[i];
        public override decimal GetDecimal(int i) => (decimal)this[i];
        public override DateTime GetDateTime(int i) => (DateTime)this[i];
        public override IDataReader GetData(int i) => throw new NotSupportedException();
        public override string GetDataTypeName(int i) => "String";
        public override Type GetFieldType(int i) => typeof(string);
        public override string GetName(int i) => _rows[0].Keys.ElementAt(i);

        public override int GetOrdinal(string name)
        {
            int index = 0;
            foreach (string key in _rows[0].Keys)
            {
                if (string.Equals(key, name, StringComparison.Ordinal))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        public override object GetValue(int i) => this[i];

        public override int GetValues(object[] values)
        {
            int count = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < count; i++)
            {
                values[i] = this[i];
            }

            return count;
        }

        public override bool IsDBNull(int i) => this[i] is DBNull;
        public override DataTable GetSchemaTable() => new();

        public int CloseCallCount { get; private set; }

        protected override ValueTask<bool> ReadCoreAsync(CancellationToken cancellationToken)
        {
            _currentRow++;
            return new ValueTask<bool>(_currentRow < _rows.Count);
        }

        protected override ValueTask<bool> NextResultCoreAsync(CancellationToken cancellationToken)
            => new(false);

        protected override ValueTask CloseCoreAsync()
        {
            _closed = true;
            CloseCallCount++;
            return default;
        }
    }

    private static List<Dictionary<string, object>> CreateTestRows()
    {
        return
        [
            new Dictionary<string, object>(StringComparer.Ordinal) { ["Id"] = 1, ["Name"] = "Alice" },
            new Dictionary<string, object>(StringComparer.Ordinal) { ["Id"] = 2, ["Name"] = "Bob" },
            new Dictionary<string, object>(StringComparer.Ordinal) { ["Id"] = 3, ["Name"] = "Charlie" },
        ];
    }

    [Fact]
    public async Task ReadAsync_Returns_True_For_Each_Row_Then_False()
    {
        var reader = new TestDbDataReader(CreateTestRows());

        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeTrue();
        (await reader.ReadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task GetAsyncEnumerator_Iterates_All_Rows()
    {
        var reader = new TestDbDataReader(CreateTestRows());
        var records = new List<IAsyncDataRecord>();

        await foreach (IAsyncDataRecord record in ((IAsyncEnumerable<IAsyncDataRecord>)reader))
        {
            records.Add(record);
        }

        records.Should().HaveCount(3);
    }

    [Fact]
    public void Sync_Read_Works()
    {
        var reader = new TestDbDataReader(CreateTestRows());

        reader.Read().Should().BeTrue();
        reader.GetInt32(0).Should().Be(1);
        reader.Read().Should().BeTrue();
        reader.GetInt32(0).Should().Be(2);
        reader.Read().Should().BeTrue();
        reader.GetInt32(0).Should().Be(3);
        reader.Read().Should().BeFalse();
    }

    [Fact]
    public async Task DisposeAsync_Calls_CloseCoreAsync()
    {
        var reader = new TestDbDataReader(CreateTestRows());

        await reader.DisposeAsync();

        reader.IsClosed.Should().BeTrue();
        reader.CloseCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_Does_Not_Close_If_Already_Closed()
    {
        var reader = new TestDbDataReader(CreateTestRows());
        reader.Close();
        int closeCountBefore = reader.CloseCallCount;

        await reader.DisposeAsync();

        reader.CloseCallCount.Should().Be(closeCountBefore);
    }

    [Fact]
    public async Task NextResultAsync_Returns_False()
    {
        var reader = new TestDbDataReader(CreateTestRows());

        (await reader.NextResultAsync()).Should().BeFalse();
    }

    [Fact]
    public void NextResult_Sync_Returns_False()
    {
        var reader = new TestDbDataReader(CreateTestRows());

        reader.NextResult().Should().BeFalse();
    }

    [Fact]
    public async Task IsDBNullAsync_Delegates_To_IsDBNull()
    {
        var reader = new TestDbDataReader(CreateTestRows());
        await reader.ReadAsync();

        (await reader.IsDBNullAsync(0)).Should().BeFalse();
    }

    [Fact]
    public async Task GetFieldValueAsync_Returns_Typed_Value()
    {
        var reader = new TestDbDataReader(CreateTestRows());
        await reader.ReadAsync();

        (await reader.GetFieldValueAsync<int>(0)).Should().Be(1);
    }

    [Fact]
    public void Implements_IAsyncDataReader()
    {
        typeof(IAsyncDataReader).IsAssignableFrom(typeof(AsyncDbDataReader)).Should().BeTrue();
    }
}
