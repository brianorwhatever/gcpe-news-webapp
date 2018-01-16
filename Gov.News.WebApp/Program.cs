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
        

        public static void ReportException(HttpRequest request, Exception ex)
        {
#if EXCEPTION_REPORTING_ENABLED
            try
            {
                if (!Properties.Settings.Default.EnableExceptionReporting)
                    return;

                string report = "";

                if (request == null)
                {
                    //TEST: If this will work on .NET Core.  It did not work in NewsroomExtensions.
                    /*
                    var context = new Microsoft.AspNetCore.Http.HttpContextAccessor().HttpContext;

                    if (context != null)
                        request = context.Request;
                    */
                }

                if (request != null)
                {
                    report += "\r\n" + "\r\n" + "Request: " + request.Path;
                    report += "\r\n" + "UrlReferrer: " + request.Headers["Referer"];

                    report += "\r\n" + "\r\n" + "User Address: " + request.HttpContext.Connection.RemoteIpAddress;
                    report += "\r\n" + "User Agent: " + request.Headers["User-Agent"];

                    if (request.HttpContext.User.Identity != null)
                        report += "\r\n" + "User Identity: " + request.HttpContext.User.Identity.Name;
                }

                report += "\r\n" + "\r\n" + "Machine Name: " + Environment.MachineName;
                report += "\r\n" + "\r\n" + "Type: " + ex.GetType().FullName;

                report += "\r\n" + "\r\n" + "Exception:" + "\r\n" + ex.ToString();

                report += "\r\n" + "\r\n" + "Caller:" + "\r\n" + new System.Diagnostics.StackTrace().ToString();

                if (ex.Data.Count > 0)
                {
                    report += "\r\n" + "\r\n" + "Data:";

                    foreach (var key in ex.Data.Keys)
                        report += "\r\n  " + key + ": " + ex.Data[key];
                }

                report = report.Trim();

                using (System.Net.Mail.SmtpClient client = new System.Net.Mail.SmtpClient())
                {
                    System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();

                    message.From = new System.Net.Mail.MailAddress("gcpe.servicedesk@gov.bc.ca", "GCPE Service Desk GCPE:EX");
                    message.To.Add("JohnMichael.Bird@gov.bc.ca");

                    //message.Subject = "Exception Report from " + request.Host System.Web.Hosting.HostingEnvironment.ApplicationHost.GetSiteName();
                    message.Subject = "Exception Report from " + request.Host.ToString().ToLower();

                    message.IsBodyHtml = false;
                    message.Body = report;

                    client.Send(message);
                }
            }
            catch
            {
            }
#endif
        }
    }
}
