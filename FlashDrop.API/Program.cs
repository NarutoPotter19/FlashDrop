using AutoMapper;
using FlashDrop.API.Modules.Identity;
using FlashDrop.API.Modules.Identity.Services;// for using Identity serivices we have created  IJwtProvider, JwtProvider
using FlashDrop.API.Shared.Behaviors;

using FlashDrop.API.Shared.Middleware;
using FluentValidation;      //AddValidatorsFromAssemblyContaining
using MediatR; // Required for JwtBearerDefaults.AuthenticationScheme constant
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection; 
using Microsoft.IdentityModel.Tokens;// Required for TokenValidationParameters and SymmetricSecurityKey
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models; // — OpenApiSecurityScheme etc.
using Microsoft.Win32;
using Serilog;
using StackExchange.Redis;// for — IConnectionMultiplexer, ConnectionMultiplexer
using System;
using System.Net;
using System.Reflection;
using System.Text;// Assembly.GetExecutingAssembly()
using FlashDrop.API.Shared.Services;//we are going to use IChace service and RedisCacheService in our program.cs to register them in DI container

using FlashDrop.API.Shared.Data;// for databse seeder abd flashdbcontext which we going to register in here for database seeding at startup of the APPLICATION



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


    //builder.Services.AddSwaggerGen();
    builder.Services.AddSwaggerGen(options =>
    {
        // Telling  Swagger a security scheme named "Bearer" exists.
        // This creates the "Authorize" button in Swagger UI and the
        // text field where users paste their JWT token.
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {

            // Name: the HTTP header that carries the token
            Name = "Authorization",

            //Type.Http: This is an HTTP header scheme (as opposed to
            //     OAuth2 which has its own type). For JWT Bearer, use Http.
            Type = SecuritySchemeType.Http,

            //     Scheme "bearer": tells Swagger the prefix is "Bearer ".
            //     Note: lowercase "bearer" here is correct per OpenAPI spec.
            //     Swagger UI will prepend "Bearer " automatically —
            //     you only paste the raw token, NOT "Bearer eyJ..."
            Scheme = "bearer",

            //earerFormat "JWT": documentation only — tells API consumers
            //     the format of the token. Has no effect on validation.
            BearerFormat = "JWT",

            // In: the token is in the request Header(not query string / cookie)
            In = ParameterLocation.Header,

            //Description: shown to users inside the Swagger UI dialog box
            Description = "Enter your JWT token below.\n\n" +
            "Get a token: POST /api/auth/login\n" + "Example value: eyJhbGciOiJIUzI1NiIs..."
        });




        //Step 2: Apply the Bearer scheme globally.
        //
        // OpenApiSecurityRequirement is a dictionary where:
        //   KEY   = the security scheme reference
        //   VALUE = list of required scopes (empty for Bearer/JWT — scopes are OAuth2 only)
        //
        // This marks ALL endpoints in Swagger UI as requiring auth.
        // IMPORTANT: This is Swagger UI behaviour only. The actual enforcement
        // of authentication on each endpoint is done by [Authorize] attributes
        // + the JWT middleware registered in Program.cs (Prompt 13).
        // Endpoints WITHOUT [Authorize] still work without a token at runtime.

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme                               // [+]
            {                                                        // [+]
                Reference = new OpenApiReference                    // [+]
                {                                                    // [+]
                    // [+] ReferenceType.SecurityScheme: we are referencing
                    //     a security scheme (not a path, component, etc.)
                    Type = ReferenceType.SecurityScheme,            // [+]
                                                                    // [+]
                    // [+] Id "Bearer": MUST exactly match the name used in
                    //     AddSecurityDefinition above. Case-sensitive.
                    Id = "Bearer"                                   // [+]
                }                                                    // [+]
            },
                Array.Empty<string>() // No scopes required for Bearer/JWT
            }
        });




    }
        );




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



    // IHttpContextAccessor provides access to HttpContext (and thus
    // HttpContext.User.Claims) from classes that are NOT controllers —

    // WHY Singleton? HttpContextAccessor itself is thread-safe — it stores
    // a reference to the current HttpContext via AsyncLocal<T>, which is
    // inherently scoped to the current async execution context. Multiple
    // handlers on different requests each see their own HttpContext.
    builder.Services.AddHttpContextAccessor();



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



    builder.Services.AddSingleton<ICacheService, RedisCacheService>();

    // Register IEventBus as Singleton
    //
    // AddSingleton<IEventBus, RabbitMqEventBus>():
    //
    // WHY Singleton?
    //   RabbitMqEventBus holds a persistent AMQP connection (_connection)
    //   and a channel (_channel). These are expensive to create (TCP handshake,
    //   AMQP negotiation). Creating one per request (Scoped) would be:
    //     - Slow: each request opens and closes a TCP connection
    //     - Wasteful: RabbitMQ has connection limits (~100 by default)
    //   Singleton: ONE connection shared across all requests for the app lifetime.
    //
    // WHY placed here (after Redis, before authentication)?
    //   No strict ordering requirement. Placed near ICacheService by convention
    //   (both are shared infrastructure services).
    //
    // STARTUP SIDE EFFECT:
    //   When DI resolves RabbitMqEventBus for the first time (at Build()),
    //   the constructor runs — which tries to connect to RabbitMQ.
    //   If RabbitMQ is not running, the retry loop runs (3 attempts, 2s delay).
    //   If all 3 attempts fail, the app throws and refuses to start.
    //   This is intentional — the app requires RabbitMQ to function.

    builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();





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



    // Register IJwtProvider interface → JwtProvider concrete class.
    // Lifetime: Scoped (one instance per HTTP request).

    // Why Scoped?
    //   JwtProvider is request-bound work (generate a token for this specific login).
    //   It reads IOptions<JwtSettings> (which is already a singleton — safe to inject
    //   into scoped services). No shared mutable state, so scoped is appropriate.
    //
    // How it's used:
    //   AuthController.Login  will inject IJwtProvider and call
    //   GenerateToken(user) after credential verification, returning the token to client.
    builder.Services.AddScoped<IJwtProvider, JwtProvider>();


    // Register the JWT Bearer authentication handler.
    //
    // What this does:
    //   1. Sets the DEFAULT authentication scheme to "Bearer"
    //      → When a request arrives, ASP.NET Core looks for "Authorization: Bearer <token>"
    //   2. Registers the JWT Bearer handler that knows how to read + validate JWT tokens
    //   3. Configures TokenValidationParameters — the RULES for what makes a token valid
    //
    // Note: We re-read JwtSettings here via GetSection().Get<>() instead of injecting
    // IOptions<JwtSettings>, because builder.Services.Add... runs BEFORE the DI container
    // is built. IOptions<T> is only available AFTER app.Build(). GetSection().Get<>()
    // reads from configuration directly, which is always available on the builder.

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {

        //Register the JWT Bearer authentication handler.
//
// What this does:
//   1. Sets the DEFAULT authentication scheme to "Bearer"
//      → When a request arrives, ASP.NET Core looks for "Authorization: Bearer <token>"
//   2. Registers the JWT Bearer handler that knows how to read + validate JWT tokens
//   3. Configures TokenValidationParameters — the RULES for what makes a token valid
//
// Note: We re-read JwtSettings here via GetSection().Get<>() instead of injecting
// IOptions<JwtSettings>, because builder.Services.Add... runs BEFORE the DI container
// is built. IOptions<T> is only available AFTER app.Build(). GetSection().Get<>()
// reads from configuration directly, which is always available on the builder.

        var jwtSettings =builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;

        options.TokenValidationParameters = new TokenValidationParameters
        {

            // Checks that the token's "iss" claim matches our configured issuer.
            // Prevents tokens issued by other APIs from being accepted here.
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            // Checks that the token's "aud" claim matches our configured audience.
            // Prevents cross-service token misuse.
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            // Checks that the current UTC time is BEFORE the token's "exp" claim.
            // Expired tokens are rejected even if everything else is valid.
            ValidateLifetime = true,



            //VALIDATE SIGNING KEY
            // Verifies the HMAC-SHA256 signature using our secret key.
            // If a token was tampered with (payload modified), the signature
            // won't match and the token is rejected.
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSettings.Secret)),



            // CLOCK SKEW = ZERO
            // By default, JWT validation allows a 5-minute grace period after expiry
            // to handle clock drift between servers.
            // Setting this to Zero means tokens expire exactly at their "exp" timestamp.
            // For FlashDrop (a flash-sale app), exact timing matters.

            ClockSkew = TimeSpan.Zero,





        };




    });










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

    //: UseRouting() must be called explicitly before UseAuthentication.
    // In .NET 9 this is called implicitly, but being explicit ensures correct ordering
    // and avoids subtle bugs when middleware is added/reordered in future prompts.
    // UseRouting matches the incoming request URL to an endpoint (controller action).
    app.UseRouting();
  //UseAuthentication() — reads the "Authorization: Bearer <token>"
