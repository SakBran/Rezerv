using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rezerv.Application.Exceptions;

namespace Rezerv.API.Middleware
{
    public class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await next(ctx);
            }
            catch (BusinessRuleException ex)
            {
                ctx.Response.StatusCode = ex.StatusCode;
                await ctx.Response.WriteAsJsonAsync(new { code = ex.Code, message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled");
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "Unexpected error" });
            }
        }
    }
}