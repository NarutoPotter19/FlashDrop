using FlashDrop.API.Modules.Catalog.DTOs;   // ProductDto
using MediatR;                              // IRequest<T>

namespace FlashDrop.API.Modules.Catalog.Queries
{

    //Define the Query — the message that asks:
    //               "Give me the product with this ID."



    // GetProductByIdQuery is a record carrying just the Id.
    //
    // It implements IRequest<ProductDto> — telling MediatR:
    //   "When you handle this query, return a ProductDto."
    //
    // WHY NO VALIDATOR for this query?
    //   A Guid is always syntactically valid from the route parameter.
    //   If the Guid doesn't match any product in the database, we return
    //   HTTP 404 — that's handled in the handler with NotFoundException,
    //   not a validation error. Validation is for business rule violations
    //   on input data (like Price > 0). "Is this ID known?" is a not-found
    //   concern, not a validation concern.
    public record GetProductByIdQuery(Guid Id) : IRequest<ProductDto>;
}
