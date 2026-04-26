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

        private readonly IJwtProvider? jwtProvider;


        //Constructor injection:
        // ASP.NET Core DI automatically provides both dependencies
        // because we registered them in Program.cs:
        //   AddDbContext<FlashDropDbContext>() → provides FlashDropDbContext
        //   AddScoped<IJwtProvider, JwtProvider>() → provides IJwtProvider


        public AuthController(FlashDropDbContext? context, IJwtProvider? jwtProvider)
        {
            _context = context;
            this.jwtProvider = jwtProvider;

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
                value: new { id = user.Id, message = "Account created successfully." } // response body with the new user's Id
            );
        }





        //we will IMplement Post/api/Login in here 


    }

}
