using System;
using System.IO;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

#if EXCEPTION_REPORTING_ENABLED
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
#endif


namespace Gov.News.Website
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseKestrel(options => options.AddServerHeader = false)
                .UseHealthChecks("/hc")
                .UseContentRoot(Directory.GetCurrentDirectory())
#if DEBUG
                .UseUrls("http://localhost:53488/")
#endif
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();
    }
}
