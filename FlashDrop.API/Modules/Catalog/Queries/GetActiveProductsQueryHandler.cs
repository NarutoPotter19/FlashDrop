using AutoMapper;                                // IMapper

using FlashDrop.API.Modules.Catalog.DTOs;//Prodcut DTOs
using FlashDrop.API.Shared;  // PagedResponse<T>
using FlashDrop.API.Shared.Data;// FlashDropDbContext
using MediatR;                                   // IRequestHandler<,>
using Microsoft.EntityFrameworkCore;             // AsNoTracking, CountAsync, ToListAsync


namespace FlashDrop.API.Modules.Catalog.Queries
{

    // Paginated list handler.
    // Returns only ACTIVE products in a paged, ordered format.
    public class GetActiveProductsQueryHandler : IRequestHandler<GetActiveProductsQuery, PagedResponse<ProductDto>>
    {



        private readonly FlashDropDbContext _context;
        private readonly IMapper _mapper;



        public GetActiveProductsQueryHandler(FlashDropDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }


        public async Task<PagedResponse<ProductDto>> Handle(GetActiveProductsQuery query, CancellationToken cancellationToken)
        {

            //QUery 1: COuting Toatal Active Products in here

            //IMP:-->

            // CountAsync runs a separate query:
            //   SELECT COUNT(*) FROM "Products" WHERE "IsActive" = true
            //
            // WHY separate from the data query?
            //   The data query uses LIMIT/OFFSET — it only returns 'pageSize' rows.
            //   We cannot derive the total from a partial result.
            //   CountAsync gets the FULL count across ALL pages.

            // Performance: COUNT(*) with an index on IsActive is very fast.
            // PostgreSQL can answer this from index statistics, not full table scan.

            var TotalCount = await _context.Products
                 .Where(p => p.IsActive)
                 .CountAsync(cancellationToken);




            //QUery 2:==> Fetch this product

            //Explaing all the method and what is happening here unders the hood 

            // .Where(p => p.IsActive):
            //   Filters to only active products. Inactive (soft-deleted) products
            //   are invisible to public catalog queries. Admin can still see them
            //   via a separate admin endpoint (not in this project scope).
            //
            // .AsNoTracking():
            //   Read-only query — no change tracking needed. Saves memory and CPU.
            //   Rule: EVERY query handler uses AsNoTracking().
            //
            // .OrderBy(p => p.CreatedAt):
            //   REQUIRED for deterministic pagination.
            //   Without OrderBy, SQL returns rows in undefined order.
            //   Same products could appear on page 1 AND page 2 without ordering.
            //   CreatedAt ASC = oldest products first (consistent catalog order).
            //
            // .Skip((query.PageNumber - 1) * query.PageSize):
            //   Converts 1-based page number to 0-based SQL OFFSET.
            //   Page 1: Skip = 0 (no rows skipped — first page)
            //   Page 2: Skip = 10 (skip first 10, start at row 11)
            //   Page 3: Skip = 20 (skip first 20, start at row 21)
            //
            // .Take(query.PageSize):
            //   SQL LIMIT — return at most 'pageSize' rows.
            //   On the last page, fewer rows may be returned (that's correct).
            //
            // .ToListAsync(cancellationToken):
            //   Executes the SQL and materialises results into memory as List<Product>.
            //   CancellationToken: cancels DB query if client disconnects.

            var products = await _context.Products
          .Where(p => p.IsActive)
          .AsNoTracking()
          .OrderBy(p => p.CreatedAt)
          .Skip((query.PageNumber - 1) * query.PageSize)
          .Take(query.PageSize)
          .ToListAsync(cancellationToken);






            // Task 3 ; Now map extracted Entities to DTOs

            // _mapper.Map<List<ProductDto>>(products) uses MappingProfile:
            //   CreateMap<Product, ProductDto>()
            // AutoMapper maps each Product in the list to a ProductDto automatically.

            var dto = _mapper.Map<List<ProductDto>>(products);



            //Task 4: Return wrapperd in Paged Response


            return new PagedResponse<ProductDto>(
           data: dto,
           pageNumber: query.PageNumber,
           pageSize: query.PageSize,
           totalCount: TotalCount
       );












        }






    }
}
