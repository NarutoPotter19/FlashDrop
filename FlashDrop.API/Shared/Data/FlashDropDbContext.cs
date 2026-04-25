using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
////   Microsoft.EntityFrameworkCore → DbContext, DbContextOptions, ModelBuilder


using FlashDrop.API.Modules.Identity;


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
        }
    }
}
