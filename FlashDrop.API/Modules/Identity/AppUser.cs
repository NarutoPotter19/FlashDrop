using FlashDrop.API.Shared;  //// Required to inherit BaseEntity

namespace FlashDrop.API.Modules.Identity
{

    //AppUser inherits BaseEntity(A model which i have already created previously).
    // This gives it two FREE properties without writing them here:
    //   - Guid Id          (auto-set to Guid.NewGuid() in BaseEntity constructor)
    //   - DateTime CreatedAt (auto-set to DateTime.UtcNow in BaseEntity constructor)

    // Result: when you do "new AppUser()", Id and CreatedAt are ALREADY set.
    // You only need to set Name, Email, PasswordHash, and Role.
    public class AppUser : BaseEntity
    {

        // "= string.Empty" is a C# nullable best practice.
        // With <Nullable>enable</Nullable> in .csproj, non-nullable strings
        // must be initialized. string.Empty prevents compiler warning CS8618.
        public string Name { get; set; } = string.Empty;// this is profile display name, not email




        // string Email — the unique login identifier.
        // UNIQUE INDEX is configured in FlashDropDbContext.OnModelCreating.
        // This index means:
        //   1. Database rejects duplicate emails (constraint enforcement)
        //   2. Lookups by email are O(log n) fast (B-tree index)
        public string Email { get; set; } = string.Empty;// this is used for login, must be unique

        // string PasswordHash — the BCrypt hash of the password.
        // BCrypt.Net-Next.BCrypt.HashPassword("mypassword") produces a string like:
        //   "$2a$11$abc123...xyz" (always 60 characters)
        //
        // CRITICAL SECURITY RULE:
        // The raw password ("mypassword") is NEVER stored anywhere in the database.
        // Only this hash is stored. To verify a login attempt:
        //   BCrypt.Verify("userEnteredPassword", storedHash) → returns true/false
        // There is NO way to reverse a BCrypt hash back to the original password.
        public string PasswordHash { get; set; } = string.Empty;

        // Used by [Authorize(Roles = "Admin")] attribute.
        // When the JWT is created (Prompt 12), this Role value is embedded as a claim.
        // ASP.NET Core reads the claim on each request to enforce role requirements.
        public string Role { get; set; } = string.Empty;

    }
}
