using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Middleware
{

    public class ValidateRefererAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var refererHeader = filterContext.HttpContext.Request.Headers["Referer"].Single();
            Uri uri;
            if (string.IsNullOrEmpty(refererHeader) ||
                !Uri.TryCreate(refererHeader, UriKind.Absolute, out uri) ||
                uri.Host != Properties.Settings.Default.NewsHostUri.Host)
            {
                filterContext.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            }

            base.OnActionExecuting(filterContext);
        }
    }
}