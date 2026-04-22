using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
////   Microsoft.EntityFrameworkCore → DbContext, DbContextOptions, ModelBuilder

namespace FlashDrop.API.Shared.Data



{
    public class FlashDropDbContext : DbContext
    {

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
        }
    }
}
