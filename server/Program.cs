using server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddStackExchangeRedisCache(options => {
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    //додаю префікс до всіх ключів у Redis
    options.InstanceName = "AutoTestLab_";
});

builder.Services.AddGrpc();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
