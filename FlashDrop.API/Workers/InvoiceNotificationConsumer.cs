using FlashDrop.API.Modules.Ordering.Events; // OrderConfirmedEvent  
using FlashDrop.API.Shared.Services;
using Microsoft.Extensions.Configuration; //Iconfiguration
using Microsoft.Extensions.Hosting; // Backeground Service
using Microsoft.Extensions.Logging; //For ILloger;
using RabbitMQ.Client; // ConnectionFactory, IConnection, IModel
using RabbitMQ.Client.Events; //EventingBasicConsumer, BasicDeliverEventArgs 
using Serilog;
using System.Text;//Encoding UTF 
using System.Text.Json; //Json serilisaer




//InvoiceNotificationConsumer inherits BackgroundService.
// BackgroundService is a .NET abstraction for long-running background tasks.( there are 2 other alternative of this kind of work iHostedSerrve and hanfire
// When registered with AddHostedService<T>():( as far as i know if you choose i Hosted serviec then you have to implement startAsync and stop async
//but in this they come preIMplemented we just have to implement ExecuteAYsn
//   - Created at app startup
//   - ExecuteAsync() called and runs in a background thread
//   - Kept alive for the entire application lifetime
//   - StopAsync() called on app shutdown → stoppingToken is cancelled

namespace FlashDrop.API.Workers
{

    //// IDisposable: explicitly close RabbitMQ resources on shutdown.
    public class InvoiceNotificationConsumer : BackgroundService, IDisposable
    {
        // Queue name — MUST match exactly what RabbitMqEventBus uses.
        // Both publisher and consumer use "order-confirmed-queue".
        // If they differ, messages pile up undelivered or consumer reads empty queue.
        private const string QueueName = "order-confirmed-queue";

        private readonly IConfiguration _configuration;

        private readonly ILogger<InvoiceNotificationConsumer> _logger;

        //RabbitMQ connection and channel — created in ExecuteAsync, held for lifetime.
        private IConnection? _connection;
        private IModel? _channel;



        public InvoiceNotificationConsumer(IConfiguration configuration, ILogger<InvoiceNotificationConsumer> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }


        //ExecuteAsync — the main loop.Runs in background until stoppingToken fires.
        //
        // Structure:
        //   1. Retry loop: connect to RabbitMQ (with patience for Docker startup)
        //   2. Declare queue (idempotent — safe to call every startup)
        //   3. Set up consumer callback (Prompt 41 fills in the handler body)
        //   4. await Task.Delay(Timeout.Infinite) — stay alive, wait for messages


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            //Step 1: Retry COnncetion to RabbitMQ
            //:
            // WHY retry here (not in constructor like RabbitMqEventBus)?
            //   BackgroundService.ExecuteAsync runs asynchronously in a background thread.
            //   We can use Task.Delay (async, non-blocking) here.
            //   RabbitMqEventBus uses Thread.Sleep (synchronous) in its constructor because
            //   DI container build is synchronous — cannot use async there.
            //   Task.Delay here is better: doesn't block any threads while waiting.


            //importing all the credential from configuration 
            var host = _configuration["RabbitMQ:Host"] ?? "localhost";
            var username = _configuration["RabbitMQ:Username"] ?? "guest";
            var password = _configuration["RabbitMQ:Password"] ?? "guest";


            // How many attempts and delay?
            //   10 attempts × 5-second delay = up to 50 seconds of patience.
            //   RabbitMQ typically starts in 10–20 seconds.
            //   Generous buffer ensures this works even on slow dev machines.

            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = username,
                Password = password,
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                DispatchConsumersAsync = false //// using synchronous EventingBasicConsumer 

                /* 
                 Why EventingBasicConsumer (not AsyncEventingBasicConsumer)?
RabbitMQ.Client 6.x has two consumer types:

EventingBasicConsumer — synchronous callback, simpler
AsyncEventingBasicConsumer — async callback, requires DispatchConsumersAsync = true on the factory

We use EventingBasicConsumer for simplicity. The callback is Received += (model, ea) => { ... }. 
Inside the callback, we can still do synchronous work (deserialization, logging) without needing async.
                Since we're just logging (not doing async DB calls), the synchronous callback is correct and simpler.
*/
            };


