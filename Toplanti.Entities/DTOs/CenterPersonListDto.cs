using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class CenterPersonListDto
    {
        public bool? TeacherActive { get; set; }
        public int? CentersId { get; set; }
        public int? UserId { get; set; }
        public int? CenterPersonTypeId { get; set; }
        public int? CenterPersonDistinctionId { get; set; }
        public string? ZoomId { get; set; }
        public int? RolId { get; set; }
        public int? AppOperationClaimId { get; set; }
    }
}
