using FluentValidation.Results;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Extensisons
{
    public class ErrorDetails
    {
        public string Message { get; set; }
        public int StatusCode { get; set; }
        public string Errors { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

    }
}
