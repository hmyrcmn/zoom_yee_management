using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Entities.DTOs
{
    public class ZoomCreateUserRequest
    {
        public string action { get; set; }
        public ZoomUserCreatedResponse user_info { get; set; }
    }
}
