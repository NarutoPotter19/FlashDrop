
using FlashDrop.API.Modules.Ordering.Commands;   // CheckoutCommand
using FlashDrop.API.Modules.Ordering.Queries; //GetMyOrdersQuery
using MediatR;                                   // ISender
using Microsoft.AspNetCore.Authorization;        // [Authorize]

using Microsoft.AspNetCore.Mvc;
// ControllerBase, ApiController, etc.


using Microsoft.AspNetCore.Mvc.ApiExplorer;
using System.Diagnostics;// for ApiExplorerSettings


namespace FlashDrop.API.Modules.Ordering
{
    // [Authorize] at CLASS LEVEL — the most important attribute here.
    // Every endpoint in this controller requires a valid JWT.
    // WHY class-level instead of per-method?
    //   Safer: no endpoint can accidentally be left unprotected.
    //   Cleaner: [Authorize] appears once, not on every method.
    //  If a future if i want to adds a new endpoint, it's protected by default
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrderController : ControllerBase
    {

        // ISender exposes only Send() for Commands/Queries.
        // IMediator also exposes Publish() for domain events — not needed here

        private readonly ISender _sender;


        // IHttpClientFactory for creating scoped HttpClient instances.
        // IHttpClientFactory is registered automatically by AddControllers() / the framework.
        // It manages HttpClient lifecycle properly (no socket exhaustion from new HttpClient()).
        private readonly IHttpClientFactory _httpClientFactory;

        public OrderController(ISender sender, IHttpClientFactory httpClientFactory)
        {
            _sender = sender;
            _httpClientFactory = httpClientFactory;

        }


        // ── POST /api/order/checkout Endpoint
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutCommand command, CancellationToken cancellationtoken)
        {

            //Dispatch the command to MediatR.
            // The handler (CheckoutCommandHandler) reads UserId internally
            // from IHttpContextAccessor — NOT from the command.
            // This is the secure design: UserId is never trust-able from the request body.
            //
            // ValidationBehavior runs BEFORE the handler — bad input is rejected here.
            // All business logic (stock check, transaction, event) is in the handler.
            // The controller just dispatches and returns.

            var result = await _sender.Send(command, cancellationtoken);


            //  HTTP 201 Created.
            // CreatedAtAction builds the Location header pointing to a "get by id"
            // equivalent. Since we don't have a standalone GET /order/{id} endpoint,
            // we reference the my-orders action that will exist .
            // If nameof(GetMyOrders) causes a compile error (method not yet added),
            // temporarily use a string: "GetMyOrders" or just return Ok(result) for now.
            // i will adds GetMyOrders to this controller — after that this compiles cleanly.
            //
            // Returning 201 (not 200) signals to the client: "a new resource was created."
            // The body is the OrderDto — the client gets the full order details immediately.
            return CreatedAtAction(nameof(GetMyOrders), result);


        }




        // ── GET /api/order/my-orders ENdpoint  ──

        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders(CancellationToken cancellationtoken)
        {

            //  Dispatch to GetMyOrdersQueryHandler.
            // Handler reads UserId from JWT claims internally.
            // Returns List<OrderDto> — possibly empty, never null

            var result = await _sender.Send(new GetMyOrdersQuery(), cancellationtoken);
            return Ok(result);
        }





        //Understanding this might be complax line by line ( Optional you can skip this if you want )
        // Flash Sale Concurrency Test Endpoint-

