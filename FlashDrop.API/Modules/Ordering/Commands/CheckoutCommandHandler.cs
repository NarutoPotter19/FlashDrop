
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





        public CheckoutCommandHandler(FlashDropDbContext dbContext, IMapper mapper, IHttpContextAccessor httpContextAccessor, IEventBus eventBus, ILogger<CheckoutCommandHandler> logger)
        {

            _dbContext = dbContext;
            _mapper = mapper;
            _httpContextAccessor = httpContextAccessor;
            _eventBus = eventBus;                                    
            _logger = logger;
        }

       public async Task<OrderDto> Handle(CheckoutCommand command, CancellationToken cancellationToken)
{
    // // ── STEP 1: Extract UserId from JWT claims ──
    var userIdString = _httpContextAccessor.HttpContext!.User.FindFirst(ClaimTypes.NameIdentifier).Value!;

    if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
    {
        throw new UnauthorizedAccessException("Valid authentication is required to checkout.");
    }

    // // ── STEP 2: Load the Product — TRACKED (not AsNoTracking) 
    var product = await _dbContext.Products.FindAsync(
        new object[] { command.ProductId }, cancellationToken);

    // ── STEP 3: Throw NotFoundException if product doesn't exist ───
    if (product == null)
    {
        throw new NotFoundException($"Product with ID '{command.ProductId}' was not found.");
    }

    // ── PROACTIVE CONCURRENCY RETRY CONFIGURATION ──
    bool saved = false;
    int maxRetries = 10; 
    int currentRetry = 0;
    Order? order = null;

    while (!saved && currentRetry < maxRetries)
    {
        // ── STEP 4: Check stock BEFORE opening a transaction 
        if (product.Stock < command.Quantity)
        {
            throw new OutOfStockException(
                $"Product '{product.Name}' has insufficient stock. " +
                $"Requested: {command.Quantity}, Available: {product.Stock}.");
        }

        // // ── STEP 5: Open an explicit database transaction ──
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // ── STEP 6: Deduct stock ──
            product.Stock -= command.Quantity;

            // Step 7- Creae order after deduction of stock 
            order = new Order
            {
                ProductId = command.ProductId,
                UserId = userId,
                Quantity = command.Quantity,
                TotalPrice = product.Price * command.Quantity,
                Status = OrderStatus.Confirmed
            };

            _dbContext.Orders.Add(order);

            // ── STEP 8: Save all changes atomically ─
            await _dbContext.SaveChangesAsync(cancellationToken);

            // step -9 Commit the Transaction 
            await transaction.CommitAsync(cancellationToken);
            
            saved = true; // Exit loop on success
        }
        catch (DbUpdateConcurrencyException)
        {
            // If a conflict happens, roll back this attempt
            await transaction.RollbackAsync(cancellationToken);

            // Important: Detach the failed order so it doesn't try to save again in the next loop
            if (order != null)
            {
                _dbContext.Entry(order).State = EntityState.Detached;
            }

            currentRetry++;
            if (currentRetry >= maxRetries) throw; // Rethrow if we exhausted retries

            // RELOAD the product from the database to get the NEW Stock value
            await _dbContext.Entry(product).ReloadAsync(cancellationToken);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // ── STEP 10: Publish OrderConfirmedEvent (fire-and-forget) ──
    try
    {
        var orderConfirmedEvent = new OrderConfirmedEvent(
            OrderId: order!.Id,
            UserId: order.UserId,
            ProductName: product.Name,
            Quantity: order.Quantity,
            TotalPrice: order.TotalPrice,
            ConfirmedAt: DateTime.UtcNow
        );

        await _eventBus.PublishAsync(orderConfirmedEvent);

        _logger.LogInformation("OrderConfirmedEvent published for Order {OrderId}", order.Id);
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex,
            "Failed to publish OrderConfirmedEvent for Order {OrderId}. " +
            "Order is still valid in the database. " +
            "Invoice generation may be delayed.",
            order!.Id);
    }

    // // ── STEP 11: Map Order entity → OrderDto ──
    var dto = _mapper.Map<OrderDto>(order);
    dto.ProductName = product.Name;

    return dto;
}
    }
    }
