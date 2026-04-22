using FlashDrop.API.Shared.Behaviors;
using FlashDrop.API.Shared.Data;
using FlashDrop.API.Shared.Middleware;
using FluentValidation;      //AddValidatorsFromAssemblyContaining
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; 
using Serilog;
using System;
using System.Reflection;// Assembly.GetExecutingAssembly()
using AutoMapper;

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

    builder.Services.AddDbContext<FlashDropDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ));


    //registering MediaTR

    // RegisterServicesFromAssembly scans the project's compiled assembly
    // and auto-registers every class that implements IRequestHandler<,>.
    // After this, ISender.Send(new SomeCommand()) will find SomeCommandHandler
    // automatically — no manual registration per handler needed.

    builder.Services.AddMediatR(cfg
        => cfg.RegisterServicesFromAssembly(
            Assembly.GetExecutingAssembly()));

    builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();


    builder.Services.AddTransient(                                     // [+]
    typeof(IPipelineBehavior<,>),                                  // [+]
    typeof(ValidationBehavior<,>));


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