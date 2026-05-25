namespace FlashDrop.API.Modules.Catalog.DTOs
{

    // ===>TopSellingProductDto is a plain data container for analytics results.
    //
    // REQUIREMENTS for SqlQueryRaw<T> mapping to work:
    //   1. Must be a plain class (NOT an EF Core entity with [Key] etc.)
    //   2. Must have a PARAMETERLESS constructor (EF Core instantiates it then sets properties)
    //   3. Property names MUST match the SQL column aliases (case-insensitive)


    // This is NOT registered as a DbSet — EF Core uses it purely as a result mapping target.
    public class TopSellingProductDto
    {


        // SQL aliases map to properties:
        //   "ProductId"   → ProductId
        //   "ProductName" → ProductName
        //   "TotalSold"   → TotalSold



        //Parameterless constructor required for SqlQueryRaw<T>.
        // EF Core creates the object with new TopSellingProductDto() then sets properties.


        //ProductId — maps to SQL alias "ProductId"
    // Source: p."Id" AS "ProductId"
        public TopSellingProductDto() { }

        public Guid ProductId { get; set; }

        //ProductName — maps to SQL alias "ProductName"
    // Source: p."Name" AS "ProductName"
        public string ProductName { get; set; } = string.Empty;

        public int TotalSold { get; set; }// Source: SUM(o."Quantity") AS "TotalSold"
                                          // Represents the total number of units sold across all confirmed orders.





    }
}
