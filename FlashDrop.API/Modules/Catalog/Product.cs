using System.ComponentModel.DataAnnotations;// for [ConcurrencyCheck] attribute which will be used to handle race condition by optimistic Concurecy Control
using FlashDrop.API.Shared;// we wiil use base Entity from here 

namespace FlashDrop.API.Modules.Catalog
{
    public class Product : BaseEntity
    {

        public string Name { get; set; } = string.Empty;


        public string SKU { get; set; } = string.Empty;


        public decimal Price { get; set; }

        public string Description { get; set; } = string.Empty;


        //Task: IsActive — soft-delete / visibility flag.
    //
    // Default = true: newly created products are active by default.
    //
    // WHY NOT hard delete?
    //   - Order records reference ProductId. Hard-deleting breaks order history.
    //   - IsActive = false hides the product from listings (WHERE IsActive = true)
    //   - The product record stays in the DB for audit/history purposes.
    //   - Easy to re-activate a "discontinued" product later.

        public bool IsActive { get; set; } = true;




        // ══════════════════════════════════════════════════════════
        // THE MOST CRITICAL PROPERTY IN THE ENTIRE PROJECT
        // ══════════════════════════════════════════════════════════
        // Task: Stock — current inventory count.
        //
        // [ConcurrencyCheck] is a data annotation from:
        //   System.ComponentModel.DataAnnotations
        //
        // WHAT IT DOES:
        // When EF Core generates an UPDATE for this entity, instead of:
        //   UPDATE "Products" SET "Stock" = @newStock WHERE "Id" = @id
        //
        // It generates:
        //   UPDATE "Products" SET "Stock" = @newStock
        //   WHERE "Id" = @id AND "Stock" = @originalStock
        //                   ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
        //                   This extra condition is the key.
        //
        // WHY THIS MATTERS (Flash Sale Scenario):
        //
        // User A and User B both request the last item simultaneously.
        // Both read Stock = 1.
        //
        // User A's transaction commits first:
        //   UPDATE Products SET Stock=0 WHERE Id=X AND Stock=1 → 1 row updated ✓
        //   Stock in DB is now 0.
        //
        // User B's transaction runs:
        //   UPDATE Products SET Stock=0 WHERE Id=X AND Stock=1 → 0 rows updated ✗
        //   (DB stock is 0, not 1 — the WHERE condition fails)
        //   EF Core sees 0 rows affected → throws DbUpdateConcurrencyException
        //
        // ExceptionHandlingMiddleware catches DbUpdateConcurrencyException → HTTP 409
        // User B gets: "This item was updated by another request. Please try again."
        //
        // WITHOUT [ConcurrencyCheck]:
        //   Both users' UPDATEs succeed → Stock goes negative → oversell ❌

        [ConcurrencyCheck]
        public int Stock { get; set; }
    }
}
