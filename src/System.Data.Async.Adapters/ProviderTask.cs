namespace System.Data.Async.Adapters;

/// <summary>
/// Delivers a provider call that threw synchronously, instead of returning a faulted task, the way an
/// <c>async</c> forwarder would: the exception surfaces when the caller awaits, not at the call, and an
/// <see cref="OperationCanceledException"/> yields a canceled task that rethrows that same instance.
/// </summary>
/// <remarks>
/// The adapters forward a provider's <see cref="Task"/> by wrapping it in a <see cref="ValueTask"/>
/// rather than awaiting it in an <c>async</c> method, which would allocate a state machine whenever the
/// provider completes asynchronously. Every forwarder catches what the provider throws synchronously
/// and routes it through here, so callers observe exactly what the <c>async</c> forwarder gave them.
/// Only that exceptional path allocates.
/// </remarks>
internal static class ProviderTask
{
    public static async ValueTask<T> FromThrown<T>(Exception exception)
        => await Task.FromException<T>(exception).ConfigureAwait(false);

    public static async ValueTask FromThrown(Exception exception)
        => await Task.FromException(exception).ConfigureAwait(false);
}
