
using AutoMapper;// for AutoMapper
using FlashDrop.API.Modules.Catalog;                 // Product entity
using FlashDrop.API.Modules.Ordering.DTOs;           // OrderDto
using FlashDrop.API.Shared.Data;                     // FlashDropDbContext
using FlashDrop.API.Shared.Exceptions;               // NotFoundException, OutOfStockException
using MediatR;// for IRequestHandler<CheckoutCommand, OrderDto>
using Microsoft.AspNetCore.Http;                // for IHttpContextAccessor to access the current user's claims/Id( IHttpContextAccessor) in the handler
using Microsoft.EntityFrameworkCore;                // for EF Core extension methods like FirstOrDefaultAsync, SaveChangesAsync, etc.
using System.Security.Claims;//for (claim Principle) but here  claimTypes.NameIdentifier to get the user id from the token in the checkout handler

using FlashDrop.API.Modules.Ordering.Events;//fro fetching creATED eVENT DEATILS 
using FlashDrop.API.Shared.Services;// we are going to be using IEventBus and RabbitMQEventBus in this 
using Microsoft.Extensions.Logging;                                // [+] ADDED: Prompt 34 — ILogger<T>




namespace FlashDrop.API.Modules.Ordering.Commands
{

    //The most important handler in the project.
    //               Implements the complete checkout business logic:
    //               load → validate → deduct → save (transaction) → map → return.


    // CheckoutCommandHandler processes the complete checkout flow.

    public class CheckoutCommandHandler : IRequestHandler<CheckoutCommand, OrderDto>
    {



        //// Registered as Transient by MediatR (one instance per request) — correct.
        // Transient handlers can safely inject Scoped services (DbContext).
        //3 dependicies for this handleer




        // FlashDropDbContext → Scoped (one per HTTP request):
        //   Used for all DB operations: load product, save order.
        //   MUST be the tracked version (no AsNoTracking) for [ConcurrencyCheck] to work.
        private readonly FlashDropDbContext _dbContext;


        // IMapper → Singleton (AutoMapper, configured via MappingProfile):
        //   Maps Order entity → OrderDto after save
        private readonly IMapper _mapper;


        // IHttpContextAccessor → Singleton :
        //   Provides access to HttpContext.User.Claims from inside the handler.
        //   Used to read UserId from the JWT 'sub' claim — NOT from the request body.
        //   Security: JWT claims are validated and tamper-proof; request body is not.
        private readonly IHttpContextAccessor _httpContextAccessor;


        private readonly IEventBus _eventBus;

        private readonly ILogger<CheckoutCommandHandler> _logger;





        public CheckoutCommandHandler(FlashDropDbContext dbContext, IMapper mapper, IHttpContextAccessor httpContextAccessor)
        {

            _dbContext = dbContext;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _eventBus = eventBus;                                    
            _logger = logger;
        }



        public async Task<OrderDto> Handle(CheckoutCommand command, CancellationToken cancellationToken,ILogger logger,IEventBus eventBus)
        {
            // // ── STEP 1: Extract UserId from JWT claims ──

            // The JWT middleware (Prompt 13) populates HttpContext.User when a
            // valid Bearer token is present. The 'sub' claim (ClaimTypes.NameIdentifier)
            // contains the user's Guid ID

            // FindFirst(ClaimTypes.NameIdentifier) looks for the claim with type
            // "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier

            var userIdString = _httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier).Value!;


            //This Line is just an extra check kind of like a defensive programming althoough we have already 
            //checked by adding [Authorize] is on the endpoint
            //TryParse will also parse userIdString to userId 
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                // This should never happen if [Authorize] is on the endpoint —
                // the JWT middleware would have rejected the request before reaching here.
                // This guard is defensive programming for belt-and-suspenders safety.
                throw new UnauthorizedAccessException("Valid authentication is required to checkout.");
            }






            // // ── STEP 2: Load the Product — TRACKED (not AsNoTracking) 

            //we are not going to add AsNoTracking as we have added concurencyCheck and for it work
            // without it will not work pls look why > in the Priject notes for Explaining
            // 
            // FindAsync is efficient for primary key lookups (checks change tracker
            // first, then DB). It is tracking-safe and the correct method here.
            var product = await _dbContext.Products.FindAsync(
             new object[] { command.ProductId }, cancellationToken);


            // ── STEP 3: Throw NotFoundException if product doesn't exist ───

            if (product == null)
            {
                throw new NotFoundException(
                    $"Product with ID '{command.ProductId}' was not found.");
            }


            // ── STEP 4: Check stock BEFORE opening a transaction 

            // Fail-fast principle: if we already know there's no stock, there's
            // no point opening a database transaction (which acquires locks and
            // consumes connection resources).

            // This check is NOT the concurrency-safe check — it's a pre-flight.
            // The REAL concurrency protection is the [ConcurrencyCheck] in SaveChangesAsync.
            // Why? Because between this check and SaveChangesAsync, another transaction
            // could deduct the last item. The [ConcurrencyCheck] catches that scenario.

            if (product.Stock < command.Quantity)
            {
                throw new OutOfStockException(
                    $"Product '{product.Name}' has insufficient stock. " +
                    $"Requested: {command.Quantity}, Available: {product.Stock}.");
            }




            //  // ── STEP 5: Open an explicit database transaction ──

