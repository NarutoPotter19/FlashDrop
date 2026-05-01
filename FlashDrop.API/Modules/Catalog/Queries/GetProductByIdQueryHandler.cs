using AutoMapper;
using FlashDrop.API.Modules.Catalog.DTOs;        // ProductDto
using FlashDrop.API.Shared.Data;                 // FlashDropDbContext
using FlashDrop.API.Shared.Exceptions;           // NotFoundException
using FlashDrop.API.Shared.Services;             // ICacheService
using MediatR;
using Microsoft.EntityFrameworkCore;             // AsNoTracking, FirstOrDefaultAsync



//The Cache-Aside query handler.
//               This is the performance-critical read path.
namespace FlashDrop.API.Modules.Catalog.Queries
{

    //GetProductByIdQueryHandler — implements the Cache-Aside pattern.
    //
    // IRequestHandler<GetProductByIdQuery, ProductDto> means:
    //   - Input:  GetProductByIdQuery (carries the product ID)
    //   - Output: ProductDto (the response returned to the controller)
    public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto>
    {

        private readonly FlashDropDbContext _context;
        private readonly ICacheService _cache;
        private readonly IMapper _mapper;

        //Three dependencies injected.
        //
        // FlashDropDbContext → Scoped (one per HTTP request)
        //   Used on CACHE MISS only — queries PostgreSQL for the product.
        //   On a cache hit, _context is injected but NEVER used.
        //
        //   EF Core is smart enough not to open a DB connection until needed.
        //
        // ICacheService → Singleton (RedisCacheService — stateless Redis wrapper)
        //   First check: GetAsync (did we cache this product before?)
        //   On miss: SetAsync after loading from DB (warm the cache)
        //
        // IMapper → Singleton (AutoMapper — configured via MappingProfile)
        //   Used on cache miss only: maps Product entity → ProductDto
        //   On cache hit, the cached value is already a ProductDto — no mapping needed.

        public GetProductByIdQueryHandler(FlashDropDbContext context, ICacheService cache, IMapper mapper)
        {
            _context = context;
            _cache = cache;
            _mapper = mapper;
        }


        public async Task<ProductDto> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
        {


            // Step 1: Building the Cache Key
            // Format: "product:{guid}"
            // Example: "product:3fa85f64-5717-4562-b3fc-2c963f66afa6"
            //
            // The "product:" prefix namespaces the key to avoid collision
            // with other entity types (order:{id}, user:{id}, etc.)

            var cacheKey = $"product:{query.Id}";



            // Step 2; Check the Cache

            var cacheProduct = await _cache.GetAsync<ProductDto>(cacheKey);

            // GetAsync returns:
            //   ProductDto   → cache HIT  (key exists in Redis)
            //   null         → cache MISS (key doesn't exist or expired)




            if (cacheProduct != null)
            {
                // Cache HIT: Return the cached ProductDto immediately.
                return cacheProduct;
            }


            // S==>tep 3: IN case we miss cache

            // .AsNoTracking():
            //   Loads the entity WITHOUT adding it to EF Core's change tracker.
            //   Safe for read-only operations — we never call SaveChangesAsync here.
            //   Benefits: less memory, less CPU overhead per entity loaded.
            //
            // .FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken):
            //   Generates: SELECT * FROM "Products" WHERE "Id" = @id LIMIT 1
            //   Returns the Product entity or null if not found.
            //   Passes cancellationToken: if client disconnects, DB query is cancelled.

            var product = await _context.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken);




            //Step 4 ==> Throu404 Error when product does not exist

            // If product is null, the ID doesn't exist in the database.
            // Throw NotFoundException → bubbles to ExceptionHandlingMiddleware → HTTP 404.



            if (product == null)
            {

                //THROW EXCEPTION
                throw new NotFoundException($"Product with ID {query.Id} not found");

                /// IMPORTANT: We do NOT cache this 404 result.
                // If a product is activated moments later, we want the next request
                // to actually hit the DB and find it — not serve a cached "not found".
            }



            //Step 5:
            //Map Entity to DTO
            var dto = _mapper.Map<ProductDto>(product);
            // Convert the Product entity (EF Core model) to ProductDto (API model).
            // Uses MappingProfile.CreateMap<Product, ProductDto>()..
            // CreatedAt is NOT in ProductDto — intentionally omitted.





            //Step 6: Populate the Cache




            await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(7));

            // Why 7 minutes?( Thala for Reason LOL)
            //   - Long enough: 50,000 users during a flash sale all benefit
            //     from the cached value set by the first request.
            //   - Short enough: if an admin updates stock or price,
            //     the cache refreshes within 7 minutes automatically.
            //
            // What if stock changes before 7 minutes?
            //   The CACHED stock count shown to users may be slightly stale.
            //   This is ACCEPTABLE — the REAL stock check happens inside
            //   CheckoutCommandHandler ( we will update in Future) inside a DB transaction.
            //   The cache is for display only; correctness is in the write path.
            //
            // We don't await this in a fire-and-forget way — we await it properly
            // because if SetAsync fails (Redis down), we want to know immediately
            // rather than silently failing to cache. The product DTO is still
            // returned even if cache population .

            return dto;

        }
    }
}
