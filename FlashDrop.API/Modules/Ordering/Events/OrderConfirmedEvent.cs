

using System.Xml.Linq;

namespace FlashDrop.API.Modules.Ordering.Events
{

    //Create the event record that flows from
//               CheckoutCommandHandler (publisher) through RabbitMQ
//               to InvoiceNotificationConsumer (subscriber).
    public record OrderConfirmedEvent
   (

        //OrderId: identifies which order was confirmed.
        // The consumer logs this for tracing. Future payment service would use
        // this to update order status or trigger fulfilment.
        Guid OrderId,


        //serId — identifies which customer placed the order.
        // Future email service would use this to fetch the customer's email address
        // and send the order confirmation email.
        Guid UserId,

        // ProductName — the human-readable product name.
    // DENORMALISED into the event — the consumer does NOT query the DB for this.
    //
    // WHY denormalise?
    //   1. Background service stays stateless (no FlashDropDbContext dependency)
    //   2. Zero extra DB queries per consumed message
    //   3. Historical accuracy: if product name changes post-order,
    //      invoice still shows the correct name from purchase time
    //   4. Event sourcing best practice: events are self-contained facts
        string ProductName,




        // Quantity — how many units were ordered.
        int Quantity,


         //TotalPrice — the final price paid.
        // This is the snapshot price (Price × Quantity) stored in Order.TotalPrice.
        decimal ToatlPrice ,

        DateTime ConfirmedAt

        );
}
