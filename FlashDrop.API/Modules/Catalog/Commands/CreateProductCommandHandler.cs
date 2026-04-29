using AutoMapper;                                // IMapper
using FlashDrop.API.Modules.Catalog.DTOs;        // ProductDto
using FlashDrop.API.Shared.Data;                 // FlashDropDbContext
using MediatR;                                   // IRequestHandler<,>

namespace FlashDrop.API.Modules.Catalog.Commands
{

    //CreateProductCommandHandler implements the MediatR handler.
    //
    // IRequestHandler<CreateProductCommand, ProductDto> means:
    //   - Input:  CreateProductCommand (the message sent by the controller)
    //   - Output: ProductDto (what the controller receives back)
    //
    // MediatR discovers this class via:
    //   AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...)) in Program.cs
    // Registered as Transient by default — new instance per request (correct).
    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
    {
       

        private readonly FlashDropDbContext _dbContext;
        private readonly IMapper _mapper;

        //Two dependencies injected:
        //
        // FlashDropDbContext → Scoped (one per HTTP request)
        //   Used for: context.Products.Add() + SaveChangesAsync()
        //
        // IMapper → Singleton (registered by AddAutoMapper)
        //   Used for: _mapper.Map<ProductDto>(product)
        //   Note: Injecting Singleton into Transient is always safe.
        public CreateProductCommandHandler(FlashDropDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }



        //handle() is the entry point MediatR calls.
        // At this point, ValidationBehavior has ALREADY run and PASSED.

        public async Task<ProductDto> Handle(CreateProductCommand command, CancellationToken cancellationToken)
        {

            //Build the Product entity ──────────────────────────
            // We construct the entity from the validated command values.
            //
            // Properties NOT set here (auto-handled):
            //   Id        → set by BaseEntity constructor: Guid.NewGuid()
            //   CreatedAt → set by BaseEntity constructor: DateTime.UtcNow
            //
            // IsActive = true: newly created products are active by default.
            // An admin can deactivate them later via a separate endpoint
            var product = new Product
            {
                Name = command.Name,
                SKU = command.SKU,
                Price = command.Price,
                Description = command.Description,
                Stock = command.Stock,
                IsActive = true
            };




            //2. Persist to the postgrSQL

            _dbContext.Products.Add(product);///   Marks the entity as "Added" in EF Core's change tracker.
        //   No SQL is executed yet.

            await _dbContext.SaveChangesAsync(cancellationToken);///   /   Executes: INSERT INTO "Products" (...) VALUES (...)
            //   EF Core wraps it in a transaction automatically.






            //3. Mapt ENity to DTO and return


            //After SaveChangesAsync, the entity has been saved.
            // Its Id is the Guid generated in BaseEntity's constructor,
            // and CreatedAt is the UTC timestamp from that constructor.
            //
            
            var productDto = _mapper.Map<ProductDto>(product);
            return productDto;//// This returns the ProductDto that the controller will serialize
                              // to JSON and send back to the client as HTTP 201.

            //return _mapper.Map<ProductDto>(product);



        }
    }
