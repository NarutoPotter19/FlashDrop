using FlashDrop.API.Modules.Catalog.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlashDrop.API.Modules.Catalog
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]// [Authorize(Roles = "Admin")] at class level.
                                // Every endpoint in AnalyticsController is admin-only.
                                // WHY admin-only? Analytics reveals business intelligence (sales volumes,
                                // top sellers) that should not be visible to customers or competitors.
    public class AnalyticsController : ControllerBase
    {

        private readonly ISender _sender;

        public AnalyticsController(ISender sender)
        {
            _sender = sender;
        }



        //    ── GET /api/analytics/top-selling ──

        //Returns the top-N best-selling products by confirmed order quantity.

        [HttpGet("top-selling")]
        public async Task<IActionResult> GetTopSelling([FromQuery] int topN=5, CancellationToken cancellationToken = default)
        {

            //// [FromQuery] int topN = 5:
            //   Query string parameter with default value of 5.
            //   Client can call:
            //     /api/analytics/top-selling          → topN = 5 (default)
            //     /api/analytics/top-selling?topN=3   → topN = 3
            //     /api/analytics/top-selling?topN=10  → topN = 10

            //   Validated by GetTopSellingProductsQueryValidator:
            //     1 ≤ topN ≤ 50, otherwise HTTP 400


            var result = await _sender.Send(new GetTopSellingProductsQuery(topN), cancellationToken);



            // Empty list returns as [] — not 404.
            // "No top sellers yet" is a valid state (no orders placed), not an error

            return Ok(result);
        }
    }
}
