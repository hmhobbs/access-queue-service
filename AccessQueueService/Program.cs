using AccessQueueService.Data;
using AccessQueueService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog configuration from appsettings and serilog.json
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IAccessService, AccessService>();
builder.Services.AddSingleton<IAccessQueueRepo, TakeANumberAccessQueueRepo>();
builder.Services.AddHostedService<AccessCleanupBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
