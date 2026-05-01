
using FluentValidation;// for AbstractValidator<T> ,RuleForand ValidationResult(used for applying fluent validation)
using FlashDrop.API.Modules.Catalog.DTOs;// we can use our productDTOs that we made in this
using MediatR;// for IRequest<T>

namespace FlashDrop.API.Modules.Catalog.Commands
{

    // Define the Command(the message) and its Validator
    //               (the rules) in ONE FILE — they are tightly coupled.
    //               The handler lives in a separate file for clarity.


    //COMMAND — "I want to create a product with these values"



    /// CreateProductCommand is a record that carries
    // all data needed to create a new product.
    //
    // 'record' is ideal for Commands because:
    //   - Immutable: once created, the values cannot change
    //   - Value equality: two commands with same values are considered equal
    //   - Concise positional syntax: no boilerplate constructor/properties
    //
    // ': IRequest<ProductDto>' tells MediatR:
    //   - This is a dispatchable message
    //   - When handled, it returns a ProductDto
    //   - MediatR will find IRequestHandler<CreateProductCommand, ProductDto> 

    public record CreateProductCommand(
     string Name,
     string SKU,
     decimal Price,
     string Description,
     int Stock
 ) : IRequest<ProductDto>;







    //VALIDATOR — "these are the rules the command must satisfy"

    //CreateProductCommandValidator validates every
    // CreateProductCommand BEFORE the handler runs.

    // HOW IT GETS CALLED (the automated pipeline):
    //   1. Controller calls ISender.Send(new CreateProductCommand(...))
    //   2. MediatR invokes ValidationBehavior<CreateProductCommand, ProductDto>
    //   3. ValidationBehavior resolves IValidator<CreateProductCommand> from DI
    //      → finds CreateProductCommandValidator (registered by AddValidatorsFromAssemblyContaining)
    //   4. ValidateAsync() runs the rules below
    //   5. If any fail → throws ValidationException → HTTP 400 (handler never runs)
    //   6. If all pass → handler runs


    public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
    {

        public CreateProductCommandValidator()
        {
            // Name validation
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("Name is Required")
                .MaximumLength(20)
                .WithMessage("Name must not exceed 20 characters");


            //Sku validation

            RuleFor(x => x.SKU)
                .NotEmpty()
                .WithMessage("SKU is Required")
                .MaximumLength(30)
                .WithMessage("SKU must not exceed 30 characters");


            // Price validation



            RuleFor(x => x.Price)
                .GreaterThan(0)
                .WithMessage("Price can not be negative or zero");


            // Stock Validation

            RuleFor(x => x.Stock)
           .GreaterThanOrEqualTo(0)
           .WithMessage("Stock cannot be negative.");


            //Description Validation

            RuleFor(x => x.Description)
           .MaximumLength(2000)
           .WithMessage("Description cannot exceed 2000 characters.");
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
