using FlashDrop.API.Modules.Catalog.Commands;//for CreateProductCommand and its handler
using FlashDrop.API.Modules.Catalog.Queries;// GetProductByIdQuery, GetActiveProductsQuery
using MediatR;                                  // ISender
using Microsoft.AspNetCore.Authorization;       // [Authorize]
using Microsoft.AspNetCore.Mvc;                 // ControllerBase, ApiController, Route, etc.

namespace FlashDrop.API.Modules.Catalog
{

    //[ApiController] enables:
    //   1. Automatic model validation → invalid request body returns 400 before action runs
    //   2. [FromBody] inference → no need to write [FromBody] on action parameters
    //   3. Problem Details responses for validation errors (works with our middleware)
    [ApiController]
    [Route("api/[controller]")]
    public class ProductController : ControllerBase
    {

        //Inject ISender(MediatR) — NOT IMediator.
        //
        // ISender only exposes Send(IRequest) — for Commands and Queries.
        // IMediator also exposes Publish(INotification) — for events.
        // We only need Send() here → inject the narrowest interface.
        // This is Interface Segregation Principle (ISP) in practice.

        private readonly ISender _sender;


        public ProductController(ISender sender)
        {
            _sender = sender;
        }



        // ── ENDPOINT 1: GET /api/product/{id} 



        // Retrieve a single product by its GUID.
        // Public endpoint — no [Authorize] attribute — anyone can view products.
        //
        // Cache-Aside: this endpoint benefits from Redis caching (Prompt 21).
        // First call: ~50ms (PostgreSQL query + Redis write).
        // Subsequent calls: ~0.1ms (Redis read only, no DB touch).
        //
        // Returns:
        //   200 OK   → { ProductDto } — product found
        //   404 Not Found → NotFoundException thrown in handler → middleware


        [HttpGet("{id:guid}")]
        public async Task <IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {


            var result = await _sender.Send(new GetProductByIdQuery(id), cancellationToken);


            return Ok(result);
        }





        // ── ENDPOINT 2: GET /api/product

        //Retrieve a paginated list of active products.
        // Public endpoint — no auth required for browsing the catalog.
        [HttpGet]
        public async Task<IActionResult> GetByAll([FromQuery] int pageNumber = 1,[FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
        {

            // Query string parameters (all optional — defaults are applied):
            //   ?pageNumber=1  → which page (default: 1)
            //   ?pageSize=10   → items per page (default: 10, max: 50 via validator)
           // [FromQuery] reads values from the URL query string.
        // Default values (= 1, = 10) apply when params are omitted from the URL.
        // Swagger automatically displays these defaults in the UI.


            var result= await _sender.Send(new GetActiveProductsQuery(pageNumber,pageSize),cancellationToken);


            return Ok(result);

        }



        // ── ENDPOINT 3: POST /api/product 

        //Create a new product.
        // PROTECTED — requires a valid JWT with Role = "Admin".

        // // [Authorize(Roles = "Admin")] enforces:
        //   - A valid JWT must be in the Authorization: Bearer <token> header
        //   - The JWT must contain a "role" claim with value "Admin"
        //   - Customers get HTTP 403 Forbidden (authenticated but wrong role)
        //   - Unauthenticated requests get HTTP 401 Unauthorized (no/invalid token)\


        //IMP:
        // Returns:
        //   201 Created → { ProductDto } with Location header pointing to GET /{id}
        //   400 Bad Request → FluentValidation failed (name empty, price <= 0, etc.)
        //   401 Unauthorized → no JWT or invalid JWT
        //   403 Forbidden → valid JWT but Role != "Admin"

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] CreateProductCommand command,CancellationToken cancellationToken)
        {

            // [FromBody] reads the request body as JSON.
            // ASP.NET Core deserializes it into CreateProductCommand automatically.
            // [ApiController] actually makes [FromBody] implicit, but we include it
            // explicitly for clarity — it documents the expected source of data.

            var result = await _sender.Send(command, cancellationToken);


            // // Dispatch the command. ValidationBehavior runs the validator FIRST.
            // If validation fails → ValidationException → 400 (handler never runs).
            // If validation passes → handler creates product → returns ProductDto.

            return CreatedAtAction(
           actionName: nameof(GetById),
           routeValues: new { id = result.Id },
           value: result);


        // HTTP 201 Created with Location header.
        //CreatedAtAction sets HTTP 201 AND adds a Location header:
        //Location: / api / product / 3fa85f64 - 5717 - 4562 - b3fc - 2c963f66afa6
            // CreatedAtAction sets:
            //   Status Code: 201
            //   Location header: /api/product/{result.Id}
            //   Response body: the ProductDto JSON
            //
            // actionName: nameof(GetById) — points to the [HttpGet("{id:guid}")] action above.
            //   This is how ASP.NET Core builds the Location URL: it calls GetById with the new Id.
            //
            // routeValues: new { id = result.Id } — provides the {id} route template value.
            //   The generated Location header becomes: /api/product/3fa85f64-...
            //
            // value: result — the ProductDto serialized as the response body.

        }

    }
}
