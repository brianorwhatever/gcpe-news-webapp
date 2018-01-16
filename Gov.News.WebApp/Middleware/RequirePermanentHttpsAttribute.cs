using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gov.News.Website.Middleware
{
    public class RequirePermanentHttpsAttribute : ActionFilterAttribute // RequireHttpsAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            if (filterContext.HttpContext.Request.IsHttps)
            {
                filterContext.HttpContext.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
            }
        }

        //public override void OnAuthorization(AuthorizationFilterContext filterContext)
        //{
        //    base.OnAuthorization(filterContext);

        //    if (filterContext.HttpContext.Request.IsHttps)
        //    {
        //        filterContext.HttpContext.Response.Headers["Strict-Transport-Security"] = "max-age=31536000";
        //    }
        //    else
        //    {
        //        var request = filterContext.HttpContext.Request;

        //        string url = string.Concat(Uri.UriSchemeHttps, Uri.SchemeDelimiter, request.Host, request.PathBase, request.Path, request.QueryString);
        //        filterContext.Result = new RedirectResult(url, true);
        //    }
        //}

        //Redirect HTTP to HTTPS using ASP.NET Core RC2
        //https://github.com/aspnet/KestrelHttpServer/issues/916

        //        Microsoft.Web.Administration.ConfigurationSection sitesSection = Microsoft.Web.Administration.WebConfigurationManager.GetSection(null, null, "system.applicationHost/sites");

        //            foreach (Microsoft.Web.Administration.ConfigurationElement site in sitesSection.GetCollection())
        //            {
        //                string siteName = System.Web.Hosting.HostingEnvironment.SiteName;

        //                if (String.Equals((string)site["name"], siteName, StringComparison.OrdinalIgnoreCase))
        //                {
        //                    foreach (Microsoft.Web.Administration.ConfigurationElement binding in site.GetCollection("bindings"))
        //                    {
        //                        string protocol = (string)binding["protocol"];

        //                        if (protocol.StartsWith("https", StringComparison.OrdinalIgnoreCase))
        //                        {
        //                            GlobalFilters.Filters.Add(new Attributes.RequirePermanentHttpsAttribute());
        //                            RequiresHttps = true;
        //                            return;
        //                        }
        //                    }
        //                }
        //            }
    }
}