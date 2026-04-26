namespace FlashDrop.API.Modules.Identity.DTOs
{


    //LoginResponse gives the client everything it needs
    // to use the JWT token and know when to refresh it.
    //
    // Token     → the JWT string the client sends in subsequent requests
    //             as: Authorization: Bearer <Token>
    //
    // ExpiresAt → the UTC timestamp when the token expires.
    //             Clients use this to show "session expires in X minutes"
    //             or to proactively refresh before the token expires.
    //             Without this, clients must decode the JWT themselves
    //             (which requires a JWT library on the client side).
    public record LoginResponse
   (
        string Token,
        DateTime  ExpiresAt

        //in Future if required you can use Refresh Token as well  with accessToken( here token is same as AccessToken)
        
        );
}
