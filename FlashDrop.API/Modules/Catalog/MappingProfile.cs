using AutoMapper; //Profile and CreatMap
using FlashDrop.API.Modules.Catalog.DTOs;// for using ProductDTO that we have created

namespace FlashDrop.API.Modules.Catalog
{


    // MappingProfile MUST inherit AutoMapper.Profile.
//
// WHY inherit Profile?
// The AddAutoMapper(Assembly.GetExecutingAssembly()) call in Program.cs
// scans the assembly for all classes that INHERIT Profile.
// If you forget : Profile, AutoMapper will NOT find this class,
// and all Map<Product, ProductDto>() calls will throw at runtime:
//   "Missing type map configuration or unsupported mapping."
    public class MappingProfile : Profile
    {



        //Constructor — this is where mapping rules are defined.
        // AutoMapper calls this constructor when building its configuration
        // during app startup (inside AddAutoMapper()).

        public MappingProfile()
        {

            // This single line tells AutoMapper:
            //   "When I call _mapper.Map<ProductDto>(someProduct),
            //    copy each property from Product to the matching property in ProductDto."

            //   It automatically maps properties with the SAME NAME and COMPATIBLE TYPE.
            CreateMap<Product, ProductDto>();
        }
    }
}  
