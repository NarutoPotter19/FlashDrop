

using FlashDrop.API.Shared.Services;
using Microsoft.Extensions.Configuration;   // IConfiguration
using Microsoft.Extensions.Logging;         // ILogger<T>
using RabbitMQ.Client;                       // ConnectionFactory, IConnection, IModel
using System;
using System.Text;                           // Encoding.UTF8
using System.Text.Json;                      // JsonSerializer


//RabbitMqEventBus implements IEventBus using the official
// RabbitMQ.Client library in this we are using version 6.0 due to compatibility


//  Singleton — holds one persistent AMQP connection + channel.
// IDisposable: properly cleans up RabbitMQ resources on app shutdown.
namespace FlashDrop.API.Shared.Services
{
    public class RabbitMqEventBus : IEventBus, IDisposable
    {


       // The AMQP connection — a persistent TCP connection to RabbitMQ.
    // Created once in constructor, reused for all publishes.
        private IConnection? _connection;


        //The channel — a multiplexed virtual connection over _connection.
        // IModel is the RabbitMQ.Client term for a channel.
        // Used for: declaring queues, publishing messages.
        private IModel? _channel;


        //The queue name — consistent across publisher (this class) and consumer 
     // Both MUST use the exact same string. Define as a constant to avoid typos.
        private const string QueueName = "order-confirmed-queue";
        private readonly ILogger<RabbitMqEventBus> _logger;


        //Constructor — resolves RabbitMQ settings from appsettings.json
        // and attempts to connect with retry logic.
        //
        // Dependencies:
        //   IConfiguration → reads RabbitMQ:Host, Username, Password from appsettings.json

        public RabbitMqEventBus(ILogger<RabbitMqEventBus> logger, IConfiguration configuration)
        {
            _logger = logger;

            //Read RabbitMQ settings from appsettings.json:
            //   "RabbitMQ": { "Host": "localhost", "Username": "guest", "Password": "guest" }
            //IN prodcution which we will do it in future  These values are overridden in docker-compose.yml via environment variables

            var host = configuration["RabbitMq:Host"] ?? "localhost";
            var username = configuration["RabbitMq:Username"] ?? "guest";
            var password = configuration["RabbitMq:Password"] ?? "guest";


            //ConnectionFactory — the RabbitMQ connection settings builder.
            var factory = new ConnectionFactory
            {
                HostName = host,
                UserName = username,
                Password = password,
                DispatchConsumersAsync = false,
                RequestedHeartbeat = TimeSpan.FromSeconds(60)// RequestedHeartbeat — how often to send heartbeat frames.
                                                             // Detects dead connections (network partition, remote crash) faster.
            };

            const int maxAttempts = 3;
            const int delaySeconds = 2;


            // // ── RETRY LOOP — 

            //RabbitMQ takes 10–20 seconds to start after Docker Compose launches.
            // The .NET app starts in 1–3 seconds. Without retry, the first connection attempt
            // fires when RabbitMQ is still initialising → "Connection refused" → crash.
            //
            // This retry loop gives RabbitMQ time to become ready.
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {

                    // Attempt to establish the AMQP TCP connection.
                    // it will Throws BrokerUnreachableException or SocketException if RabbitMQ isn't ready.
                    _connection = factory.CreateConnection();


                    // Create a channel (virtual connection) over the TCP connection.
                    // Channels are the unit of work in AMQP — you declare, publish, and consume via channels.
                    _channel = _connection.CreateModel();




                    //Declare the queue.
                    // QueueDeclare is IDEMPOTENT — safe to call on every startup.
                    // If the queue already exists with the same settings → no-op.
                    // If it doesn't exist → create it.
                   
                    _channel.QueueDeclare(
                        queue: QueueName,//   queue:      "order-confirmed-queue" — the queue name
                        durable: true,  //   durable:    true — queue survives RabbitMQ restart
                    //               (stored to disk, not lost on broker crash)
                        exclusive: false,//   exclusive:  false — other connections can also use this queue
                    //               (exclusive=true would auto-delete when connection closes)
                        autoDelete: false, //   autoDelete: false — queue stays even when no consumers are connected
                    //               (messages accumulate until a consumer reconnects)
                        arguments: null); //   arguments:  null — no extra AMQP arguments (TTL, DLX, etc.)





                    _logger.LogInformation(
                        "RabbitMQ connected successfully on attempt {Attempt}. Queue '{Queue}' ready.",
                        attempt, QueueName);


                    //If no Exception has been thrown till now then it means that our connecttion is successful and we can exit the loop

                    break; // Exit loop on success
                }
                catch (Exception ex)
                {
                    if (attempt < maxAttempts)
                    {
                        //Loggin the failure as warning 
                        _logger.LogWarning(
                            "RabbitMQ connection attempt {Attempt}/{MaxAttempts} failed: {Message}. " +
                            "Retrying in {Delay}s...",
                            attempt, maxAttempts, ex.Message, delaySeconds);


                       
                        //Synchronous sleep — this is in the constructor which runs
                        // during DI container build (synchronous context). Thread.Sleep is correct here.

                        Thread.Sleep(TimeSpan.FromSeconds(delaySeconds));
                    }
                    else
                    {
                       
                        // Final attempt failed. Log as Error and rethrow.
                        // The app will fail to start — which is the correct behaviour.
                        // Without RabbitMQ, the app cannot process orders or generate invoices.
                        // Better to fail fast and visibly than to start silently broken.
                        _logger.LogError(
                            ex,
                            "RabbitMQ connection failed after {MaxAttempts} attempts. " +
                            "Ensure RabbitMQ is running: docker compose up -d",
                            maxAttempts);
                        throw;
                    }
                }
            }
        }

