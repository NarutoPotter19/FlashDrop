using FlashDrop.API.Modules.Ordering.DTOs;
using FluentValidation;
using MediatR;

namespace FlashDrop.API.Modules.Ordering.Commands
{

    //// Goal: CheckoutCommand record + FluentValidation Validator No DI registration needed.
    // Both Command and Validator are auto-discovered via assembly scanning.



    //Defining  the checkout Command(the MediatR message)
    //               and its FluentValidation Validator (the business rules).
    //               Both live in this one file — they are tightly coupled.


    //Command: the message sent by the client to trigger checkout.
    public record CheckoutCommand 
    (

        //// WHY only ProductId and Quantity (not UserId)?
        //   UserId is read from the JWT 'sub' claim in the HANDLER:
        //     var userId = Guid.Parse(HttpContext.User.FindFirst("sub").Value);
        //
        //   If the client could supply UserId in the request body, a malicious
        //   user could checkout on behalf of another user. Reading UserId from
        //   the JWT token (validated by the JWT middleware) is tamper-proof.
        //   This is a critical security design decision.
        Guid ProductId,
        int Quantity
        ) :IRequest<OrderDto>;





    //Validator: the business rules for validating the checkout request.



    public class CheckoutCommonnValidator : AbstractValidator<CheckoutCommand>
    {
        public CheckoutCommonnValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithMessage("ProductId is required.");


            RuleFor(x => x.Quantity)
          .GreaterThanOrEqualTo(1)
          .WithMessage("Quantity must be at least 1.")
          .LessThanOrEqualTo(5)
          .WithMessage("Quantity cannot exceed 5 units per order.");
        }
    }



}
