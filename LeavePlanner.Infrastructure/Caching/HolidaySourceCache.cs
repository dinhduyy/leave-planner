using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace LeavePlanner.Infrastructure.Caching;

internal sealed class HolidaySourceCache(MemoryCache cache, TimeSpan duration) : IDisposable
{
    private readonly ConcurrentDictionary<string, PendingLoad> _pending = new(StringComparer.Ordinal);

    public async Task<T> GetOrLoadAsync<T>(
        string key, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
        where T : class
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (cache.TryGetValue(key, out T? cached) && cached is not null)
            {
                return cached;
            }

            var candidate = new PendingLoad(cancellationToken);
            var pending = _pending.GetOrAdd(key, candidate);
            if (ReferenceEquals(candidate, pending))
            {
                _ = PopulateAsync(key, pending, load, cancellationToken);
            }

            try
            {
                return (T)await pending.Completion.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && pending.LoadingToken.IsCancellationRequested)
            {
                // The loading caller left; an active waiter can start a fresh load.
            }
        }
    }

    private async Task PopulateAsync<T>(
        string key, PendingLoad pending, Func<CancellationToken, Task<T>> load,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            T value;
            if (cache.TryGetValue(key, out T? cached) && cached is not null)
            {
                value = cached;
            }
            else
            {
                value = await load(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                cache.Set(key, value, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = duration,
                    Size = 1
                });
            }

            _pending.TryRemove(key, out _);
            pending.Completion.TrySetResult(value);
        }
        catch (Exception exception)
        {
            _pending.TryRemove(key, out _);
            pending.Completion.TrySetException(exception);
        }
    }

    public void Dispose() => cache.Dispose();

    private sealed class PendingLoad(CancellationToken loadingToken)
    {
        public CancellationToken LoadingToken { get; } = loadingToken;
        public TaskCompletionSource<object> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
