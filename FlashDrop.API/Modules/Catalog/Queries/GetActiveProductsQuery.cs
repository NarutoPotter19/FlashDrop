using FlashDrop.API.Modules.Catalog.DTOs;//as we are goint to use Product DTOs
using FlashDrop.API.Shared;//we are goint to use our pagination wrapper PagedResponse<T>
using MediatR;//as we are goint to use MediatR for CQRS pattern
using FluentValidation;// for AbstractValidator<GetActiveProductsQuery> to validate the query parameters



namespace FlashDrop.API.Modules.Catalog.Queries
{


    //The Query — requests a specific page of active products.
    //
    // IRequest<PagedResponse<ProductDto>>:
    //   The response is a full PagedResponse wrapper, NOT a plain list.
    //   This carries both the data AND pagination metadata (TotalCount, TotalPages, etc.)
    public record GetActiveProductsQuery(
        int PageNumber,
        int PageSize
    ) : IRequest<PagedResponse<ProductDto>>;


    //Validator — unlike GetProductByIdQuery (no validator needed),
    // this query accepts user-controlled integers that need bounds checking.
    //
    // WHY validate a query (read operation)?
    //   PageNumber=-1 or PageSize=999999 are not "not found" issues —
    //   they are invalid input values. Validation is the right tool.
    //   The ValidationBehavior pipeline handles this automatically.
    public class GetActiveProductsQueryValidtator : AbstractValidator<GetActiveProductsQuery>
    {


        
        public GetActiveProductsQueryValidtator()
        {

            //1. Rule 
            //PageNumber must be at least 1.
            // Page 0 is meaningless (Skip = -10). Page -5 is nonsensical.
            RuleFor(q => q.PageNumber)
                .GreaterThan(0)
                .WithMessage("PageNumber must be greater than 0.");


            //2. Rule
            //PageSize must be positive AND capped.
           // Without cap: ?pageSize = 999999 → EF loads 999,999 rows → memory exhaustion → crash
        //   With cap: worst case is 50 rows per request — always bounded and safe
            RuleFor(q => q.PageSize)
                .GreaterThan(0).WithMessage("PageSize must be greater than 0.")
                .LessThanOrEqualTo(50).WithMessage("PageSize cannot exceed 50.");
        }
    }
}
