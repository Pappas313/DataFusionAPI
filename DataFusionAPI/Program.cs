using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json.Serialization;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Extensions;
using DataFusionAPI.Infrastructure.Aggregation;
using DataFusionAPI.Infrastructure.Clients;
using DataFusionAPI.Infrastructure.Http;
using DataFusionAPI.Infrastructure.Options;
using DataFusionAPI.Infrastructure.Stats;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

builder.Services.AddAggregationOptions();
builder.Services.AddSingleton<IStatsRecorder, InMemoryStatsRecorder>();

builder.Services.AddExternalApiClient<GitHubApiClient>(GitHubApiClient.Name, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<GitHubOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DataFusionAPI");
    if (!string.IsNullOrWhiteSpace(options.Token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
    }
});

builder.Services.AddExternalApiClient<CoinGeckoApiClient>(CoinGeckoApiClient.Name, (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<CoinGeckoOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DataFusionAPI");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    if (!string.IsNullOrWhiteSpace(options.DemoApiKey))
    {
        client.DefaultRequestHeaders.Add("x-cg-demo-api-key", options.DemoApiKey);
    }
});

builder.Services.AddExternalApiClient<TmdbApiClient>(TmdbApiClient.Name, (sp, client) =>
    client.BaseAddress = sp.GetRequiredService<IOptions<TmdbOptions>>().Value.BaseAddress);

builder.Services.AddScoped<IAggregationService, AggregationService>();

var app = builder.Build();

if (string.IsNullOrWhiteSpace(app.Services.GetRequiredService<IOptions<TmdbOptions>>().Value.ApiKey))
{
    app.Logger.LogWarning(
        "TMDB API key is not configured (Tmdb:ApiKey). TMDB will be reported in failedSources until it is set.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
