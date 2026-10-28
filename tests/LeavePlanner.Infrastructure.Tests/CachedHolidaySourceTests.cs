using LeavePlanner.Application.Abstractions;
using LeavePlanner.Application.Models;
using LeavePlanner.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace LeavePlanner.Infrastructure.Tests;

public sealed class CachedHolidaySourceTests
{
    [Fact]
    public async Task CatalogAndYearlyHolidaysExpireAfterConfiguredAbsoluteLifetime()
    {
        var clock = new TestClock();
        var source = new StubSource();
        using var cache = CreateCache(clock, TimeSpan.FromHours(24));
        var cached = new CachedHolidaySource(source, cache);

        await cached.GetAvailableCountryCodesAsync(CancellationToken.None);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(23));
        await cached.GetAvailableCountryCodesAsync(CancellationToken.None);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        Assert.Equal(1, source.CountryCalls);
        Assert.Equal(1, source.HolidayCalls);

        clock.Advance(TimeSpan.FromHours(1));
        await cached.GetAvailableCountryCodesAsync(CancellationToken.None);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        Assert.Equal(2, source.CountryCalls);
        Assert.Equal(2, source.HolidayCalls);
    }

    [Fact]
    public async Task LifetimeCanBeConfiguredToOneHour()
    {
        var clock = new TestClock();
        var source = new StubSource();
        using var cache = CreateCache(clock, TimeSpan.FromHours(1));
        var cached = new CachedHolidaySource(source, cache);

        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(1));
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);

        Assert.Equal(2, source.HolidayCalls);
    }

    [Fact]
    public async Task CountryAndYearAreDistinctKeysAndEmptyHolidayListIsCached()
    {
        var source = new StubSource();
        using var cache = CreateCache();
        var cached = new CachedHolidaySource(source, cache);

        Assert.Empty(await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Empty(await cached.GetHolidaysAsync("at", 2026, CancellationToken.None));
        Assert.Empty(await cached.GetHolidaysAsync("AT", 2027, CancellationToken.None));
        Assert.Empty(await cached.GetHolidaysAsync("DE", 2026, CancellationToken.None));

        Assert.Equal(3, source.HolidayCalls);
        Assert.Equal(["AT:2026", "AT:2027", "DE:2026"], source.RequestedYears);
    }

    [Fact]
    public async Task CachedSnapshotsCannotBeChangedThroughSourceCollections()
    {
        var countries = new HashSet<string>(["AT"]);
        var subdivisions = new[] { "AT-9" };
        var types = new[] { "Public" };
        var records = new List<HolidayRecord>
        {
            new(new DateOnly(2026, 1, 1), "Synthetic holiday", "AT", false, subdivisions, types)
        };
        var source = new StubSource
        {
            CountryLoader = _ => Task.FromResult<IReadOnlySet<string>>(countries),
            HolidayLoader = (_, _, _) => Task.FromResult<IReadOnlyList<HolidayRecord>>(records)
        };
        using var cache = CreateCache();
        var cached = new CachedHolidaySource(source, cache);

        await cached.GetAvailableCountryCodesAsync(CancellationToken.None);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        countries.Clear();
        subdivisions[0] = "AT-1";
        types[0] = "Bank";
        records.Clear();

        Assert.Contains("AT", await cached.GetAvailableCountryCodesAsync(CancellationToken.None));
        var holiday = Assert.Single(await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal("AT-9", Assert.Single(holiday.SubdivisionCodes!));
        Assert.Equal("Public", Assert.Single(holiday.HolidayTypes));
    }

    [Fact]
    public async Task ConcurrentCallersShareOneLoadAndWaitingCallerCanCancel()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubSource
        {
            HolidayLoader = async (_, _, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                return [];
            }
        };
        using var cache = CreateCache();
        var cached = new CachedHolidaySource(source, cache);

        var leader = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var waitingCancellation = new CancellationTokenSource();
        var canceledWaiter = cached.GetHolidaysAsync("AT", 2026, waitingCancellation.Token);
        var otherWaiter = new CachedHolidaySource(source, cache)
            .GetHolidaysAsync("AT", 2026, CancellationToken.None);
        waitingCancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWaiter);
        release.SetResult();

        await leader.WaitAsync(TimeSpan.FromSeconds(5));
        await otherWaiter.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, source.HolidayCalls);
    }

    [Fact]
    public async Task FailedLoadIsRetriedAndNotServedFromExpiredCache()
    {
        var clock = new TestClock();
        var source = new StubSource();
        using var cache = CreateCache(clock, TimeSpan.FromHours(1));
        var cached = new CachedHolidaySource(source, cache);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        clock.Advance(TimeSpan.FromHours(1));
        source.HolidayLoader = (_, _, _) => throw new InvalidOperationException("Synthetic upstream failure");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cached.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cached.GetHolidaysAsync("AT", 2026, CancellationToken.None));

        Assert.Equal(3, source.HolidayCalls);
    }

    [Fact]
    public async Task CanceledLoadDoesNotPopulateCache()
    {
        var source = new StubSource
        {
            HolidayLoader = (_, _, token) => Task.FromCanceled<IReadOnlyList<HolidayRecord>>(
                new CancellationToken(canceled: true))
        };
        using var cache = CreateCache();
        var cached = new CachedHolidaySource(source, cache);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cached.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        source.HolidayLoader = (_, _, _) => Task.FromResult<IReadOnlyList<HolidayRecord>>([]);
        Assert.Empty(await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None));
        Assert.Equal(2, source.HolidayCalls);
    }

    [Fact]
    public async Task EntryLimitPreventsAdditionalKeysFromBeingCached()
    {
        var source = new StubSource();
        using var cache = CreateCache(maxEntries: 1);
        var cached = new CachedHolidaySource(source, cache);

        await cached.GetAvailableCountryCodesAsync(CancellationToken.None);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        await cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);

        Assert.Equal(1, source.CountryCalls);
        Assert.Equal(2, source.HolidayCalls);
    }

    [Fact]
    public async Task ConcurrentCallersShareFailureWithoutCachingIt()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubSource
        {
            HolidayLoader = async (_, _, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                throw new InvalidOperationException("Synthetic upstream failure");
            }
        };
        using var cache = CreateCache();
        var cached = new CachedHolidaySource(source, cache);

        var first = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        var third = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        release.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => first.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => third.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, source.HolidayCalls);
    }

    [Fact]
    public async Task ConcurrentCallersShareLoadEvenWhenCacheIsAtCapacity()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubSource
        {
            HolidayLoader = async (_, _, token) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                return [];
            }
        };
        using var cache = CreateCache(maxEntries: 1);
        var cached = new CachedHolidaySource(source, cache);
        await cached.GetAvailableCountryCodesAsync(CancellationToken.None);

        var first = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        var third = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        release.SetResult();

        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        await third.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, source.HolidayCalls);
    }

    [Fact]
    public async Task WaitingCallerRetriesWhenLoadingCallerCancels()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var source = new StubSource
        {
            HolidayLoader = async (_, _, token) =>
            {
                if (token.CanBeCanceled)
                {
                    entered.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, token);
                }

                return [];
            }
        };
        using var cache = CreateCache();
        var cached = new CachedHolidaySource(source, cache);
        using var leaderCancellation = new CancellationTokenSource();

        var leader = cached.GetHolidaysAsync("AT", 2026, leaderCancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var waiter = cached.GetHolidaysAsync("AT", 2026, CancellationToken.None);
        leaderCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => leader);
        Assert.Empty(await waiter.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, source.HolidayCalls);
    }

    private static HolidaySourceCache CreateCache(
        TestClock? clock = null, TimeSpan? duration = null, int maxEntries = 512) =>
        new(new MemoryCache(new MemoryCacheOptions { Clock = clock, SizeLimit = maxEntries }),
            duration ?? TimeSpan.FromHours(24));

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public void Advance(TimeSpan duration) => UtcNow += duration;
    }

    private sealed class StubSource : IHolidaySource
    {
        public int CountryCalls { get; private set; }
        public int HolidayCalls { get; private set; }
        public List<string> RequestedYears { get; } = [];
        public Func<CancellationToken, Task<IReadOnlySet<string>>> CountryLoader { get; set; } =
            _ => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(["AT"]));
        public Func<string, int, CancellationToken, Task<IReadOnlyList<HolidayRecord>>> HolidayLoader { get; set; } =
            (_, _, _) => Task.FromResult<IReadOnlyList<HolidayRecord>>([]);

        public Task<IReadOnlySet<string>> GetAvailableCountryCodesAsync(CancellationToken cancellationToken)
        {
            CountryCalls++;
            return CountryLoader(cancellationToken);
        }

        public Task<IReadOnlyList<HolidayRecord>> GetHolidaysAsync(
            string countryCode, int year, CancellationToken cancellationToken)
        {
            HolidayCalls++;
            RequestedYears.Add($"{countryCode}:{year}");
            return HolidayLoader(countryCode, year, cancellationToken);
        }
    }
}