        public Task PublishAsync<T>(T message)//serialize and publish an event to RabbitMQ.
        {
            var json = JsonSerializer.Serialize(message);//Serialize the event object to a JSON string.
                                                         // System.Text.Json serializes all public properties by default.
            var body = Encoding.UTF8.GetBytes(json); //Convert JSON string to UTF - 8 byte array.
                                                     // AMQP messages are binary (byte arrays). UTF-8 is the standard encoding for JS




            //Create message properties.
            // IBasicProperties is a metadata wrapper for the AMQP message.
            var properties = _channel!.CreateBasicProperties();

            // Without this: messages are in-memory only — lost if RabbitMQ crashes before processing.
            // With this: messages are written to disk — survive broker restarts.
            properties.Persistent = true;// — message survives RabbitMQ broker restart.


            //Publish the message.----->
            _channel.BasicPublish(
                exchange: "",// default exchange routes directly by queue name
                routingKey: QueueName,// QueueName — with default exchange, routing key = queue name
                mandatory: false,//false — don't throw if no queue is bound (default exchange handles it)
                basicProperties: properties, //the metadata set above (Persistent)
                body: body); 

            _logger.LogDebug(
                "Published message to queue '{Queue}'. Type: {Type}",
                QueueName, typeof(T).Name);

            return Task.CompletedTask;
        }





        // WHY implement IDisposable?
        //   When the app shuts down (Ctrl+C or IIS stop), ASP.NET Core calls Dispose()
        //   on all Singleton services that implement IDisposable.
        //   Without this, the TCP connection to RabbitMQ stays open for the OS timeout period
        //   after the app exits — bad practice and can prevent clean container shutdown.
        //

        public void Dispose()
        {

            // IsOpen check before closing prevents ObjectDisposedException
           
            try
            {
                if (_channel is { IsOpen: true })
                {
                    _channel.Close();
                }

                if (_connection is { IsOpen: true })
                {
                    _connection.Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Exception during RabbitMQ connection disposal.");
            }
            finally
            {
                _channel?.Dispose();
                _connection?.Dispose();
            }
        }
    }
}