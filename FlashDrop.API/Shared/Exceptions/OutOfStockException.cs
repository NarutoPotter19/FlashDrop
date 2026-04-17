namespace FlashDrop.API.Shared.Exceptions
{
    public class OutOfStockException : Exception
    {

        // Usage example:
        //   throw new OutOfStockException(
        //       $"Product '{product.Name}' only has {product.Stock} units remaining.");
        public OutOfStockException(string message) : base(message)
        {

        }
    }
}
