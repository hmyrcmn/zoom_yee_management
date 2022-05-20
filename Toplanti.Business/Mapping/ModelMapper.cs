using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Business.Mapping
{
    internal static class ModelMapper
    {


        private static IMapper _mapper;

        public static IMapper Mapper
        {
            get
            {
                if (_mapper == null)
                {
                    var config = new MapperConfiguration(cfg =>
                    {
                    });

                    _mapper = config.CreateMapper();
                }
                return _mapper;
            }
        }


    }
}
