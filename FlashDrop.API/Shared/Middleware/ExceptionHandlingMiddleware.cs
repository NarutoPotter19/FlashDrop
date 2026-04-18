using FlashDrop.API.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;//for the ProblemDetails class
using System.Collections.Generic;
using System.Net;
using System.Reflection.Emit;
using System.Text.Json;//to serialize the ProblemDetails to JSON
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using static System.Net.WebRequestMethods;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FlashDrop.API.Shared.Middleware
{

    //Creating  the global exception handling middleware for this project.
    //               This is the safety net for the entire application.
    //               Every unhandled exception thrown anywhere in the
    //               request pipeline is caught here and converted to
    //               a clean RFC 7807 ProblemDetails JSON response.


    // This is a conventional middleware class (not minimal API).
    // It follows the ASP.NET Core middleware convention:
    //   - Constructor receives RequestDelegate (the next middleware in pipeline)
    //   - InvokeAsync(HttpContext) is called for every HTTP request
    public class ExceptionHandlingMiddleware
    {

        //Task: _next holds a reference to the next middleware.
    // We call it inside a try block — if it throws, we catch the exception here.
        private readonly RequestDelegate _next;


        //Task: ILogger is injected for structured logging of exceptions.
    // We log the full exception at Error level before returning the response.
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        // ASP.NET Core DI automatically provides RequestDelegate and ILogger.
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }


       // Task: InvokeAsync is called on EVERY HTTP request.
    // The try/catch wraps the entire downstream pipeline.
        public async Task InvokeAsync(HttpContext context)
        {

            try
            {
                // Pass control to the next middleware/controller.
                // If anything downstream throws, execution jumps to the catch block.
                await _next(context);
            }
            catch (Exception exception)
            {
                //  Log the FULL exception (message + stack trace) at Error level.
                // This is visible in console and log files. Crucial for debugging production issues.
                _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

                //Determine the correct HTTP status code and title
                // by checking the specific exception type.
                await HandleExceptionAsync(context, exception);
            }

        }



        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {

            //Task: Map each custom exception type to an HTTP status code.
            // We use pattern matching on exception type — clean and readable.

            var (statusCode, title, errors) = exception switch
            {
                // Task: NotFoundException → 404
                // The requested resource doesn't exist in the database.
                NotFoundException notFound =>
                    (HttpStatusCode.NotFound, "Not Found", (IDictionary<string, string[]>?)null),

                //  Task: ValidationException → 400
                // Input failed FluentValidation rules. Include the errors dictionary
                // so the client knows which FIELDS failed and WHY.
                Shared.Exceptions.ValidationException validationEx =>
                    (HttpStatusCode.BadRequest, "Bad Request", validationEx.Errors),

                //  Task: OutOfStockException → 409
                // The request was valid, but server state (stock level) conflicts with it.
                OutOfStockException =>
                    (HttpStatusCode.Conflict, "Conflict", null),

                //  Task: EVERYTHING ELSE → 500
                // Unexpected bugs, null reference exceptions, database failures, etc.
                // We don't expose internal details to the client (security risk).
                _ => (HttpStatusCode.InternalServerError, "Internal Server Error", null)
            };




            //Task: Build the RFC 7807 ProblemDetails object.
            // ProblemDetails is the official ASP.NET Core class for this standard.
            var problemDetails = new ProblemDetails
            {
                //  Task: "type" — URI pointing to the RFC 7807 specification.
                Type = "https://tools.ietf.org/html/rfc7807",

                //  Task: "title" — short human-readable summary of the error type.
                Title = title,

                //Task: "status" — the numeric HTTP status code.
                Status = (int)statusCode,

                //Task: "detail" — the specific exception message.
                // For 500 errors we hide the real message and show generic text.
                Detail = exception is NotFoundException or Shared.Exceptions.ValidationException or OutOfStockException
                    ? exception.Message
                    : "An unexpected error occurred. Please try again later.",

                //  Task: "instance" — the URL path that caused the error.
                // Helps the client know which endpoint was called.
                Instance = context.Request.Path
            };



            //Task: For validation errors, attach the field - level errors
            // to the Extensions dictionary of ProblemDetails.
            // This will appear as "errors": { "Quantity": [...] } in the JSON response.
            if (errors is not null)
            {
                problemDetails.Extensions["errors"] = errors;
            }




            // Task: Set the HTTP response status code.
            context.Response.StatusCode = (int)statusCode;



            // Prompt4 Task: Tell the client we're sending JSON.
            // "application/problem+json" is the RFC 7807 media type — more specific than
            // "application/json" and signals to clients that this is a structured error.
            context.Response.ContentType = "application/problem+json";

            //Task: Serialize ProblemDetails to JSON and write to response body.
            // JsonSerializerOptions with camelCase naming ensures JSON keys match
            // what frontend developers expect (e.g., "statusCode" not "StatusCode").

            await context.Response.WriteAsync(
           JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
           {
               PropertyNamingPolicy = JsonNamingPolicy.CamelCase
           })
       );



        }

    }

}
