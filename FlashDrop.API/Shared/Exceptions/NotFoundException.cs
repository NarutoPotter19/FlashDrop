namespace FlashDrop.API.Shared.Exceptions
{

    //  this is a  Custom exception for "resource does not exist".
    //               Thrown from MediatR handlers when a database lookup
    //               returns null (e.g., Product with given ID not found).
    //               ExceptionHandlingMiddleware (Prompt 4) will catch this
    //               and return HTTP 404 Not Found.



    //this cutome exceptino is inherting from exception class
    //so that  it can work with middleware pipline and standard try/catch blocks.
    public class NotFoundException : Exception
    {
       // Constructor takes only a message string.
    // Usage example in a handler:
    //   throw new NotFoundException($"Product with ID {id} was not found.");
   
        public NotFoundException(string message) : base(message)
        {
        }


    }
}
