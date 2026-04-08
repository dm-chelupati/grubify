using System.Net;
using System.Text.Json;

namespace GrubifyApi.Middleware
{
    public class GlobalExceptionHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (OutOfMemoryException ex)
            {
                _logger.LogError(ex, "Out of memory exception occurred");
                await HandleExceptionAsync(context, HttpStatusCode.ServiceUnavailable, 
                    "Service temporarily unavailable due to high load. Please try again later.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred");
                await HandleExceptionAsync(context, HttpStatusCode.InternalServerError, 
                    "An error occurred processing your request.");
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, HttpStatusCode statusCode, string message)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var response = new
            {
                statusCode = (int)statusCode,
                message = message
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
