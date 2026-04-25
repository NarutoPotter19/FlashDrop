using AutoMapper;
using FlashDrop.API.Modules.Identity;
using FlashDrop.API.Shared.Behaviors;
using FlashDrop.API.Shared.Data;
using FlashDrop.API.Shared.Middleware;
using FluentValidation;      //AddValidatorsFromAssemblyContaining
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; 
using Serilog;
using StackExchange.Redis;// for — IConnectionMultiplexer, ConnectionMultiplexer
using System;
using System.Reflection;// Assembly.GetExecutingAssembly()

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


    builder.Services.AddTransient(                                      
    typeof(IPipelineBehavior<,>),                                  
    typeof(ValidationBehavior<,>));



    // ConnectionMultiplexer is the StackExchange.Redis client.
    // It manages a persistent pool of connections to Redis.
    //
    // MUST be Singleton: ConnectionMultiplexer is thread-safe and designed
    // to be shared across all requests. Creating a new one per request
    // (Scoped/Transient) creates a new TCP connection per request,
    // exhausting Redis connection limits almost instantly.
    //
    // .Connect() is called at app startup (during builder.Build()).
    // This means: if Redis is NOT running when you start the .NET app,
    // you get a RedisConnectionException immediately.
    // Solution: always run "docker compose up -d" before "dotnet run".
    //
    // The ! (null-forgiving operator) tells the compiler we guarantee
    // the connection string exists at runtime (we set it in appsettings.json).

    var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString!));






    //i m Binding  the "JwtSettings" section from appsettings.json
    // to the JwtSettings record using the Options pattern.
    //
    // This single call registers IOptions<JwtSettings> in the DI container.
    // Any class that needs JWT config can now inject IOptions<JwtSettings>
    // and read .Value to get a fully-populated, type-safe JwtSettings object.
    //
    // Why GetSection("JwtSettings")?
    //   → It tells the binder to look at ONLY the JwtSettings sub-section,
    //     not the entire appsettings.json root. The keys within that section
    //     ("Secret", "Issuer", "Audience", "ExpiryMinutes") are matched to
    //     the record's property names case-insensitively.

    builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

    var app = builder.Build();

    app.UseSerilogRequestLogging();

   //ADDING ALL MIDDLEWARE HERE

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