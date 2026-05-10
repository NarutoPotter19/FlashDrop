
using MediatR;//IRequestHandler<,>

using System.Security.Claims;// // ClaimTypes.NameIdentifier
using Microsoft.EntityFrameworkCore;// // AsNoTracking, ToListAsync
using Microsoft.AspNetCore.Http;// // IHttpContextAccessor
using FlashDrop.API.Modules.Ordering.DTOs;           // OrderDto
using FlashDrop.API.Shared.Data;                     // FlashDropDbContext
using FlashDrop.API.Shared.Exceptions;               // NotFoundException (for bad userId parse)

//GetMyOrdersQueryHandler fetches orders for the
// authenticated user using a single JOIN query.

namespace FlashDrop.API.Modules.Ordering.Queries
{
    public class GetMyOrdersQueryHandler : IRequestHandler<GetMyOrdersQuery, List<OrderDto>>
    {


        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly FlashDropDbContext _dbcontext;

        // WHY no IMapper?
        //   We project directly into OrderDto inside the LINQ JOIN query.
        //   AutoMapper is NOT used here because the JOIN query builds the DTO
        //   inline — this is more efficient (one query, all fields populated)
        //   and avoids the ProductName.Ignore() workaround entirely.


        //only 2 dependcies we are goin to use in here 

        public GetMyOrdersQueryHandler(IHttpContextAccessor httpContextAccessor, FlashDropDbContext DbContext) {

            //
            // IHttpContextAccessor → Singleton:
            //   Provides access to the current HTTP request's User claims.
            //   Registered via: builder.Services.AddHttpContextAccessor()
            //   Required because MediatR handlers are NOT controllers —
            //   they don't have direct access to HttpContext without this service.

            _httpContextAccessor = httpContextAccessor;


            // FlashDropDbContext → Scoped:
            //   Used for the JOIN query against Orders + Products tables.
            _dbcontext = DbContext;
        
        }


        public async Task <List<OrderDto>> Handle(GetMyOrdersQuery query,CancellationToken cancellationToken)
        {
            // ── STEP 1: Extract UserId from JWT claims ─

            //// The JWT middleware ( populates HttpContext.User
            // with claims when a valid Bearer token is present.
            // ClaimTypes.NameIdentifier maps to the 'sub' claim in our JWT.
            var userIdString =  _httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;


            // Defensive check: if somehow the claim is missing or invalid,
            // return an empty list rather than crash. In practice this should
            // never happen if [Authorize] is on the endpoint — the JWT middleware
            // would have rejected the request before reaching here.
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return new List<OrderDto>();
            }




            // ── STEP 2: JOIN query — single round-trip to PostgreSQL ─
            //why? so if not this earlier what could have been done is that we might have asked order table for list of order for particular user id 
            //lets say ther are n orders for that user ID . now to return the reponse we must have productName as mentioned in dto but order table only has productId.
            // in this case we have to again query for each product id and fetch the name . now total no of query become (1+ n)
            //1->to fetch the orders
            //N-> to find product name of each product ( which are one) this is famoue N+1 problem.

            //and we solve it suing join so we will perform join operation on Order table and prodcut table and from the koin result we can fill all the 
            //dto memebers even the product name too as join will give use the result containing all the data 



            var orders = await _dbcontext.Orders
                .Where(o => o.UserId == userId)
                .AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Join(
                _dbcontext.Products,
                order => order.ProductId,
                product => product.Id,
                (order, product) => new OrderDto
                {
                    Id = order.Id,
                    ProductId = order.ProductId,
                    ProductName = product.Name,      // ← filled by JOIN — the key benefit
                    Quantity = order.Quantity,
                    TotalPrice = order.TotalPrice,
                    Status = order.Status.ToString(),  // enum → string (same as MappingProfile)
                    CreatedAt = order.CreatedAt

                })
                .ToListAsync(cancellationToken);




            // ── STEP 3: Return results ─
            // Returns an empty list [] if the user has no orders.

            return orders;



        }
    }
}
