using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Core.Models;
using DataFusionAPI.Infrastructure.Aggregation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DataFusionAPI.Tests;

public class AggregationServiceTests
{
    private static readonly DateTimeOffset Now = new(2025, 1, 10, 0, 0, 0, TimeSpan.Zero);

    private static AggregatedItem Item(string id, string source, string category, double score, int daysAgo) =>
        new(id, source, $"Title {id}", "Description", $"https://example.com/{id}", Now.AddDays(-daysAgo), category, score);

    private static Mock<IAggregationSource> Client(string name, params AggregatedItem[] items)
    {
        var mock = new Mock<IAggregationSource>();
        mock.SetupGet(c => c.SourceName).Returns(name);
        mock.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourceResult(items, false));
        return mock;
    }

    private static Mock<IAggregationSource> FailingClient(string name)
    {
        var mock = new Mock<IAggregationSource>();
        mock.SetupGet(c => c.SourceName).Returns(name);
        mock.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        return mock;
    }

    private static AggregationService CreateService(params Mock<IAggregationSource>[] clients) =>
        new(clients.Select(c => c.Object), NullLogger<AggregationService>.Instance);

    [Fact]
    public async Task AggregateAsync_SourceServedStale_ListsItInStaleSources()
    {
        var stale = new Mock<IAggregationSource>();
        stale.SetupGet(c => c.SourceName).Returns("S");
        stale.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SourceResult([Item("s1", "S", "news", 0.5, 1)], true));
        var service = CreateService(Client("A", Item("a1", "A", "news", 0.5, 1)), stale);

        var result = await service.AggregateAsync("q", SortField.Relevance, SortDirection.Desc, null, CancellationToken.None);

        Assert.Equal(new[] { "S" }, result.StaleSources);
        Assert.Empty(result.FailedSources);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task AggregateAsync_AllSucceed_MergesAndSortsByRelevanceDescendingByDefault()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "news", 0.5, 1), Item("a2", "A", "news", 0.9, 2)),
            Client("B", Item("b1", "B", "news", 0.7, 3)),
            Client("C", Item("c1", "C", "news", 0.1, 4)));

        var result = await service.AggregateAsync("q", SortField.Relevance, SortDirection.Desc, null, CancellationToken.None);

        Assert.Empty(result.FailedSources);
        Assert.Equal(new[] { "a1", "b1", "c1", "a2" }, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task AggregateAsync_SortByDateAscending_OrdersOldestFirst()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "news", 0.5, 1)),
            Client("B", Item("b1", "B", "news", 0.7, 3)),
            Client("C", Item("c1", "C", "news", 0.1, 2)));

        var result = await service.AggregateAsync("q", SortField.Date, SortDirection.Asc, null, CancellationToken.None);

        Assert.Equal(new[] { "b1", "c1", "a1" }, result.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData(SortDirection.Asc)]
    [InlineData(SortDirection.Desc)]
    public async Task AggregateAsync_SortByDate_PutsUndatedItemsLast(SortDirection direction)
    {
        var undated = Item("u1", "U", "news", 0.5, 0) with { PublishedOrRelevantDate = null };
        var service = CreateService(
            Client("U", undated),
            Client("A", Item("a1", "A", "news", 0.5, 1)),
            Client("B", Item("b1", "B", "news", 0.5, 5)));

        var result = await service.AggregateAsync("q", SortField.Date, direction, null, CancellationToken.None);

        Assert.Equal("u1", result.Items[^1].Id);
    }

    [Fact]
    public async Task AggregateAsync_RelevanceSort_NormalisesScoresAcrossSources()
    {
        // Raw scores are on unrelated scales; each source's best match must still rank first.
        var service = CreateService(
            Client("Big", Item("big1", "Big", "x", 900, 1), Item("big2", "Big", "x", 800, 1)),
            Client("Small", Item("small1", "Small", "x", 0.01, 1)));

        var result = await service.AggregateAsync("q", SortField.Relevance, SortDirection.Desc, null, CancellationToken.None);

        Assert.Equal(new[] { "big1", "small1", "big2" }, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task AggregateAsync_OneClientThrows_ReportsFailedSourceAndReturnsOthers()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "news", 0.5, 1)),
            FailingClient("B"),
            Client("C", Item("c1", "C", "news", 0.1, 2)));

        var result = await service.AggregateAsync("q", SortField.Relevance, SortDirection.Desc, null, CancellationToken.None);

        Assert.Equal(new[] { "B" }, result.FailedSources);
        Assert.Equal(2, result.Items.Count);
        Assert.DoesNotContain(result.Items, i => i.Source == "B");
    }

    [Fact]
    public async Task AggregateAsync_CategoryProvided_FiltersCaseInsensitively()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "News", 0.5, 1), Item("a2", "A", "Weather", 0.9, 1)),
            Client("B", Item("b1", "B", "news", 0.7, 1)),
            Client("C", Item("c1", "C", "Music", 0.1, 1)));

        var result = await service.AggregateAsync("q", SortField.Relevance, SortDirection.Desc, "NEWS", CancellationToken.None);

        Assert.Equal(new[] { "a1", "b1" }, result.Items.Select(i => i.Id));
    }
}


