using Microsoft.AspNetCore.Builder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#if USE_SIGNALR
using Microsoft.Owin.Builder;
using Owin;
#endif

namespace Gov.News.Website
{
    public static class SignalRExtensions
    {
        public static void UseSignalR(this IApplicationBuilder app)
        {
#if USE_SIGNALR
            app.UseOwin(pipeline =>
            {
                pipeline(next =>
                {
                    var appBuilder = new AppBuilder();

                    appBuilder.Properties["builder.DefaultApp"] = next;

                    appBuilder.MapSignalR();

                    return appBuilder.Build();
                });
            });
#endif
        }
    }
}