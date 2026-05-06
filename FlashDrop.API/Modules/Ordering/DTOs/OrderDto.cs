using StackExchange.Redis;

namespace FlashDrop.API.Modules.Ordering.DTOs
{

    // Creating  the OrderDto — the HTTP response shape  for all order-related endpoints.
    // This is going to be a plain class instead of a record.
    //As we are going to be using Automapper nd :
    //   AutoMapper needs a PARAMETERLESS constructor to set properties.
    //   Plain class with { get; set; } = what AutoMapper expects.
    public class OrderDto
    {


        public Guid Id { get; set; }// Client uses this for order tracking, support requests, receipts.


        //ProductId — the Guid of the product that was ordered.
        // Allows client to navigate to: GET /api/product/{ProductId}
        // to show product details alongside order history.
        public Guid ProductId { get; set; }

        //Now ProdutName:
        //THIS FIELD IS NOT ON THE ORDER ENTITY.
        // Order entity only has Guid ProductId.
        // ProductName must be populated by the HANDLER — NOT by AutoMapper.
        //How will AUtomappper deal with it as it will convert the Order entity to OrderDto?
        //so AUtomapper works same way for all the coloumn now for this coloumn it cant auto map to anything
        //so we will sdd ignore() to it and it will leave it to handle by someone else in this case we will do it 
        //by out handler 

        public string ProductName { get; set; } = string.Empty;


        public int Quantity { get; set; }//Quantity — how many units were purchased.
                                         // Shown in order history: "2x Air Jordan 1 @ $299.99 each"



       // TotalPrice — the purchase-time price snapshot.
    // Calculated as Product.Price × Quantity at checkout time.
    // Stored in Order.TotalPrice — AutoMapper maps this directly.
    // decimal type: exact base-10 monetary arithmetic.
        public decimal TotalPrice { get; set; }


        public string Status { get; set; } = string.Empty;  
        public DateTime CreatedAt { get; set; }


        // // INTENTIONALLY OMITTED:
        // UserId — the client already knows their own identity
        //          (it's in their JWT token). Returning it in every order
        //          response adds noise without value.
    }
}
