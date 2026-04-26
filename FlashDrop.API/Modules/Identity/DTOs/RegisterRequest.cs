namespace FlashDrop.API.Modules.Identity.DTOs
{
    public record RegisterRequest
    (

        //Name — the user's display name.
        // Validated by ASP.NET Core model binding: non-null required.
        // We could add FluentValidation here too, but for brevity
        // we keep register validation in-controller for this project.
        string Name,

        // Email — must be unique in the database.
        // The duplicate check is done inside the controller action,
        // not here. The DTO just carries the value.

        string Email,

        // Password — the raw plain-text password from the client.
        // CRITICAL: This is ONLY ever kept in memory momentarily.
        // It is IMMEDIATELY hashed by BCrypt. The raw value is NEVER written
        // to the database, never logged, never returned to the client.
        string Password
    );
}
