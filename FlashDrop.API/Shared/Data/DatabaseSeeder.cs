using FlashDrop.API.Modules.Catalog;       // Product entity
using FlashDrop.API.Modules.Identity;      // AppUser entity
using Microsoft.EntityFrameworkCore;       // AnyAsync, CountAsync, AddRangeAsync
using Microsoft.Extensions.DependencyInjection; // IServiceScope, GetRequiredService
using Serilog;                             // Log.Information


namespace FlashDrop.API.Shared.Data
{

    //Static class — cannot be instantiated.
    // Called once from Program.cs: await DatabaseSeeder.SeedAsync(app.Services)
    public static class DatabaseSeeder
    {

        //SeedAsync receives IServiceProvider (app.Services).
        // This is the ROOT service provider — it provides Singleton services directly.
        // To get a SCOPED service (FlashDropDbContext is Scoped), we must create a scope.

        // WHY async? We use async EF Core methods(AnyAsync, SaveChangesAsync).
        // WHY IServiceProvider? Decouples the seeder from DI registration — it resolves
        // its own dependencies at call time rather than having them injected.

        public static async Task SeedAsync(IServiceProvider services)
        {


            // // 'using var scope' ensures the scope (and its Scoped services) are
            // disposed when the using block exits — prevents connection leaks.
            using var scope = services.CreateScope();

            //Resolve FlashDropDbContext from the scope's provider.
        // GetRequiredService<T> throws InvalidOperationException if T is not
        // registered — better than GetService<T> which returns null silently.
            var context = scope.ServiceProvider
            .GetRequiredService<FlashDropDbContext>();

            //var logger = scope.ServiceProvider
            // .GetRequiredService<ILogger<DatabaseSeeder>>();

            // 1. Get the factory
            var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();//Resolve ILogger for structured startup logging.

            // 2. Create a logger with a custom string name instead of a <Type>
            var logger = loggerFactory.CreateLogger("DatabaseSeeder");






            //Idempotency check.
        // AnyAsync(u => u.Role == "Admin") generates:
        //   SELECT EXISTS(SELECT 1 FROM "Users" WHERE "Role" = 'Admin')
        // If an Admin already exists → skip creation entirely.
        // This makes seeding safe to run on every app restart.

            var adminExists = await context.Users
           .AnyAsync(u => u.Role == "Admin");


            if (!adminExists)
            {

                var adminUser = new AppUser
                {
                    Name = "System Administrator",
                    Email = "admin@flashdrop.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
                    Role = "Admin"
                    // Id and CreatedAt auto-set by BaseEntity constructor
                };

                context.Users.Add(adminUser);
                await context.SaveChangesAsync();

                Log.Information("Admin user seeded: {Email}", adminUser.Email);

            }




            // ── SEED PRODUCTS 

            var productCount = await context.Products.CountAsync();

            if (productCount < 5)
            {
                var products = new List<Product>
            {
                new Product
                {
                    Name        = "Air Jordan 1 Retro High OG Chicago",
                    SKU         = "AJ1-RED-10",
                    Price       = 299.99m,   // 'm' suffix = decimal literal
                    Description = "The iconic Air Jordan 1 in the classic Chicago colorway. " +
                                  "Red, Black, and White leather upper with Nike Air cushioning.",
                    Stock       = 50,
                    IsActive    = true
                },
                new Product
                {
                    Name        = "Adidas Yeezy Boost 350 V2 Black",
                    SKU         = "YZ350-BLK-9",
                    Price       = 249.99m,
                    Description = "Kanye West x Adidas collaboration. Full-length Boost midsole " +
                                  "with Primeknit upper in triple-black colorway.",
                    Stock       = 30,
                    IsActive    = true
                },
                new Product
                {
                    Name        = "Nike Dunk Low White Black Panda",
                    SKU         = "NK-DUNK-WHT-11",
                    Price       = 120.00m,
                    Description = "The Panda Dunk. Classic two-tone leather upper in " +
                                  "white and black with padded collar.",
                    Stock       = 100,
                    IsActive    = true
                },
                new Product
                {
                    Name        = "Adidas NMD R1 Blue Boost",
                    SKU         = "AD-NMD-BLU-10",
                    Price       = 179.99m,
                    Description = "Street-ready NMD with full-length Boost midsole, " +
                                  "Primeknit upper, and signature plug accents in blue.",
                    Stock       = 45,
                    IsActive    = true
                },
                new Product
                {
                    Name        = "New Balance 550 Grey White",
                    SKU         = "NB-550-GRY-9",
                    Price       = 139.99m,
                    Description = "Retro basketball silhouette with premium leather upper " +
                                  "in grey and white. Lightweight EVA midsole.",
                    Stock       = 60,
                    IsActive    = true
                }
            };


                //AddRangeAsync is more efficient than calling Add() 5 times.
                // It batches all 5 INSERTs into a single database round-trip.

                await context.Products.AddRangeAsync(products);
                await context.SaveChangesAsync();
               Log.Information("Seeded {Count} products into the database.", products.Count);
            }

            else
            {
                Log.Information("Products already seeded ({Count} exist). Skipping product seed.",
                    productCount);
            }

        }
    }
}