// header, validates the JWT using TokenValidationParameters (configured above),
// and if valid, populates HttpContext.User with the token's claims.
    app.UseAuthentication();




    app.UseAuthorization();
    app.MapControllers();


    //Run DatabaseSeeder at startup

    // WHY here? The seeder needs the DI container (app.Services) to
    // resolve FlashDropDbContext. The container is only ready after
    // app.Build() completes. And seeding must complete before the
    // app starts accepting HTTP requests.
    //
    // await: seeding is async (EF Core calls). We must await it to
    // ensure seeding finishes before the HTTP server starts.
    // Using 'await' requires the top-level code to be async-capabl

    await DatabaseSeeder.SeedAsync(app.Services);


    ////Testing :
    ///TEMPORARY — DELETE AFTER TESTING reesponse should be 401 unauthorise d
    //app.MapGet("/test-auth", [Microsoft.AspNetCore.Authorization.Authorize] () =>
    //    "You are authenticated!")
    //   .WithName("TestAuth");

    // TEMPORARY TEST — delete after verifying, before git commit
    app.MapGet("/test-cache", async (ICacheService cache) =>
    {
        // Write to cache
        await cache.SetAsync("test-key", new { Name = "CacheTest", Value = 42 }, TimeSpan.FromMinutes(1));
        // Read from cache
        var result = await cache.GetAsync<object>("test-key");
        return Results.Ok(result);
    });


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