        //[ApiExplorerSettings(IgnoreApi = true)] :  Hides this endpoint from Swagger UI completely.

        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("flash-sale-test")]
        public async Task<IActionResult> FlashSaleTest([FromQuery] Guid productId, CancellationToken cancellationToken)
        {

            // Read the caller's JWT token from the Authorization header.
            // We forward it to each sub-request so they are authenticated.
            // Without forwarding, each sub-request gets 401 Unauthorized.
            var authorizationHeader = Request.Headers["Authorization"].ToString();



            if (string.IsNullOrEmpty(authorizationHeader))
            {
                return Unauthorized(new { message = "Authorization header required for flash-sale-test." });
            }



            //Step 2: Build the checkout URL.


            // We call our OWN checkout endpoint (POST /api/order/checkout) 20 times concurrently.

            // WHY call the HTTP endpoint instead of ISender.Send() directly?
            //   DbContext is SCOPED (one per HTTP request). If we called ISender.Send()
            //   from 20 tasks in the same request, all 20 would share the SAME DbContext
            //   instance — DbContext is NOT thread-safe. Concurrent operations on the same
            //   DbContext cause runtime errors.

            //   By calling the HTTP endpoint, each sub-request:
            //     - Gets its own request scope
            //     - Gets its own DbContext instance (from DI Scoped lifetime)
            //     - Runs its own independent transaction
            //   This correctly simulates 20 real concurrent users.


            var scheme = Request.Scheme; // gives http or https
            var host = Request.Host.Value;// pur local host 
            var checkoutUrl = $"{scheme}://{host}/api/order/checkout";



            //Step -3:The checkout request body.


            // Each of the 20 requests buys exactly 1 unit of the same product.
            // Requesting 1 unit per attempt makes the math clean:
            //   succeeded == initial stock count (e.g., 5 successes from 5 stock)

            var requestBody = System.Text.Json.JsonSerializer.Serialize(new
            {
                productId = productId,
                quantity = 1
            });



            //step-4:  Fire 20 concurrent checkout attempts.

            // Task.WhenAll starts all tasks simultaneously and waits for ALL to finish.
            //
            // Each task:
            //   1. Creates an HttpClient from the factory (proper lifecycle management)
            //   2. POSTs to /api/order/checkout with the Authorization header forwarded
            //   3. Returns a (bool success, string failureReason) tuple

            const int concurrentAttempts = 20;
            var tasks = Enumerable.Range(0, concurrentAttempts)
                .Select(_ => AttemptSingleCheckoutAsync(
                     checkoutUrl, requestBody, authorizationHeader))
                .ToList();


            var results = await Task.WhenAll(tasks);

            //Step 5 : Count the Outcome :
            int succeeded = results.Count(r => r.Success);
            int failedOutOfStock = results.Count(r => !r.Success && r.FailureType == "OutOfStock");
            int failedConcurrency = results.Count(r => !r.Success && r.FailureType == "Concurrency");
            int failedOther = results.Count(r => !r.Success && r.FailureType == "Other");


            //Step 6: etch the final product stock to show in the summary.
            // This queries the DB AFTER all concurrent requests complete to show ground truth.

            var finalProduct = await _sender.Send(
                new FlashDrop.API.Modules.Catalog.Queries.GetProductByIdQuery(productId), cancellationToken);



            //Step 7: Build the response summary.

            var response = new
            {
                productId = productId,
                totalAttempts = concurrentAttempts,
                succeeded = succeeded,
                failedOutOfStock = failedOutOfStock,
                failedConcurrencyConflict = failedConcurrency,
                failedOther = failedOther,
                finalStockLevel = finalProduct?.Stock ?? -1,
                summary = $"{concurrentAttempts} concurrent requests fired. " +
                     $"{succeeded} succeeded. " +
                     $"{concurrentAttempts - succeeded} rejected by concurrency protection. " +
                     $"Final stock: {finalProduct?.Stock ?? -1}."
            };


            return Ok(response);









        }




        // ── Private helper: single checkout attempt ──

        // Fires ONE checkout request and categorises the outcome.
        // Called 20 times concurrently by FlashSaleTest via Task.WhenAll.
        // Returns a record with: Success (bool), FailureType (string)
        // FailureType values: "OutOfStock", "Concurrency", "Other", or "" (success)

        //private async Task<(bool Success, string FailureType)> AttemptSingleCheckoutAsync(string url, string jsonBody, string authHeader)
        //{
        //    try
        //    {
        //        // Create a scoped HttpClient via IHttpClientFactory.
        //        // Never use 'new HttpClient()' directly — it doesn't reuse TCP connections
        //        // and can exhaust the socket pool under load.

        //        var client = _httpClientFactory.CreateClient();

        //        // Forward the caller's JWT so each sub-request is authenticated.
        //        client.DefaultRequestHeaders.Add("Authorization", authHeader);
        //        var content = new StringContent(                      
        //        jsonBody,                                         
        //        System.Text.Encoding.UTF8,                         
        //        "application/json");



        //        var response = await client.PostAsync(url, content);

        //        if (response.IsSuccessStatusCode)
        //        {
        //            return (true, string.Empty);
        //        }


        //        //Step 3: Read the response body to determine failure type.
        //        var body = await response.Content.ReadAsStringAsync();

        //        // HTTP 409 Conflict — could be OutOfStock or Concurrency conflict.
        //        // We distinguish by looking at the response detail text.
        //        if ((int)response.StatusCode == 409)
        //        {
        //            var failureType = body.Contains("another request", StringComparison.OrdinalIgnoreCase) 
        //            ? "Concurrency"                               
        //            : "OutOfStock";                                
        //            return (false, failureType);
        //        }


        //        return (false, "Other");

        //    }

        //    catch                                                    
        //    {                                                         
        //                                                               // Network errors, timeouts, etc.
        //        return (false, "Other");                             
        //    }
        //}



        private async Task<(bool Success, string FailureType)> AttemptSingleCheckoutAsync(string url, string jsonBody, string authHeader)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", authHeader);
                var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return (true, string.Empty);
                }

                var body = await response.Content.ReadAsStringAsync();

                if ((int)response.StatusCode == 409)
                {
                    var failureType = body.Contains("another request", StringComparison.OrdinalIgnoreCase) ||
                                      body.Contains("database operation", StringComparison.OrdinalIgnoreCase)
                    ? "Concurrency"
                    : "OutOfStock";
                    return (false, failureType);
                }

                // Print exact HTTP errors to the terminal so we don't guess
                Console.WriteLine($"[FLASH SALE ERROR] HTTP {(int)response.StatusCode}: {body}");
                return (false, "Other");
            }
            catch (Exception ex)
            {
                // Print network crashes to the terminal
                Console.WriteLine($"[FLASH SALE ERROR] Exception: {ex.Message}");
                return (false, "Other");
            }
        }


    }
}
