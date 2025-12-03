using RssReader.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RssReader.Data;
using RssReader.Services;
using RssReader.Endpoints;
using Serilog;

// Configure Serilog early to capture startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting RssReader application");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog from appsettings.json
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

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
    builder.Services.AddScoped<ArticleQueryService>();

    // Add background service for scheduled feed updates
    builder.Services.AddHostedService<FeedUpdater>();

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var app = builder.Build();

    // Use Serilog for request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // Apply database migrations
    using (var scope = app.Services.CreateScope())
    {
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RssReaderContext>>();
        await using var context = await dbContextFactory.CreateDbContextAsync();
        Log.Information("Applying database migrations");
        await context.Database.MigrateAsync();
        Log.Information("Database migrations completed");
    }

    // Initialize default settings
    using (var scope = app.Services.CreateScope())
    {
        var settingsService = scope.ServiceProvider.GetRequiredService<Settings>();
        Log.Information("Initializing default settings");
        await settingsService.InitializeDefaultSettingsAsync();
        Log.Information("Default settings initialized");
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
    
    // Map image proxy endpoint
    app.MapImageProxyEndpoint();
    
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    Log.Information("Application started successfully");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.Information("Application shutting down");
    await Log.CloseAndFlushAsync();
}
