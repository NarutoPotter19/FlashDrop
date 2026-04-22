using FlashDrop.API.Shared.Middleware;
using Serilog;
using System;

using FlashDrop.API.Shared.Data;
using Microsoft.EntityFrameworkCore;

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

    // AddDbContext<T> registers FlashDropDbContext in the DI container
    // as a SCOPED service (one instance per HTTP request).
    //
    // options.UseNpgsql(...) tells EF Core to use the Npgsql PostgreSQL
    // provider (from the Npgsql.EntityFrameworkCore.PostgreSQL NuGet package).
    //
    // GetConnectionString("DefaultConnection") reads from appsettings.json:
    //   "ConnectionStrings": {
    //     "DefaultConnection": "Host=localhost;Port=5432;Database=flashdrop;..."
    //   }
    //

    builder.Services.AddDbContext<FlashDropDbContext>(options=>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ));


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