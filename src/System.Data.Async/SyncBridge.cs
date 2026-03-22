namespace System.Data.Async;

internal static class SyncBridge
{
    internal static void ThrowIfBrowser(string asyncMethodName)
    {
        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException(
                $"Synchronous operations are not supported on this platform. Use {asyncMethodName}() instead.");
        }
    }
}
