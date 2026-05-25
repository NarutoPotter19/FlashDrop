using FlashDrop.API.Modules.Catalog.DTOs;
using FluentValidation;
using MediatR;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace FlashDrop.API.Modules.Catalog.Queries
{
    //======>Query record — takes TopN to limit the results.
    // Returns a list of the top-N best-selling products by confirmed order quantity.


    public record GetTopSellingProductsQuery(int TopN) : IRequest<List<TopSellingProductDto>>;



    public class GetTopSellingProductsQueryValidator : AbstractValidator<GetTopSellingProductsQuery>
    {

        public GetTopSellingProductsQueryValidator() { 
        
            RuleFor(x=>x.TopN)
                 .GreaterThan(0)
            .WithMessage("TopN must be greater than zero.")// // LessThanOrEqualTo(50): prevents loading massive result sets.
            // Admin analytics rarely needs more than 50 products.
            .LessThanOrEqualTo(50)
            .WithMessage("TopN cannot exceed 50.");


        }

    }





}
