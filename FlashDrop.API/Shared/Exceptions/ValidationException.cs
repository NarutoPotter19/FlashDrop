namespace FlashDrop.API.Shared.Exceptions
{
    public class ValidationException : Exception
    {
        //Errors is a dictionary where:
        //   KEY   = field name (e.g., "Quantity", "Email")
        //   VALUE = array of error messages for that field
        //           (one field can have multiple errors)
        //
        // Example content:
        // {
        //   "Quantity": ["Quantity must be between 1 and 5."],
        //   "ProductId": ["ProductId must not be empty.", "ProductId is not a valid GUID."]
        //   }
        //


        public IDictionary<string, string[]> Errors { get; }



        //this is our constructor which is going to 
        // accepts the structured errors dictionary.
        // Called from ValidationBehavior like this:
        //   throw new ValidationException(failures
        //       .GroupBy(f => f.PropertyName)
        //       .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()));
        public ValidationException(IDictionary<string, string[]> errors)
       : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }



    }
}
