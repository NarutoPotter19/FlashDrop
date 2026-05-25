
# STAGE 1: BUILD
# Uses the full .NET 9 SDK image (~800MB) to compile and publish.
# This stage is discarded after building — not included in final image.

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
 
# Set the working directory inside the container.
# All subsequent COPY and RUN instructions execute relative to this folder.
# /src is a conventional name for source code in Docker builds.
WORKDIR /src
 
# ── LAYER CACHE OPTIMISATION — Copy .csproj FIRST ────────────────────────
#
# WHY copy the .csproj before copying all source files?
#
# Docker builds images in layers. Each instruction (FROM, COPY, RUN) creates
# a new layer that is cached after the first build. On subsequent builds,
# Docker checks if the layer's inputs changed:
#   - If UNCHANGED → use the cached layer (skip the instruction entirely)
#   - If CHANGED   → re-run this instruction AND all instructions below it
#
# The .csproj file only changes when you ADD, REMOVE, or UPDATE a NuGet package.
# Your source code (.cs files) changes every time you write code.
#
# By separating them:
#   COPY .csproj → RUN dotnet restore   ← cached unless packages change
#   COPY . .     → RUN dotnet publish   ← re-runs on any source code change
#
# Without this pattern (if you copied everything at once):
#   COPY . .     → RUN dotnet restore   ← re-runs on EVERY source code change
#   COPY . .     → RUN dotnet publish   ← re-runs on EVERY source code change
#   This means a 30-120 second package restore on every single rebuild.
#
# With this pattern, a typical code change only re-runs dotnet publish
# (skipping the expensive restore step). Saves 30-120 seconds per rebuild.
COPY FlashDrop.API.csproj .
 
# Restore NuGet packages.
# Downloads all packages listed in FlashDrop.Api.csproj from NuGet.
# Result: /src/.nuget (or the shared cache) populated with package DLLs.
# This layer is CACHED as long as FlashDrop.Api.csproj doesn't change.
RUN dotnet restore
 
# Now copy all remaining source files.
# The . (source) copies everything in the Docker build context (FlashDrop.Api/ folder).
# The . (destination) is the current WORKDIR (/src).
#
# This COPY invalidates the cache whenever ANY source file changes.
# That's expected — if code changed, we need to rebuild.
#
# .dockerignore (create in Prompt 50 or optionally now) can exclude:
#   bin/, obj/, .git/, logs/ — files that shouldn't be in the build context.
COPY . .
 
# Compile and publish the application in Release configuration.
#
# -c Release:
#   Compiles with optimisations (inlining, dead code elimination).
#   No debug symbols embedded in the output DLLs.
#   The correct configuration for any container/production deployment.
#   Never use Debug for production — it's slower and larger.
#
# -o /app/publish:
#   Output directory inside this build container.
#   Contains: FlashDrop.Api.dll, all dependency DLLs, appsettings.json, etc.
#   Does NOT contain: source code (.cs files), obj/ folder, SDK tools.
#   This /app/publish folder is what Stage 2 copies in Prompt 49.
RUN dotnet publish -c Release -o /app/publish
 
# ── END OF STAGE 1 ───────────────────────────────────────────────────────

# STAGE 2: RUNTIME

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime           
                                                                 
#  Set working directory inside the runtime container.
# /app is the conventional location for the application in Docker.
WORKDIR /app                                                  
                                                            
# Copy the published output from Stage 1 into this stage.
# --from=build: references Stage 1 by its alias "build"
# /app/publish: the output directory specified in dotnet publish -o
# . (destination): the current WORKDIR (/app)
#
# After this COPY, /app/ contains:
#   FlashDrop.Api.dll, all dependency .dlls, appsettings.json, etc.
# The Stage 1 SDK container is now completely discarded —
# its ~800MB of compiler + tools are not present in this final image.
COPY --from=build /app/publish .                                
                                                                
#  Expose port 8080 — the port Kestrel listens on inside the container.
#
# WHY 8080 and not 5000?
#   Port 5000 is the default for local 'dotnet run' development.
#   Port 8080 is the Microsoft-recommended port for containerised .NET apps
#   (introduced as default from .NET 8 onwards).
#   Port 8080 is a non-privileged port (>1024) — no root access required,
#   which aligns with container security best practices.
#
# EXPOSE is DOCUMENTATION — it does not publish the port to the host machine.
# Actual host port mapping is done in docker-compose.yml:
#   ports: "5000:8080"  means  host:5000 → container:8080
EXPOSE 8080                                                   
                                                              
# [+] Configure Kestrel to listen on port 8080 inside the container.
# http://+:8080 means "listen on all network interfaces on port 8080".
# The + is equivalent to 0.0.0.0 — accepts connections from any IP.
# Without this, Kestrel would default to port 5000 and EXPOSE 8080 would be ignored.
ENV ASPNETCORE_URLS=http://+:8080                             
                                                               
# [+] Set the runtime environment to Production.
#
# Effects of ASPNETCORE_ENVIRONMENT=Production:
#   1. Swagger UI DISABLED:
#      In Program.cs: if (app.Environment.IsDevelopment()) { app.UseSwagger()... }
#      Production is not Development → Swagger never registered → 404 at /swagger
#      NEVER expose API documentation to the public internet.
#
#   2. Detailed error pages DISABLED:
#      ASP.NET Core does not return stack traces to clients in Production.
#      ExceptionHandlingMiddleware still returns clean RFC 7807 JSON — correct.
#
#   3. Configuration layering:
#      Reads: appsettings.json (base) + appsettings.Production.json (overrides)
#      appsettings.Development.json is IGNORED in Production.
#
#   4. Logging adjustments:
#      Serilog respects MinimumLevel overrides for Microsoft.* namespaces.
#
# In the final docker-compose.yml (Prompt 50), connection strings are provided
# via environment variables — these OVERRIDE appsettings.json values at runtime.
ENV ASPNETCORE_ENVIRONMENT=Production                   
                                                              
# [+] Set the entry point for the container.
#
# EXEC FORM ["dotnet", "FlashDrop.Api.dll"] is required (not shell form).
#
# WHY exec form over shell form?
#   Shell form:  dotnet FlashDrop.Api.dll
#     Docker runs: /bin/sh -c "dotnet FlashDrop.Api.dll"
#     /bin/sh is PID 1, dotnet is a child process.
#     When Docker sends SIGTERM to stop the container: /bin/sh receives it.
#     Many shells don't forward signals to children → .NET never gets SIGTERM.
#     Result: Docker waits 10 seconds, then sends SIGKILL → ungraceful shutdown.
#     Consequences: in-flight requests killed mid-processing, connections not closed,
#                   Serilog's CloseAndFlush never called → last logs lost.
#
#   Exec form:  ["dotnet", "FlashDrop.Api.dll"]
#     Docker runs: dotnet FlashDrop.Api.dll (the .NET process IS PID 1)
#     When Docker sends SIGTERM: .NET process receives it directly.
#     ASP.NET Core graceful shutdown: drains in-flight requests, closes connections,
#                   calls ApplicationStopped events (including Log.CloseAndFlush).
#     Result: clean, graceful shutdown.
ENTRYPOINT ["dotnet", "FlashDrop.API.dll"]                     
 

 
