using System.Data.Async.Adapters.Tests.Fakes;
using System.Data.Common;
using FluentAssertions;
using Xunit;

namespace System.Data.Async.Adapters.Tests;

public class AdapterDbBatchCommandsTests
{
    private static (AdapterDbBatch Adapter, DbBatch Inner) CreateBatch()
    {
        var inner = new PendingDbBatch(new PendingSlot());
        return (new AdapterDbBatch(inner, connection: null), inner);
    }

    private static DbBatchCommand InnerOf(IAsyncDbBatchCommand command) => (DbBatchCommand)(AdapterDbBatchCommand)command;

    [Fact]
    public void Indexer_returns_the_instance_that_was_added()
    {
        var (batch, _) = CreateBatch();
        var command = batch.CreateBatchCommand();
        batch.BatchCommands.Add(command);

        batch.BatchCommands[0].Should().BeSameAs(command);
        batch.BatchCommands[0].Should().BeSameAs(batch.BatchCommands[0]);
    }

    [Fact]
    public void Enumerator_and_CopyTo_return_the_same_instances_as_the_indexer()
    {
        var (batch, _) = CreateBatch();
        var first = batch.CreateBatchCommand();
        var second = batch.CreateBatchCommand();
        batch.BatchCommands.Add(first);
        batch.BatchCommands.Add(second);

        batch.BatchCommands.Should().Equal(first, second);

        var copy = new IAsyncDbBatchCommand[3];
        batch.BatchCommands.CopyTo(copy, 1);
        copy[1].Should().BeSameAs(first);
        copy[2].Should().BeSameAs(second);
    }

    [Fact]
    public void Commands_added_to_the_inner_batch_are_wrapped_once()
    {
        var (batch, inner) = CreateBatch();
        var raw = inner.CreateBatchCommand();
        inner.BatchCommands.Add(raw);

        var wrapped = batch.BatchCommands[0];

        InnerOf(wrapped).Should().BeSameAs(raw);
        batch.BatchCommands[0].Should().BeSameAs(wrapped);
    }

    [Fact]
    public void Indexer_tracks_the_inner_collection_through_every_mutation()
    {
        var (batch, inner) = CreateBatch();
        var commands = batch.BatchCommands;
        var a = batch.CreateBatchCommand();
        var b = batch.CreateBatchCommand();
        var c = batch.CreateBatchCommand();
        var d = batch.CreateBatchCommand();

        commands.Add(a);
        commands.Add(c);
        commands.Insert(1, b);
        commands.Should().Equal(a, b, c);

        commands[2] = d;
        commands.Should().Equal(a, b, d);

        commands.Remove(a).Should().BeTrue();
        commands.Should().Equal(b, d);

        commands.RemoveAt(0);
        commands.Should().Equal(d);

        // A change made behind the adapter's back, straight on the provider's collection.
        var raw = inner.CreateBatchCommand();
        inner.BatchCommands.Insert(0, raw);
        InnerOf(commands[0]).Should().BeSameAs(raw);
        commands[1].Should().BeSameAs(d);

        commands.Clear();
        commands.Should().BeEmpty();
        inner.BatchCommands.Should().BeEmpty();
    }

    [Fact]
    public void Reading_through_the_indexer_allocates_nothing()
    {
        var (batch, _) = CreateBatch();
        var commands = batch.BatchCommands;
        commands.Add(batch.CreateBatchCommand());
        commands.Add(batch.CreateBatchCommand());
        _ = commands[0];
        _ = commands[1];

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            _ = commands[i % 2];
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).Should().Be(0);
    }
}
