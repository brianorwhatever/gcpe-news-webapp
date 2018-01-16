using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Middleware
{
    public class ExceptionReportingFilter : ActionFilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (System.Diagnostics.Debugger.IsAttached)
                return;

            if (System.Net.IPAddress.IsLoopback(context.HttpContext.Connection.RemoteIpAddress))
                return;

            //MIGRATION: Check custom errors mode from IIS configuration
            //System.Configuration.Configuration configuration = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration("/");
            //System.Web.Configuration.CustomErrorsSection customErrors = (System.Web.Configuration.CustomErrorsSection)configuration.GetSection("system.web/customErrors");
            //if (customErrors.Mode == System.Web.Configuration.CustomErrorsMode.Off)
            //    return;

            // How to: Handle Application-Level Errors
            // https://msdn.microsoft.com/en-us/library/24395wz3(v=vs.140).aspx

            // Error Handling
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/error-handling

            var ex = context.Exception;

            if (ex is InvalidOperationException && ex.Message == "The requested resource can only be accessed via SSL.")
                return;

            //MIGRATION: Determine if 404 errors are included in exception reporting and method to address them
            //HttpException httpEx = ex as HttpException;
            //if (httpEx != null)
            //{
            //    if (httpEx.ErrorCode == (int)System.Net.HttpStatusCode.NotFound)
            //        return;
            //}

            var request = context.HttpContext.Request;

            Program.ReportException(request, ex);
        }
    }
}

