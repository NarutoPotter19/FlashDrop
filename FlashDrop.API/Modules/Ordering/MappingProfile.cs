using AutoMapper;
using FlashDrop.API.Modules.Ordering.DTOs;//we are going to use Ordering DTO to map with the mapping profile 


namespace FlashDrop.API.Modules.Ordering
{

    //: Ordering module's AutoMapper profile.
    // This profile is discovered alongside Modules/Catalog/MappingProfile.cs.
// Both run during startup — both maps are available via IMapper.
    public class MappingProfile : Profile
    {

        public MappingProfile()
        {
            /*Now there are two kinds of mappings are there in the Mapping profile 
             * 1. automapping: which can be donenby following way
             //   Order.Id          → OrderDto.Id          ✓
        //   Order.ProductId   → OrderDto.ProductId   ✓
        //   Order.Quantity    → OrderDto.Quantity    ✓



            2. Manual Mapping : for this we need to use ForMember() methid
             */



            /* 
            First ForMember: Status enum → string conversion.
            //
            // Order.Status is type OrderStatus (enum stored as int: 0, 1, 2).
            // OrderDto.Status is type string ("Pending", "Confirmed", "Failed").
            //
            // .ToString() on an enum returns its name: OrderStatus.Confirmed.ToString() = "Confirmed"
             */

            CreateMap<Order, OrderDto>()
                .ForMember(
                dest => dest.Status,
                opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(
                dest => dest.ProductName,
                opt => opt.Ignore());

            /* 
             2nd ForMember: ProductName is not on the Order entity at all — it must be populated by the handler.
            if we do not add opt.ignore() it will throw error
            Ignore() tells AutoMapper: "leave ProductName as its default value
            Handler will set it after Mapping 
             */
        }


    }
}
