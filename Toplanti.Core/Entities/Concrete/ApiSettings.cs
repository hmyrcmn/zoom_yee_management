using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Entities.Concrete
{
    public class ApiSettings
    {
        public string BaseAdress { get; set; }
        public string BaseAdressName { get; set; }
        public string DefaultRequestHeadersName { get; set; }
        public string DefaultRequestHeadersValue { get; set; }
    }
}