            const int maxAttempts = 10;
            const int delaySeconds = 5;
            bool connected = false;

            for (int attempt = 1; attempt < maxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
            {

                try
                {

                    _logger.LogInformation(
                   "InvoiceNotificationConsumer: RabbitMQ connection attempt {Attempt}/{Max}...",
                   attempt, maxAttempts);


                    _connection = factory.CreateConnection();
                    _channel = _connection.CreateModel();


                    //  Declare the queue — idempotent.
                    // Must match the declaration in RabbitMqEventBus.PublishAsync (Prompt 29):
                    //   durable: true, exclusive: false, autoDelete: false
                    // If settings differ from the existing queue → RabbitMQ throws a precondition error.


                    _channel.QueueDeclare(
                          queue: QueueName,//   queue:      "order-confirmed-queue" — the queue name
                        durable: true,  //   durable:    true — queue survives RabbitMQ restart
                    //               (stored to disk, not lost on broker crash)
                        exclusive: false,//   exclusive:  false — other connections can also use this queue
                    //               (exclusive=true would auto-delete when connection closes)
                        autoDelete: false, //   autoDelete: false — queue stays even when no consumers are connected
                    //               (messages accumulate until a consumer reconnects)
                        arguments: null//   arguments:  null — no extra AMQP arguments (TTL, DLX, etc.)

                        ); //we have done same kind of operation in RabitMqEvenBus pls look at it for you rerrence 





                    //Step 4:
                    //BasicQos: prefetch 1 message at a time.
                    // This tells RabbitMQ: "only send me one message at a time.
                    // Wait for me to ack before sending the next."
                    // prefetchCount=1 is conservative and correct for our use case.
                    // Without this: RabbitMQ could flood the consumer with all queued messages at once.

                    _channel.BasicQos(


                        prefetchSize: 0,//0 = no size limit
                         prefetchCount: 1,//one message at a time
                         global: false // per consumer not per connection

                        );

                    connected = true;// the main funct

                    _logger.LogInformation(
                  "InvoiceNotificationConsumer: Connected to RabbitMQ. " +      
                  "Listening on queue '{Queue}'.", QueueName);


                    break; // break the retry loop when the connnection is established 

                }

                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {

                    // we will start this function by loggin the error in the warning
                    _logger.LogWarning(
                    "InvoiceNotificationConsumer: Connection attempt {Attempt}/{Max} failed: {Message}. " +
                    "Retrying in {Delay}s...",
                    attempt, maxAttempts, ex.Message, delaySeconds);


                    if (attempt < maxAttempts)
                    {

                        //in this case go for Task delay as it is non blocking approach not thread.sleep 
                        //and pass on the stopping  token , if shutdown in bw token will delay the cancelation 

                        //in Otehr words Task.Delay (not Thread.Sleep) — async, non-blocking.
                        // Passes stoppingToken: if app shuts down during wait, delay cancels immediately.
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
                    }




                }//Catch Block end here



            }
            //Retry loop ends here

            if (!connected)
            {
                _logger.LogError(                                       
                "InvoiceNotificationConsumer: Failed to connect to RabbitMQ after {Max} attempts. " +
                "Invoice notifications will not be processed until the service restarts.",
                maxAttempts);                                         
                return; // Exit ExecuteAsync — no consumer will run
            }




            //Now here we are sure that we are conncted ot RabbitMq

            //Step 2 : SetUp the message Consuemer

            //EventingBasicConsumer: event-driven consumer.
            // Subscribes to the 'Received' event — called whenever RabbitMQ delivers a message.

            // The callback runs synchronously within the RabbitMQ.Client consumer thread.
            // Deserialization and logging are fast synchronous operations — appropriate here.
            // If we needed async operations (e.g., DB writes), we'd use AsyncEventingBasicConsume

            var consumer = new EventingBasicConsumer(_channel!);

            consumer.Received += (model, ea) =>
            {
                //STEP 2a: Extract the raw message bytes.

                var body = ea.Body.ToArray();//ea.Body.ToArray() converts ReadOnlyMemory<byte> to byte[].


                //Step 2B:
                // The publisher (RabbitMqEventBus) serialised as UTF-8 JSON.
                // We decode with the same encoding.

                var json = Encoding.UTF8.GetString(body);

                try
                {

                    //STEP 2c: Deserialise JSON → OrderConfirmedEvent.


                   
                    // The record properties match the JSON keys from System.Text.Json's default
                    // camelCase output: orderId, userId, productName, quantity, totalPrice, confirmedAt

                    var orderEvent = JsonSerializer.Deserialize<OrderConfirmedEvent>(
                        json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); //PropertyNameCaseInsensitive: true handles camelCase JSON from publisher.

                    //validation Order Event 
                    if (orderEvent is null)
                    {
                        _logger.LogWarning(                                 
                            "InvoiceNotificationConsumer: Received null event from queue. Raw: {Json}", json);
                    }

                    else
                    {

                        // STEP 2d: Log the structured invoice message.




                        //  // In a real system, this is where you would:
                        //   - Call a PDF generation service to create an invoice
                        //   - Send a confirmation email via SendGrid/Mailchimp
                        //   - Update a notification service
                        // For this project, we simulate all of those with a structured log.
                        _logger.LogInformation(
                            "Invoice generated for Order {OrderId}, User {UserId}, " +
                            "Product {ProductName}, Qty {Quantity}, Total {TotalPrice}",
                             orderEvent.OrderId,
                             orderEvent.UserId,
                             orderEvent.ProductName,
                             orderEvent.Quantity,
                             orderEvent.TotalPrice

                            );
                    }


                    //STEP 2e: Acknowledge the message.

                    // BasicAck tells RabbitMQ: "I processed this message successfully.
                    // You can permanently remove it from the queue."
                    //
                    // deliveryTag: unique identifier for this delivery
                    // multiple: false — ack this message only (not all earlier unacknowledged messages)
                    //
                    // WHY ack AFTER logging (not before)?
                    //   If we acked first and then crashed, the log would never be written
                    //   and the message would be gone — silent data loss.
                    //   Ack after successful processing = at-least-once delivery guarantee.
                    //   If we crash after logging but before acking: message is requeued.
                    //   Next time we process it, we log again (duplicate log — harmless).

                    _channel!.BasicAck(
                        deliveryTag: ea.DeliveryTag,//  // deliveryTag: unique identifier for this delivery
                        multiple: false //// multiple: false — ack this message only (not all earlier unacknowledged messages)

                        );


                }

                catch (Exception ex)
                {


                    //If processing fails (e.g., corrupt JSON), log the error
                    //in Production or bigger applcication this is where we send DeadLetterQueue
                    _logger.LogError(ex,                                  
                   "InvoiceNotificationConsumer: Failed to process message. Raw: {Json}", json);

                    // If processing fails(e.g., corrupt JSON), log the error.
                    // BasicNack with requeue=false: reject and discard the message.

                    _channel!.BasicNack(
                        deliveryTag: ea.DeliveryTag,
                        multiple: false,
                        requeue: false

                        );

                    


                }
            };


            //Step -3:Try Consuming from Queue
            //we will use basicCOnsume method for this 

            _channel.BasicConsume(
                queue: QueueName,//information about which queue to consume 
                // Manual ack
                autoAck: false,// autoAck: false — we manually ack after processing (at-least-once delivery).
                               /// If autoAck were true: RabbitMQ removes message immediately on delivery,before we process it. If we crash mid-processing: message is lost.
                consumer: consumer

                );



          //  STEP 4: Wait indefinitely.


        // Task.Delay(Timeout.Infinite, stoppingToken) keeps ExecuteAsync alive.
        // When the app shuts down, stoppingToken is cancelled:
        //   → Task.Delay throws TaskCanceledException
        //   → ExecuteAsync exits cleanly
        //   → BackgroundService framework calls StopAsync → Dispose

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }

            catch(OperationCanceledException)
            {

                // Expected on app shutdown — not an error. Log for clarity.
                _logger.LogInformation(                                    
                    "InvoiceNotificationConsumer: Shutdown signal received. Stopping consumer.");

            }




        }




        public override void Dispose()
        {

            try
            {
                if (_channel is { IsOpen: true })
                    _channel.Close();

                if (_connection is { IsOpen: true })
                    _connection.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "InvoiceNotificationConsumer: Exception during RabbitMQ disposal.");
            }
            finally
            {
                _channel?.Dispose();
                _connection?.Dispose();
                base.Dispose();
            }
        }


    }
}
