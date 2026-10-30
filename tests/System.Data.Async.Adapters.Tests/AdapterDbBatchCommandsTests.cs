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
    public void First_read_wraps_the_provider_command_that_was_added()
    {
        var (batch, _) = CreateBatch();
        var command = batch.CreateBatchCommand();
        batch.BatchCommands.Add(command);

        InnerOf(batch.BatchCommands[0]).Should().BeSameAs(InnerOf(command));
    }

    [Fact]
    public void Repeated_reads_return_the_same_wrapper()
    {
        var (batch, _) = CreateBatch();
        batch.BatchCommands.Add(batch.CreateBatchCommand());
        batch.BatchCommands.Add(batch.CreateBatchCommand());

        var first = batch.BatchCommands[0];
        var second = batch.BatchCommands[1];

        batch.BatchCommands[0].Should().BeSameAs(first);
        batch.BatchCommands[1].Should().BeSameAs(second);
        batch.BatchCommands.Should().Equal(first, second);

        var copy = new IAsyncDbBatchCommand[3];
        batch.BatchCommands.CopyTo(copy, 1);
        copy[1].Should().BeSameAs(first);
        copy[2].Should().BeSameAs(second);
    }

    [Fact]
    public void IndexOf_Contains_and_Remove_accept_the_instance_that_was_added()
    {
        var (batch, inner) = CreateBatch();
        var first = batch.CreateBatchCommand();
        var second = batch.CreateBatchCommand();
        batch.BatchCommands.Add(first);
        batch.BatchCommands.Add(second);
        _ = batch.BatchCommands[0];

        batch.BatchCommands.IndexOf(second).Should().Be(1);
        batch.BatchCommands.Contains(first).Should().BeTrue();
        batch.BatchCommands.IndexOf(batch.BatchCommands[1]).Should().Be(1);

        batch.BatchCommands.Remove(first).Should().BeTrue();
        inner.BatchCommands.Should().Equal(InnerOf(second));
        batch.BatchCommands.Contains(first).Should().BeFalse();
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
    public void Indexer_tracks_the_inner_collection_through_every_mutation_after_a_read()
    {
        var (batch, inner) = CreateBatch();
        var commands = batch.BatchCommands;
        var a = batch.CreateBatchCommand();
        var b = batch.CreateBatchCommand();
        var c = batch.CreateBatchCommand();
        var d = batch.CreateBatchCommand();

        commands.Add(a);
        var readA = commands[0];
        InnerOf(readA).Should().BeSameAs(InnerOf(a));

        // Once a read has built the cache, mutations keep it aligned with the instances passed in.
        commands.Add(c);
        commands.Insert(1, b);
        commands.Should().Equal(readA, b, c);

        commands[2] = d;
        commands.Should().Equal(readA, b, d);

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

        commands.Add(a);
        commands[0].Should().BeSameAs(a);
    }

    [Fact]
    public void Mutations_before_any_read_reach_the_provider()
    {
        var (batch, inner) = CreateBatch();
        var commands = batch.BatchCommands;
        var a = batch.CreateBatchCommand();
        var b = batch.CreateBatchCommand();
        var c = batch.CreateBatchCommand();

        commands.Add(a);
        commands.Add(c);
        commands.Insert(1, b);
        commands[2] = a;
        commands.RemoveAt(0);
        inner.BatchCommands.Should().Equal(InnerOf(b), InnerOf(a));
        commands.Count.Should().Be(2);

        commands.Clear();
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

    // A batch that is built, run and disposed without reading BatchCommands back, as an ORM's
    // generated code does, should cost the provider's allocations plus the adapter objects
    // themselves: the batch, one wrapper per command and the list. Adding a command stores
    // nothing of the adapter's own.
    [Fact]
    public void Adding_commands_allocates_nothing_over_the_provider()
    {
        const int Iterations = 200;
        long raw = 0, adapted = 0, access = 0;
        for (var round = 0; round < 2; round++)
        {
            raw = adapted = access = 0;
            for (var i = 0; i < Iterations; i++)
            {
                var inner = new PendingDbBatch(new PendingSlot());
                var r1 = inner.CreateBatchCommand();
                var r2 = inner.CreateBatchCommand();
                var t0 = GC.GetAllocatedBytesForCurrentThread();
                inner.BatchCommands.Add(r1);
                inner.BatchCommands.Add(r2);
                inner.Dispose();
                raw += GC.GetAllocatedBytesForCurrentThread() - t0;

                var batch = new AdapterDbBatch(new PendingDbBatch(new PendingSlot()), connection: null);
                var c1 = batch.CreateBatchCommand();
                var c2 = batch.CreateBatchCommand();
                var t1 = GC.GetAllocatedBytesForCurrentThread();
                var commands = batch.BatchCommands;
                var t2 = GC.GetAllocatedBytesForCurrentThread();
                commands.Add(c1);
                commands.Add(c2);
                batch.Dispose();
                adapted += GC.GetAllocatedBytesForCurrentThread() - t2;
                access += t2 - t1;
            }
        }

        (adapted - raw).Should().Be(0, "adding a command should store nothing beyond the provider's own entry");
        (access / Iterations).Should().BeLessThanOrEqualTo(24, "the list should be a single object of one field");
    }
}
