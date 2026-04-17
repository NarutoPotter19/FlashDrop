namespace FlashDrop.API.Shared
{

    /*
     * why abstract : As you can never do "new BaseEntity()" directly.
 Only concrete subclasses (AppUser, Product, Order) can be instantiated.
    
     */

    public abstract class BaseEntity
    {

        // : Guid is used instead of int because:
        // 1. No database round-trip needed to generate an ID
        // 2. IDs don't expose record counts (security)
        // 3. Works correctly in any distributed scenario
        public Guid Id { get; set; }

        // Prompt3 Task: UTC timestamp of when this entity was first saved.
        // We use UtcNow (not Now) to avoid timezone bugs across servers.

        public DateTime CreatedAt { get; set; }


        // Defaulr constructor is going to intilise the mebers Without this, if you forget to set Id before SaveChanges(),
        // EF Core would insert an empty Guid (00000000-0000-0000-0000-000000000000)
        // which would cause primary key conflicts if it happened twice.

        protected BaseEntity()
        {

            // Guid.NewGuid() generates a cryptographically random UUID
            Id = Guid.NewGuid();

            // DateTime.UtcNow captures the exact creation moment in UTC
            CreatedAt = DateTime.UtcNow;
        }
    }
}
