using AccessQueueService.Data;
using AccessQueueService.Services;

var builder = WebApplication.CreateBuilder(args);

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
