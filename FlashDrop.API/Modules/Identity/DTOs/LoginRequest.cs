namespace FlashDrop.API.Modules.Identity.DTOs
{

    // LoginRequest carries only what the login endpoint needs.
    // Email  → used to find the user in the database
    // Password → compared against the stored BCrypt hash
    //
    // No Id, no Role, no Token — those are determined server-side
    public record LoginRequest
    (

       
        string Email,
        string Password
        
        
        );
    
}
