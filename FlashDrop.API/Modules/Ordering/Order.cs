using FlashDrop.API.Shared;

namespace FlashDrop.API.Modules.Ordering
{

    // Order entity represents a customer's purchase record.

    public class Order : BaseEntity
    {

       
    // UserId: Stores the Guid primary key of the AppUser who checked out.
    //
    // WHY plain Guid instead of navigation property (public AppUser User)?
    //   Navigation properties require either:
    //     a) Lazy loading (hidden N+1 query risk)
    //     b) Explicit .Include(o => o.User) in every query
    //   For this CQRS project, we query exactly what we need in each handler.
    //   If we need user info alongside order info, we write a JOIN query explicitly.
    //   This makes query behavior transparent — no "magic" loading.
        public Guid UserId { get; set; }// UserId — who placed this order.

    // Stores the Guid primary key of the Product.
    // Same reasoning as UserId — plain FK, no navigation property.
        public Guid ProductId { get; set; }// ProductId — what was ordered.

        public int Quantity { get; set; }// Quantity — how many units were ordered.
                                         //we will validate the quantity or apply some rules on it int handler function later 





        // ToatlPrice: Calculated as: Product.Price × Quantity
        // This snapshot is CRITICAL for order history integrity:
        //
        // Without snapshot: If admin raises Air Jordan price from $299 to $499,
        //   all historical orders recalculate TotalPrice = 499 × Quantity.
        //   Customer's receipt suddenly shows wrong amount. Legally problematic.
        //
        // With snapshot: TotalPrice is locked at purchase-time price.
        //   Historical orders always show the price the customer actually paid.
        //
        // decimal type: exact base-10 arithmetic for monetary calculations.
        // Configured as decimal(18,2) in OnModelCreating.
        public decimal TotalPrice { get; set; }//TotalPrice — price snapshot at the moment of purchase.

        public OrderStatus Status { get; set; } = OrderStatus.Pending;//// Default = Pending: every new order starts in Pending state.


    }
}
