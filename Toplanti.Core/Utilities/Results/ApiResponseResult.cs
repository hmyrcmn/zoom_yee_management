using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Utilities.Results
{
    public class ApiResponseResult<T>
    {
        public bool success = false;
        public string error = null;
        public T Data
        {
            get;
            set;
        }
    }
}
