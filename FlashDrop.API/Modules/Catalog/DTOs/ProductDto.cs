namespace FlashDrop.API.Modules.Catalog.DTOs
{

    //Create the ProductDto — the API response shape
//               for all product-related endpoints.




     //==> ProductDto is a plain CLASS(not a record).
//
// WHY NOT a record?
//   AutoMapper sets properties after construction using the parameterless
//   constructor. Records with positional parameters require all values
//   in the constructor — AutoMapper can't do this without extra config.
//   A plain class with { get; set; } properties is what AutoMapper
//   expects out of the box. Zero extra configuration needed.
//
    public class ProductDto
    {


        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;


        public string SKU { get; set; } = string.Empty;


        public decimal Price { get; set; }


        public string Description { get; set; } = string.Empty;

        public int Stock { get; set; }


        public bool IsActive { get; set; }

    }
}
 