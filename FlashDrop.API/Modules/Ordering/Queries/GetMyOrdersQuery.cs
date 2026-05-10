
using FlashDrop.API.Modules.Ordering.DTOs;
using MediatR;



// Define the query that retrieves the current
//               user's order history. Zero parameters —
//               UserId is read from JWT claims in the handler.
namespace FlashDrop.API.Modules.Ordering.Queries
{


    //GetMyOrdersQuery — empty record because the only
    // filter (UserId) comes from the authenticated user's JWT token,
    // not from client input.


    public record GetMyOrdersQuery(
   //// IRequest<List<OrderDto>>: the handler returns a list of all
   // orders belonging to the authenticated user.


   //Now instead of returing OrderDTOs i could also Have returened PagedResponseDTOs
   //but that is suitable for higher no of records as to apply pagination for lower no of 
   //Records it will add unnecessary complaxity.) : IRequest<List<OrderDto>>




   //Now with other Queries and Commnad we usuly add fluent validation rules as well but in here we r not doing so 
   //as there is no user/Client input in this query and we are using userId whihc we are getting from 
   // JWT claimes Nothing can be "invalid" here — the query always either returns
   //   a list (possibly empty) or throws if authentication fails.
   ) : IRequest<List<OrderDto>>;
}
