using RssReader.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RssReader.Data;
using RssReader.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContextFactory<RssReaderContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("RssReaderContext") ?? throw new InvalidOperationException("Connection string 'RssReaderContext' not found."));
    
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
    
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

builder.Services.AddQuickGridEntityFrameworkAdapter();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add HttpClient factory for FeedService
builder.Services.AddHttpClient();

// Add memory cache for settings
builder.Services.AddMemoryCache();

// Add notification services
builder.Services.AddSingleton<UpdateNotifier>();

// Add application services
builder.Services.AddScoped<OpmlHandler>();
builder.Services.AddScoped<Settings>();
builder.Services.AddScoped<FeedManager>();

// Add background service for scheduled feed updates
builder.Services.AddHostedService<FeedUpdater>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RssReaderContext>>();
    await using var context = await dbContextFactory.CreateDbContextAsync();
    await context.Database.MigrateAsync();
}

// Initialize default settings
using (var scope = app.Services.CreateScope())
{
    var settingsService = scope.ServiceProvider.GetRequiredService<Settings>();
    await settingsService.InitializeDefaultSettingsAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseMigrationsEndPoint();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
