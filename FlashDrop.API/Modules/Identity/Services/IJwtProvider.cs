
using FlashDrop.API.Modules.Identity;//For AppUser type used in method signature


namespace FlashDrop.API.Modules.Identity.Services
{
    public interface IJwtProvider
    {

     // this function  Takes a fully-loaded AppUser entity(including Id, Email, Role)
    // and returns a signed JWT string ready to send back to the client.
    // Returns a string (not Task<string>) because token generation is a CPU-bound
    // synchronous cryptographic operation — there's no I/O await needed.

        string GenerateToken(AppUser user);


        //we can use this as well when we want to work on access token as well for validation
        //ClaimsPrincipal? ValidateAccessToken(string token); // returns null if invalid
    }
}
