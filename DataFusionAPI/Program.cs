using DataFusionAPI.Core.Interfaces;
using DataFusionAPI.Infrastructure.Aggregation;
using DataFusionAPI.Infrastructure.Clients;
using DataFusionAPI.Infrastructure.Http;
using DataFusionAPI.Infrastructure.Stats;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IStatsRecorder, InMemoryStatsRecorder>();

builder.Services.AddExternalApiClient<GitHubApiClient>(GitHubApiClient.Name, client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("DataFusionAPI");
});

builder.Services.AddScoped<IAggregationService, AggregationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
