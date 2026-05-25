using FlashDrop.API.Modules.Catalog.DTOs;
using FlashDrop.API.Shared.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace FlashDrop.API.Modules.Catalog.Queries
{


    //====> Analytics handler using raw SQL via EF Core.

    // Demonstrates  that EF Core supports raw SQL when LINQ would be awkward.
    public class GetTopSellingProductsQueryHandler: IRequestHandler<GetTopSellingProductsQuery, List<TopSellingProductDto>>
    {

        //unlike other query handler which uses AUttomapper as well this will not use it as raw Sql directly map to DTO
        //And also  we are not going to use Cache as well as analytics data  are gerneraly not cached But if required we will use it in future

        private readonly FlashDropDbContext _context;
        public GetTopSellingProductsQueryHandler(FlashDropDbContext context)
        {
            _context = context;
        }

        public async Task<List<TopSellingProductDto>> Handle(GetTopSellingProductsQuery query,CancellationToken cancellationToken)
        {

            const string sql = @"
            SELECT
                p.""Id""            AS ""ProductId"",
                p.""Name""          AS ""ProductName"",
                SUM(o.""Quantity"") AS ""TotalSold""
            FROM ""Orders"" AS o
            INNER JOIN ""Products"" AS p ON o.""ProductId"" = p.""Id""
            WHERE o.""Status"" = 1
            GROUP BY p.""Id"", p.""Name""
            ORDER BY ""TotalSold"" DESC
            LIMIT {0}";





            // Step 2: Parameterised query — prevents SQL injection.
            // Even though TopN is an int (technically can't inject SQL), always use parameters.
            // Habit: if you parameterise integers, you'll remember to parameterise strings.



            // NpgsqlParameter: the Npgsql-specific parameter type.
            //   Constructor: (parameterName, value)
            //   The {0} placeholder in the SQL corresponds to index 0 of the params array.


            // SqlQueryRaw<TopSellingProductDto>(sql, params):
            //   EF Core executes the SQL and maps each row to TopSellingProductDto.
            //   Returns IQueryable<TopSellingProductDto> — we chain .ToListAsync() to execute.


            var results = await _context.Database
                        .SqlQueryRaw<TopSellingProductDto>(sql, query.TopN)
                        .ToListAsync(cancellationToken);

//Returns an empty list[] if no orders exist — not null, not exception. 
            return results;
        }


    }
}
