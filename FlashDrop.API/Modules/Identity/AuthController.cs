using FlashDrop.API.Modules.Identity.DTOs;// we are going to be needing the DTOs that we have created for the Identity 
using FlashDrop.API.Modules.Identity.Services;
using FlashDrop.API.Shared.Data;// for accesing the database context(FlashDropDbContext) and the entities
using Microsoft.AspNetCore.Mvc;// for using the controller and the action result types==>ControllerBase, ApiController, Route, etc.
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client.Exceptions;// we can use variouid Linq and EFCore realted method such as : AnyAsync, FindAsync, etc.



namespace FlashDrop.API.Modules.Identity
{

    //this is Our Auth Contoller which repersent ot hadnle request at api/auth
    //[ApiController] does two important things:
    //   1. Enables automatic model validation — if [Required] fields are missing,
    //      ASP.NET Core returns HTTP 400 automatically (before your action runs)
    //   2. Enables automatic [FromBody] inference — you don't need to explicitly
    //      write [FromBody] on action parameters that come from the request body

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {


        private readonly FlashDropDbContext? _context;//why ? because we need to access the database to check if the email is already exist or not and to add the new user to the database

        //Inject IJwtProvider for token generation.
        // Used in  (login). Injected now so the constructor
        // is complete — adding it later would require modifying the constructor.

        private readonly IJwtProvider? _jwtProvider;


        //Constructor injection:
        // ASP.NET Core DI automatically provides both dependencies
        // because we registered them in Program.cs:
        //   AddDbContext<FlashDropDbContext>() → provides FlashDropDbContext
        //   AddScoped<IJwtProvider, JwtProvider>() → provides IJwtProvider


        public AuthController(FlashDropDbContext? context, IJwtProvider? jwtProvider)
        {
            _context = context;
            this._jwtProvider = jwtProvider;

        }


        // HTTP Method: POST (creates a new resource — a user account)
        // Route: the method name "Register" doesn't matter for routing
        //        since [Route] on the class is "api/auth" and this method
        //        has no additional [Route] attribute → maps to POST /api/auth/register
        //        Wait — actually we need [HttpPost("register")] to be explicit.
        [HttpPost("register")]// this action will handle the post request at api/auth/register



        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {

            //check for the duplicate email first for clean desgin 

            // AnyAsync is more efficient than FirstOrDefaultAsync for existence checks
            // because it translates to: SELECT EXISTS(SELECT 1 FROM "Users" WHERE "Email" = @email)
            // It returns as soon as it finds ONE match — no need to load the full row.


            var EmailExist = await _context.Users.AnyAsync(u => u.Email == request.Email);


            if (EmailExist)
            {

                // Conflict() is a helper on ControllerBase that sets status 409.
                // The message is included in the response body as a plain string.
                // This gives the client a clear, actionable error message.
                return Conflict(new { message="An Account with this  eamil has  Already exist and Registered "});
            }



            //step -2. Lets Hash the password entered by the User if the Email does not exist in the database


            var passwordHash= BCrypt.Net.BCrypt.HashPassword(request.Password);



            // ── STEP 3: Create the AppUser entity ─────────────────────────
            // AppUser inherits BaseEntity → Id (Guid.NewGuid()) and CreatedAt (DateTime.UtcNow)
            // are set automatically in the BaseEntity constructor.
            // We only need to set the four domain-specific properties.

            var user = new AppUser
            {
                Name = request.Name,
                Email = request.Email,
                PasswordHash = passwordHash,

                // Role is hardcoded to "Customer" on registration.
                // Admin accounts are created via the DatabaseSeeder in future,
                // not through this public endpoint. This prevents anyone from
                // self-registering as Admin.
                Role = "Customer"

            };


            // ── STEP 4: Save to database ───────────────────────────────────
            // context.Users.Add(user) → marks the entity as Added (pending INSERT)
            // SaveChangesAsync() → executes: INSERT INTO "Users" (...) VALUES (...)
            //
            // EF Core wraps this in a transaction automatically.
            // If SaveChangesAsync throws (e.g., unique constraint from race condition),
            // ExceptionHandlingMiddleware catches DbUpdateException → HTTP 500.
            // The pre-check in Step 1 prevents this in normal flow.


            _context.Users.Add(user);
            await _context.SaveChangesAsync();




            // ── STEP 5: Return HTTP 201 Created ───────────────────────────
            // CreatedAtAction sets:
            //   Status code: 201
            //   Location header: /api/auth/register (pointing to this action)
            //   Body: anonymous object with the new user's Id
            //
            // Why return the Id? The client needs it to identify the user
            // in future requests (e.g., for an admin to look up a user by Id).
            // We do NOT return PasswordHash, Name, Email in the creation response
            // — just the minimum needed: the new resource's identifier.


            return CreatedAtAction(
                
                actionName: nameof(Register), // the name of this action method
                //routeValues: null, // no route parameters to include in the Location header
                value: new { id = user.Id, message = "Your Account created successfully." } // response body with the new user's Id
            );
        }





        //we will IMplement Post/api/Login in here 

        //Login Endpoint Flow:

        // Finds the user by email, verifies the password, generates a JWT.
        //
        // Returns:
        //   200 OK          → { token, expiresAt }
        //   401 Unauthorized → { message } — same message for wrong email AND wrong password

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {

            // 1. find user Email
            //                                                               [+]
            //  FirstOrDefaultAsync returns the user or null.
            //  ToLower() on both sides for case-insensitive comparison.
            //  We use FirstOrDefaultAsync (not SingleOrDefaultAsync) because:
            //   - The unique index on Email guarantees at most ONE result
            // - FirstOrDefaultAsync is slightly faster (stops at first match)

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());





            //2.Retuen 401 Unauthorized if user not found or password is incorrect not 404 ( it protects from Enumeration attack)
            //  SECURITY: We return the SAME message whether:
            //   a) The email doesn't exist in the database, OR
            //    b) The password is wrong
            // 
            //  This prevents "email enumeration attacks" where an attacker
            //  sends many login attempts and watches if they get different
            //  responses for emails that exist vs. don't exist.
            // 
            // "Invalid email or password." deliberately doesn't say WHICH
            //  one is wrong — the attacker learns nothing useful.

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid Email or password. " });
            }



