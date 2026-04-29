

using FlashDrop.API.Shared.Exceptions;// for our custom ValidationException class
using FluentValidation;// IValidator<T>     
using MediatR;// for the pipeline behaviour;


namespace FlashDrop.API.Shared.Behaviors
{

    // Create the MediatR pipeline behavior that runs
    //               FluentValidation validators for every Command/Query
    //               BEFORE the handler executes.



    //ValidationBehavior is an open generic class.
    // The <TRequest, TResponse> means it will be applied to EVERY
    // Command/Query that flows through MediatR — we register it once
    // and it covers everything automatically.
    //
    // Constraint: where TRequest : notnull
    // This is required by MediatR's IPipelineBehavior interface.
    // It means the request (Command/Query) object cannot be null.
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {


        //We inject IEnumerable<IValidator<TRequest>>.
        // "IEnumerable" is KEY — there may be ZERO, ONE, or MULTIPLE validators
        // for a given request type.
        //
        // When AddValidatorsFromAssemblyContaining<Program>() runs in Program.cs,
        // it registers e.g. CheckoutCommandValidator for IValidator<CheckoutCommand>.
        // DI then injects it here automatically when CheckoutCommand flows through.
        //
        // If NO validator exists for a request type, the IEnumerable is empty
        // and we simply skip validation and call the handler. This is correct
        // behaviour — not every request needs a validator.


        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }


        // public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        // {

        //     if (!_validators.Any())
        //     {
        //         return await next();
        //     }


        //     var context = new ValidationContext<TRequest>(request);



        //     var validationResults = await Task.WhenAll(
        //    _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        //);



        //     var failures = validationResults
        //     .SelectMany(result => result.Errors)
        //     .Where(failure => failure != null)
        //     .ToList();



        //     if (failures.Count != 0)
        //     {
        //         var errorDictionary = failures
        //             .GroupBy(
        //                 failure => failure.PropertyName,         // KEY = field name
        //                 failure => failure.ErrorMessage          // VALUE = error message
        //             )
        //             .ToDictionary(
        //                 group => group.Key,
        //                 group => group.ToArray()                 // Array of strings per field
        //             );




        //         throw new Exceptions.ValidationException(errorDictionary);


        //         return await next();
        //     }







        public async Task<TResponse> Handle(TRequest request,RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        {
            //Task: STEP 1 — If no validators exist for this request,
            // skip validation entirely and call the next step in the pipeline.
            // This is an early exit for performance — no allocation of a context.
            if (!_validators.Any())
            {
                return await next();
            }

            //  STEP 2 — Build a FluentValidation context.
            // ValidationContext<TRequest> wraps the request object so validators
            // can access its properties. We pass InstanceToValidate = request.
            var context = new ValidationContext<TRequest>(request);

            // STEP 3 — Run ALL validators concurrently.
            // Task.WhenAll runs each validator's ValidateAsync in parallel.
            // This is more efficient than running them sequentially.
            //
            // Each ValidateAsync returns a ValidationResult containing
            // a list of ValidationFailure objects (each has PropertyName + ErrorMessage).
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))
            );

            // STEP 4 — Collect all failures from all validators.
            // SelectMany flattens: if 3 validators each return 2 failures → 6 failures total.
            // Where(f => f != null) safety check (FluentValidation can return null failures).
            var failures = validationResults
                .SelectMany(result => result.Errors)
                .Where(failure => failure != null)
                .ToList();

            //  STEP 5 — If any failures exist, throw our custom exception.
            // We group failures by PropertyName (field name) so the errors dictionary
            // has the shape: { "FieldName": ["error1", "error2"], ... }
            //
            // This is the shape our ValidationException constructor expects,
            // and the shape ExceptionHandlingMiddleware serializes to HTTP 400 JSON.
            if (failures.Count != 0)
            {
                var errorDictionary = failures
                    .GroupBy(
                        failure => failure.PropertyName,         // KEY = field name
                        failure => failure.ErrorMessage          // VALUE = error message
                    )
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToArray()                 // Array of strings per field
                    );

                // Task: Throw our custom ValidationException.
                // This bubbles up through MediatR → ExceptionHandlingMiddleware
                // → client receives HTTP 400 with field-level errors.
                // The handler NEVER executes.
                throw new Exceptions.ValidationException(errorDictionary);
            }

            //EP 6 — Validation passed. Call the next step.
            // 'next()' either calls the next IPipelineBehavior in the chain
            // (if more behaviors are registered) or calls the actual handler.
            // The handler's return value flows back here as TResponse.
            return await next();
        }

    }
}




//Key FluentValidation Rules
//.NotNull()               → not null
//.NotEmpty()              → not null/empty/whitespace
//.NotEqual(value)         → not equal to specific value
//.Equal(value)            → must equal specific value
//.Equal(x => x.OtherProp) → must equal another property
//.Length(min, max)        → string length between min and max
//.MinimumLength(n)        → string minimum length
//.MaximumLength(n)        → string maximum length
//.EmailAddress()          → valid email format
//.InclusiveBetween(a, b)  → number between a and b (inclusive)
//.ExclusiveBetween(a, b)  → number between a and b (exclusive)
//.GreaterThan(n)          → greater than n
//.LessThan(n)             → less than n
//.Matches(@"regex")       → matches regex pattern
//.IsInEnum()              → value is valid enum member
//.Must(predicate)         → custom bool predicate
//.MustAsync(predicate)    → async custom predicate (DB checks)
//.Custom(func)            → complex custom rule with context

//CONDITIONALS:
//.When(condition)         → only validate if condition is true
//.Unless(condition)       → only validate if condition is false
//.WithMessage("msg")      → custom error message
//.WithErrorCode("code")   → custom error code
//.WithName("Field Name")  → override field name in error message
