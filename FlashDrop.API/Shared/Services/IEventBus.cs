namespace FlashDrop.API.Shared.Services
{

    //IEvent bus is Massage Contract or Interface
    //it has one methode PublishAysnc

    //WHY generic<T>?
    //   The same interface handles any event type:
    //     await _eventBus.PublishAsync(new OrderConfirmedEvent(...));
    //     await _eventBus.PublishAsync(new StockAlertEvent(...));      ← future
    //     await _eventBus.PublishAsync(new UserRegisteredEvent(...));   ← future
    //   One interface, unlimited event types.


    // Publish an event message to the message broker.
    // T = any serializable C# type (records, classes).
    // The message is serialized to JSON by the implementation.
    // Fire-and-forget contract: the caller does not wait for processing.
    public interface IEventBus
    {
        // WHY Task (not void)?

        //   Publishing is an I/O operation (network call to RabbitMQ).
        //   async Task lets callers await completion and handle exceptions.
        //   Even though RabbitMQ.Client 6.x publish is synchronous internally,
        //   we return Task.CompletedTask — the interface stays async-compatible
        //   for implementations that might be genuinely async (Azure Service Bus, etc.)
        //
        // Registered in DI as: AddSingleton<IEventBus, RabbitMqEventBus>()
        // Used by: CheckoutCommandHandler in future i m Going to implement it 
        // Consumed by: InvoiceNotificationConsumer in future as i will create it  via RabbitMQ queue
        Task PublishAsync<T>(T message);
    }
}
