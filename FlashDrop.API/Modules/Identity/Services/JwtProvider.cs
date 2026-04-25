using FlashDrop.API.Modules.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;// // IOptions<T> — to inject JwtSettings and apply option pattern on auth confg
using Microsoft.IdentityModel.Tokens;//singing credentials  and security Algorithms
using System.IdentityModel.Tokens.Jwt; // this will deal with JWTSecurityToken and JWTTokenHandler
using System.Security.Claims;  //jwt registerd claims ,claims name and claims type 
using System.Text;//Encoding UTF8


namespace FlashDrop.API.Modules.Identity.Services
{

    // task: JwtProvider is the concrete implementation of IJwtProvider.
    // It is responsible for building, signing, and serializing JWT tokens.
    //
    // Token anatomy:
    //   Header  → algorithm: HS256, type: JWT
    //   Payload → claims: Sub, Email, Role, Jti  (see comments below)
    //   Signature → HMAC-SHA256(header + payload, secret_key)
    public class JwtProvider : IJwtProvider
    {


        //tore the settings as a private readonly field.
        // _jwtSettings.Value is accessed once in the constructor and stored.
        private readonly JwtSettings _jwtSettings;



        //Constructor — injects IOptions<JwtSettings> from DI.
        // IOptions<JwtSettings> is registered in Program.cs via:
        //   services.Configure<JwtSettings>(config.GetSection("JwtSettings"))
        // Calling .Value unwraps the IOptions wrapper and gives us the raw record.

        public JwtProvider(IOptions<JwtSettings> jwtsettings)
        {
            _jwtSettings = jwtsettings.Value;
        }



        public string GenerateToken(AppUser user)
        {
            var claims = new[]
            {

                 // Sub (Subject): The unique identifier for who the token belongs to.
            // We use the user's Guid Id, converted to string.
            // AuthController  reads this via HttpContext.User as our claimPrinciple can be accessd by httpContext.User to
            // identify which user is making the authenticated request.
                new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()),


                // Email: The user's email address.
            // Useful for display purposes or secondary lookups.


                new Claim(JwtRegisteredClaimNames.Email,user.Email),

                 // Role: "Admin" or "Customer" (we will set this ).
            // This is the claim that powers [Authorize(Roles = "Admin")].
            // ProductController.CreateProduct  uses this which we will going to create going forward.

            //IMP:  Must use ClaimTypes.Role (not JwtRegisteredClaimNames) so that
            // ASP.NET Core's [Authorize(Roles = ...)] attribute recognizes it.
                new Claim(ClaimTypes.Role,user.Role),

                // Jti (JWT ID): A unique GUID per token.
            // Prevents replay attacks — if an attacker captures a token and
            // replays it, the Jti can be checked against a blacklist (future feature). 
                new Claim(JwtRegisteredClaimNames.Jti,Guid.NewGuid().ToString())
            };


            //— Create the signing key.
        // We derive a SymmetricSecurityKey from the Secret string.
        // UTF8.GetBytes converts the string to raw bytes the crypto library needs.
        // The secret MUST be at least 32 bytes (256 bits) for HMAC-SHA256.

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));




            var credentials = new SigningCredentials(key, 
                SecurityAlgorithms.HmacSha256);




            // — Build the JWT token object.
            // JwtSecurityToken is the structured object before serialization.
            var token = new JwtSecurityToken(

                // Matches "Issuer" in appsettings.json and ValidIssuer
                issuer: _jwtSettings.Issuer,

                // Matches "Audience" in appsettings.json and ValidAudience
                audience: _jwtSettings.Audience,

                //here we use the claims that we have created above 
                claims: claims,

                // Token becomes invalid after ExpiryMinutes from now (UTC).
                // Using UTC is critical — always use UtcNow for tokens, never Now.
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),

                //using the signing credentials that we have created above to sign the token
                signingCredentials: credentials
                );


            //— Serialize to the "eyJ..." string format.
            // JwtSecurityTokenHandler.WriteToken encodes the header + payload as
            // base64url and appends the HMAC-SHA256 signature.
            // The result is what we return in the HTTP response body to the client.
                        return new JwtSecurityTokenHandler().WriteToken(token);



        }

    }
}
