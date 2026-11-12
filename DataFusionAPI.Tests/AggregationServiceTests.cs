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

    private static Mock<IExternalApiClient> Client(string name, params AggregatedItem[] items)
    {
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns(name);
        mock.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        return mock;
    }

    private static Mock<IExternalApiClient> FailingClient(string name)
    {
        var mock = new Mock<IExternalApiClient>();
        mock.SetupGet(c => c.SourceName).Returns(name);
        mock.Setup(c => c.FetchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("boom"));
        return mock;
    }

    private static AggregationService CreateService(params Mock<IExternalApiClient>[] clients) =>
        new(clients.Select(c => c.Object), NullLogger<AggregationService>.Instance);

    [Fact]
    public async Task AggregateAsync_AllSucceed_MergesAndSortsByRelevanceDescendingByDefault()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "news", 0.5, 1), Item("a2", "A", "news", 0.9, 2)),
            Client("B", Item("b1", "B", "news", 0.7, 3)),
            Client("C", Item("c1", "C", "news", 0.1, 4)));

        var result = await service.AggregateAsync("q", null, null, null, CancellationToken.None);

        Assert.Empty(result.FailedSources);
        Assert.Equal(new[] { "a2", "b1", "a1", "c1" }, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task AggregateAsync_SortByDateAscending_OrdersOldestFirst()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "news", 0.5, 1)),
            Client("B", Item("b1", "B", "news", 0.7, 3)),
            Client("C", Item("c1", "C", "news", 0.1, 2)));

        var result = await service.AggregateAsync("q", "date", "asc", null, CancellationToken.None);

        Assert.Equal(new[] { "b1", "c1", "a1" }, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task AggregateAsync_OneClientThrows_ReportsFailedSourceAndReturnsOthers()
    {
        var service = CreateService(
            Client("A", Item("a1", "A", "news", 0.5, 1)),
            FailingClient("B"),
            Client("C", Item("c1", "C", "news", 0.1, 2)));

        var result = await service.AggregateAsync("q", null, null, null, CancellationToken.None);

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

        var result = await service.AggregateAsync("q", null, null, "NEWS", CancellationToken.None);

        Assert.Equal(new[] { "b1", "a1" }, result.Items.Select(i => i.Id));
    }
}
