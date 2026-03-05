using Microsoft.EntityFrameworkCore;
using server.Data;
using StackExchange.Redis;
using Azure.Identity;
using server.Services.AI;
using server.Services.RAG;
using server.Services.Grpc;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine(
    "CS = " + builder.Configuration.GetConnectionString("DefaultConnection")
);


builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()
    )
);


builder.Services.AddScoped<IAIService, OpenAiService>();

builder.Services.AddScoped<RagService>();

builder.Services.AddScoped<TestGenerator>();

builder.Services.AddGrpc();

var connectionString = builder.Configuration["CacheConnection"];

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Redis connection string 'CacheConnection' is missing.");
}

var redisOptions = ConfigurationOptions.Parse(connectionString);

if (redisOptions.EndPoints.Any(e => e.ToString()!.Contains("redis.cache.windows.net")))
{
    redisOptions = await redisOptions.ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential());
}

builder.Services.AddStackExchangeRedisCache(option =>
{
    option.ConfigurationOptions = redisOptions;
});

var app = builder.Build();

// Автоматичне застосування міграцій при запуску
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.MapGrpcService<AuthGrpcService>();

app.MapGrpcService<FileGrpcService>();

app.MapGrpcService<TestGrpcService>();

app.MapGet("/", () => "AutoTestLab gRPC Server");

app.Run();