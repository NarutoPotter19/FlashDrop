using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
////   Microsoft.EntityFrameworkCore → DbContext, DbContextOptions, ModelBuilder


using FlashDrop.API.Modules.Identity;
using FlashDrop.API.Modules.Catalog;// for the  Product type we have cewated in Catalog module


namespace FlashDrop.API.Shared.Data



{
    public class FlashDropDbContext : DbContext
    {



        // DbSet<AppUser> tells EF Core:
        //   1. There is a table called "Users" in the database
        //      (EF Core pluralises "AppUser" → "AppUsers" by default,
        //       but we name the DbSet property "Users" explicitly
        //       so the table name will be "Users")
        //   2. The "Users" table maps to the AppUser C# class
        //   3. context.Users gives access to all CRUD operations on this table
        public DbSet<AppUser> Users { get; set; }

        //— DbSet<Product>
        //
        // Maps to the "Products" table in PostgreSQL.
        // All CRUD operations on products go through context.Products:
        //   context.Products.Add(product)         → INSERT
        //   context.Products.ToListAsync()         → SELECT *
        //   context.Products.FindAsync(id)         → SELECT WHERE Id = @id
        //   context.Products.Remove(product)       → DELETE (we never do this — IsActive instead)

        public DbSet<Product> Products { get; set; }

        public FlashDropDbContext(DbContextOptions options) : base(options)
        {


                 //onstructor receives DbContextOptions from DI.
                 // When we call AddDbContext<FlashDropDbContext>(...) in Program.cs,
                 // EF Core builds a DbContextOptions object containing:
                 //   - The database provider (Npgsql / PostgreSQL)
                 //   - The connection string (read from appsettings.json)
                 //   - Any other EF Core options (lazy loading, tracking behaviour, etc.)
                 // This options object is passed here automatically by DI — you never
                 // call this constructor manually.


       
        }




        protected FlashDropDbContext(DbContextOptions<FlashDropDbContext> options) : base(options)
        {
            // The base(options) call passes everything to EF Core.
        }



        //OnModelCreating is the place where we configure:
    //   - Unique indexes (e.g., unique email on AppUser)
    //   - Column type overrides (e.g., decimal precision for Price)
    //   - Relationships (foreign keys between entities)
    //   - Concurrency tokens (e.g., [ConcurrencyCheck] on Product.Stock)
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            //sk: Always call base first — it runs EF Core's default
            // conventions (auto-detecting primary keys, pluralizing table names, etc.)

            base.OnModelCreating(modelBuilder);




           //  Effect in the database(PostgreSQL):
        //   CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
        //
        // This SQL is generated when EF Core migrations run (Prompt 25).
        // Until migrations run, this configuration only exists in memory.
        //
        // Why IsUnique() matters:
        //   - Two users cannot have the same email
        //   - PostgreSQL rejects the INSERT and throws a unique constraint violation
        //   - The index also speeds up the WHERE Email = @email query in /login
            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.Email)
                .IsUnique();





            //— Product SKU unique index
            //
            // Every product has a unique SKU (Stock Keeping Unit).
            // This index enforces that uniqueness at the database level.
            //
            // Generated SQL (runs in Prompt 25 migration):
            //   CREATE UNIQUE INDEX "IX_Products_SKU" ON "Products" ("SKU");
            //
            // Benefits:
            //   1. Database rejects duplicate SKUs — no duplicate products
            //   2. Lookup by SKU is O(log n) — fast even at millions of rows



            modelBuilder.Entity<Product>()                             
            .HasIndex(p => p.SKU)                                 
            .IsUnique();



            //— Product Price column type
            //
            // Without this configuration, EF Core maps C# decimal to
            // PostgreSQL 'numeric' with no precision specified.
            // That works, but being explicit about decimal(18,2) is:
            //   - Clearer for anyone reading the schema
            //   - Consistent with SQL Server convention (common in .NET)
            //   - Ensures 2 decimal places maximum (cents precision)
            //
            // decimal(18,2) means:
            //   18 = total significant digits
            //    2 = digits after the decimal point (cents)
            // Max storable price: 9,999,999,999,999,999.99

            modelBuilder.Entity<Product>()                            
           .Property(p => p.Price)                                
           .HasColumnType("decimal(18,2)");
        }
    }
}