            // BeginTransactionAsync: opens a PostgreSQL transaction.
            // 'await using': ensures the transaction is disposed (rolled back if
            // not committed) when the using block exits, even on exception.
            //
            // Both operations (stock deduction + order insert) must commit together:
            //   - If stock deduction commits but order insert fails → inconsistency
            //   - If order insert commits but stock deduction fails → ghost order
            // The transaction guarantees all-or-nothing atomicity.

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {

                // ── STEP 6: Deduct stock ──

                
                // EF Core change tracker records:
                //   Original Stock (snapshot from FindAsync): e.g., 5
                //   New Stock (after this line):              e.g., 3 (if Quantity=2)
                //
                // When SaveChangesAsync runs, this becomes:
                //   UPDATE "Products" SET "Stock" = 3
                //   WHERE "Id" = @productId AND "Stock" = 5   ← [ConcurrencyCheck] adds this
                //
                // If another transaction already changed Stock from 5 to 4 (one item sold):
                //   The WHERE condition "AND Stock = 5" fails (DB stock is 4, not 5)
                //   → 0 rows updated → EF Core throws DbUpdateConcurrencyException
                product.Stock -= command.Quantity;


                //Step 7- Creae order after deduction of stock 

                var order = new Order
                {
                    ProductId = command.ProductId,
                    UserId = userId,
                    Quantity = command.Quantity,

                    // TotalPrice snapshot: locks in the price at purchase time.
                    // If admin raises Price later, this order's total is preserved.
                    TotalPrice = product.Price * command.Quantity,

                    // Status = Confirmed: we confirmed stock exists (Step 4) and
                    // are about to commit the deduction atomically.
                    // BaseEntity constructor auto-sets: Id = Guid.NewGuid(), CreatedAt = UtcNow
                    Status = OrderStatus.Confirmed
                };


                _dbContext.Orders.Add(order);



                // ── STEP 8: Save all changes atomically ─

                // This single SaveChangesAsync call executes BOTH:
                //   1. UPDATE "Products" SET "Stock"=@new WHERE "Id"=@id AND "Stock"=@original
                //   2. INSERT INTO "Orders" (...) VALUES (...)
                //
                // Both run in the same transaction. If either fails, both roll back.
                //
                // DbUpdateConcurrencyException is thrown if:
                //   - Another transaction committed a stock change between our read (Step 2)
                //     and this save — the WHERE "Stock"=@original condition fails.
                //
                // This exception propagates out of the try block → caught by outer catch
                await _dbContext.SaveChangesAsync(cancellationToken);

                // step -9 Commit the Transaction 

                await transaction.CommitAsync(cancellationToken);




                // ── STEP 10: Publish OrderConfirmedEvent (fire-and-forget) ──
                //This block is AFTER CommitAsync() and OUTSIDE
                // the try/catch that has RollbackAsync in it.

                //Explanation: But i would recoomend to look at Notes
                // If this publish block were INSIDE the try block:
                //   A publish failure would trigger catch → RollbackAsync()
                //   But the transaction is already committed — RollbackAsync on a committed
                //   transaction is a no-op in PostgreSQL (the data stays).
                //   However, the exception would propagate → HTTP 500 returned to client.
                //   Client thinks order FAILED but database says order SUCCEEDED → confusion.
                //
                // The CORRECT pattern: publish is best-effort, outside the transaction scope.
                //   DB commit = correctness requirement (must succeed)
                //   Event publish = notification (nice to have, not a correctness requirement)
                //
                // Analogous to: you confirm a hotel booking, THEN send the confirmation

                try
                {
                    var orderConfirmedEvent = new OrderConfirmedEvent(
                OrderId: order.Id,
                UserId: order.UserId,
                ProductName: product.Name,
                Quantity: order.Quantity,
                TotalPrice: order.TotalPrice,
                ConfirmedAt: DateTime.UtcNow
            );

                    await _eventBus.PublishAsync(orderConfirmedEvent);

                    _logger.LogInformation(
                "OrderConfirmedEvent published for Order {OrderId}", order.Id);

                }


                catch(Exception ex)
                {



                    //Log a WARNING and SWALLOW the exception.
                    // We DO NOT rethrow. This is deliberate.  
                    // The order is already committed to PostgreSQL  The customer's purchase is valid and real.  
                    // Rethrowing would cause the controller to return HTTP 500,
                    // making the customer think the checkout FAILED when it  
                    // actually SUCCEEDED — the worst possible user experience. 

                    // By logging a Warning (not Error), we:                   
                    //   1. Signal the team that invoicing may be delayed       
                    //   2. Provide enough info to manually re-send the event  
                    //   3. Never disrupt the customer's experience
                    //   
                    // In production, I would implement the Transactional     
            // Outbox Pattern to guarantee zero message loss.          

                    _logger.LogWarning(                                    
                ex,                                                
                "Failed to publish OrderConfirmedEvent for Order {OrderId}. " + 
                "Order is still valid in the database. " +         
                "Invoice generation may be delayed.",             
                order.Id);


                }



                // // ── STEP 11: Map Order entity → OrderDto ──

                var dto = _mapper.Map<OrderDto>(order);
                dto.ProductName = product.Name;

                return dto;
            }


            catch
            {
                // If anything goes wrong (SaveChangesAsync, CommitAsync, mapping),
                // roll back the transaction. 'await using' would handle this
                // automatically on disposal, but explicit rollback is clearer
                // and faster (doesn't wait for disposal).
                await transaction.RollbackAsync(cancellationToken);
                throw;  // Re-throw original exception — do not swallow it
            }




        }
    }
    }
