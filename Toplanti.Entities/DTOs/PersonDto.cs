using Core.Entities.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class PersonDto : Base
    {
        public string Name { get; set; }
        public string LastName { get; set; }
        public string? Email { get; set; }
        public int PersonId { get; set; }
        public int CenterId { get; set; }
        public string CenterPersonTypeName { get; set; }
    }
}
