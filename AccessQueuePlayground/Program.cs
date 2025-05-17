using AccessQueuePlayground.Components;
using AccessQueuePlayground.Services;
using AccessQueueService.Data;
using AccessQueueService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog configuration for console logging only
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .WriteTo.Console()
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext();
});

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton<IAccessService, AccessService>();
builder.Services.AddSingleton<IAccessQueueRepo, TakeANumberAccessQueueRepo>();
builder.Services.AddSingleton<IAccessQueueManager, AccessQueueManager>();
builder.Services.AddHostedService<AccessQueueBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