            // 3.Verify Password

            //BCrypt.Verify compares the plain-text password the user
            //  just sent against the stored hash from the database.
            // 
            //  Internally, BCrypt extracts the salt from the stored hash
            //  (it's embedded in the hash string), re-runs the hashing
            //  with that same salt, and compares the results.
            // 
            //  BCrypt.Verify is TIMING-SAFE — it takes the same time
            //  whether the password is correct or not. This prevents
            //  "timing attacks" where an attacker measures response time
            //  to infer password characters.
            var IsPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);

            if (IsPasswordValid == false)
            {
                return Unauthorized(new { message = "Invalid Email or Password." });
            }





            //4. Genrate JWT TOken

            var token = _jwtProvider.GenerateToken(user);




            //5.Build Expireay Timestamp

            //  We compute ExpiresAt here so the client gets it explicitly.
            //  This must match the exp claim embedded in the token.
            // ExpiryMinutes comes from JwtSettings (appsettings.json: 60).
            //
            // NOTE: If your JwtSettings class exposes ExpiryMinutes,
            //  you can inject IOptions<JwtSettings> here. For simplicity
            // we hardcode the lookup — or you can expose it via the provider.
            //  The cleanest approach: have GenerateToken return a tuple or object.
            //  For this project, we compute it from configuration directly.
            var jwtSettings = HttpContext.RequestServices
           .GetRequiredService<Microsoft.Extensions.Options.IOptions<JwtSettings>>()
           .Value;
            var expiresAt = DateTime.UtcNow.AddMinutes(jwtSettings.ExpiryMinutes);



            //Last if eveything is good till here then return 200 Ok with the token

            return Ok(new LoginResponse(token, expiresAt));
        }




    }

}
