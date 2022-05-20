using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class CenterPersonDTO
    {
        public int? CentersId { get; set; }
        public int? UserId { get; set; }
        public string ZoomId { get; set; }
        public string TeacherActive { get; set; }
    }
}
