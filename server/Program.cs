using Microsoft.EntityFrameworkCore;
using server.Data;
using StackExchange.Redis;
using Azure.Identity;

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

builder.Services.AddGrpc();
var redisOptions = await ConfigurationOptions.Parse(builder.Configuration["CacheConnection"]!).ConfigureForAzureWithTokenCredentialAsync(new DefaultAzureCredential());
builder.Services.AddStackExchangeRedisCache(option =>
{
    option.ConfigurationOptions = redisOptions;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.MapGrpcService<AuthGrpcService>();
app.MapGet("/", () => "AutoTestLab gRPC Server");

app.Run();
