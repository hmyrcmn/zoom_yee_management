using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Toplanti.Core.Extensisons
{
    public class ExceptionMiddleware
    {
        private RequestDelegate _next;
        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception e)
            {
                await HandleExceptionAsync(httpContext, e);
            }
        }

        private Task HandleExceptionAsync(HttpContext httpContext, Exception exception)
        {
            httpContext.Response.ContentType = "application/json";
            httpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            string message = "Internal Server Error";


            if (exception.GetType() == typeof(UnauthorizedAccessException))
            {
                message = exception.Message;
                httpContext.Response.StatusCode = 401;

                return httpContext.Response.WriteAsync(new ErrorDetails
                {
                    StatusCode = 401,
                    Message = message,
                }.ToString());
            }

            if (exception.GetType() == typeof(ValidationException))
            {
                IEnumerable<ValidationFailure> errors;

                message = exception.Message;
                errors = ((ValidationException)exception).Errors;
                httpContext.Response.StatusCode = 400;

                return httpContext.Response.WriteAsync(new ValidationErrorDetails
                {
                    StatusCode = 400,
                    Message = message,
                    Errors = errors
                }.ToString());
            }

            return httpContext.Response.WriteAsync(new ErrorDetails
            {
                StatusCode = httpContext.Response.StatusCode,
                Message = message,
                Errors = message
            }.ToString());
        }

    }
}
