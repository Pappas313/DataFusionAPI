using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using DataFusionAPI.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Moq;

namespace DataFusionAPI.Tests;

public class CachedExternalApiClientTests
{
    private static readonly IReadOnlyList<AggregatedItem> Items =
    [
        new("1", "Src", "T", "D", "u", DateTimeOffset.UnixEpoch, "c", 1)
    ];

    private static Mock<IExternalApiClient> Inner()
    {
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns("Src");
        return mock;
    }

    [Fact]
    public async Task FetchAsync_SecondCallWithinTtl_ReturnsCachedAndCallsInnerOnce()
    {
        var inner = Inner();
        inner.Setup(c => c.FetchAsync("q", It.IsAny<CancellationToken>())).ReturnsAsync(Items);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CachedExternalApiClient(inner.Object, cache);

        await client.FetchAsync("q", CancellationToken.None);
        var second = await client.FetchAsync("q", CancellationToken.None);

        Assert.Same(Items, second);
        inner.Verify(c => c.FetchAsync("q", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_InnerThrowsAfterFreshEntryExpired_ReturnsLastKnownValue()
    {
        var inner = Inner();
        inner.SetupSequence(c => c.FetchAsync("q", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Items)
            .ThrowsAsync(new HttpRequestException("down"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CachedExternalApiClient(inner.Object, cache);

        await client.FetchAsync("q", CancellationToken.None);
        cache.Remove("Src:q"); // simulate the 60s TTL expiring

        var result = await client.FetchAsync("q", CancellationToken.None);

        Assert.Same(Items, result);
    }

    [Fact]
    public async Task FetchAsync_InnerThrowsAndNothingCached_Rethrows()
    {
        var inner = Inner();
        inner.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CachedExternalApiClient(inner.Object, cache);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FetchAsync("q", CancellationToken.None));
    }

    [Fact]
    public async Task FetchAsync_ConcurrentCallsForSameKey_CallInnerOnce()
    {
        var inner = Inner();
        inner.Setup(c => c.FetchAsync("q", It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(100);
                return Items;
            });
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CachedExternalApiClient(inner.Object, cache);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.FetchAsync("q", CancellationToken.None)));

        inner.Verify(c => c.FetchAsync("q", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_DifferentQueries_AreCachedSeparately()
    {
        var inner = Inner();
        inner.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Items);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new CachedExternalApiClient(inner.Object, cache);

        await client.FetchAsync("a", CancellationToken.None);
        await client.FetchAsync("b", CancellationToken.None);

        inner.Verify(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
