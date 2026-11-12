using System.Reflection;
using System.Text.Json.Serialization;
using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Infrastructure.Aggregation;
using DataFusionAPI.Infrastructure.Clients;
using DataFusionAPI.Infrastructure.Http;
using DataFusionAPI.Infrastructure.Stats;

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
builder.Services.AddSingleton<IStatsRecorder, InMemoryStatsRecorder>();

builder.Services.AddExternalApiClient<GitHubApiClient>(GitHubApiClient.Name, client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DataFusionAPI");
});

builder.Services.AddExternalApiClient<CoinGeckoApiClient>(CoinGeckoApiClient.Name, client =>
{
    client.BaseAddress = new Uri("https://api.coingecko.com/api/v3/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DataFusionAPI");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});

builder.Services.AddExternalApiClient<TmdbApiClient>(TmdbApiClient.Name, client =>
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/"));

builder.Services.AddScoped<IAggregationService, AggregationService>();

var app = builder.Build();

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
