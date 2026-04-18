using FlashDrop.API.Shared.Middleware;
using Serilog;
using System;

// 1. BOOTSTRAP LOGGER (Catches crashes before the app even fully starts)
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting up FlashDrop API...");

    var builder = WebApplication.CreateBuilder(args);

    // 2. FULL SERILOG CONFIGURATION
    builder.Host.UseSerilog((context, services, logConfig) =>
        logConfig.ReadFrom.Configuration(context.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext()
    );

    // ==========================================
    // PROMPT 6 TO 50: ADD ALL SERVICES HERE
    // ==========================================
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();


    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // ==========================================
    // PROMPT 6 TO 50: ADD ALL MIDDLEWARE HERE
    // ==========================================
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start correctly.");
}
finally
{
    Log.CloseAndFlush();
}