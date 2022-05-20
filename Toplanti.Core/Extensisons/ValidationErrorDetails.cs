using FluentValidation.Results;
using System.Collections.Generic;

namespace Core.Extensisons
{
    public class ValidationErrorDetails : ErrorDetails
    {
        public IEnumerable<ValidationFailure> Errors { get; set; }
    }
}
