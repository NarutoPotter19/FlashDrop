namespace FlashDrop.API.Shared
{

    //Create the generic pagination wrapper for paginated results used by
    //               GetActiveProductsQuery  and any future
    //               paginated endpoint in the project



    // This is a plain class (not record, not abstract) because:
    //   - It needs a parameterless-compatible constructor for JSON deserialization
    //   - Its computed properties use expression-body syntax (no field backing)
    //   - It is mutable by design (properties have setters for serializer compatibility)
    public class PagedResponse<T>
    {

        // 1. Data — the current page's items.
        //
        // IEnumerable<T> instead of List<T> because:
        //   - Any collection type works: the handler can pass List<T>, T[], LINQ result
        //   - JSON serializers serialize all IEnumerable<T> as arrays
        //   - Liskov Substitution: code consuming PagedResponse<T> only needs to iterate

        public IEnumerable<T> Data { get; set; } = Enumerable.Empty<T>();




        //2. PageNumber: WHich page the client requensted( 1th Indexing)
        //// Page 1 = first page. Page 0 or negative = invalid (validated in handler).
        ///
        public int PageNumber { get; set; }

        // 3. PageSize: How many items per page the client requested.
        // PageSize 0 or negative = invalid (validated in handler).
        //// Typical reasonable values: 5, 10, 20. Cap at 50 to prevent abuse.
        public int PageSize { get; set; }





        // 4th:  Task: TotalCount — the TOTAL number of records in the full dataset
        // (not just this page). Used by the client to compute total pages and display
        // "Showing 11-20 of 247 products.


        public int TotalCount { get; set; }
        //we are going to calculte it in the handler function in follwing ways 
        //  1. var totalCount = await context.Products
        //       .Where(p => p.IsActive)
        //       .CountAsync();
        //2. You cannot derive this from Data.Count() because Data only holds one page.




   // ── COMPUTED PROPERTIES (derived, read-only, no setter)
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasNextPage=>PageNumber<TotalPages;
        public bool HasPreviousPage => PageNumber > 1;


        public PagedResponse(
       IEnumerable<T> data,
       int pageNumber,
       int pageSize,
       int totalCount)
        {
            Data = data;
            PageNumber = pageNumber;
            PageSize = pageSize;
            TotalCount = totalCount;
        }


        //Parameterless constructor is added .
    // Required for: JSON deserialization (if a client sends PagedResponse back),
    // and for EF Core or serializers that need a default constructor.
    // In practice, handlers always use the parameterized constructor above.
    public PagedResponse() { }




    }
}
