using System;
using Gov.News.WebApp;
using Gov.News.Website.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gov.News.Website
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            if (!System.Diagnostics.Debugger.IsAttached)
                builder.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);
            builder.AddEnvironmentVariables();

            if (env.IsDevelopment())
            {
            }

            Configuration = builder.Build();

            Configuration.Bind(Properties.Settings.Default);

            //Data.Repository.RepositoryException += (ex) => Program.ReportException(null, ex);
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.

            services.AddMemoryCache();

            services.AddMvc().AddMvcOptions(options =>
            {
#if DEBUG
                var cacheProfile = new CacheProfile { Location = ResponseCacheLocation.None, NoStore = true };
#else
                var cacheProfile = new CacheProfile { Duration = 60 };
#endif

                options.CacheProfiles.Add("Default", cacheProfile);
                options.CacheProfiles.Add("Feed", cacheProfile);
                options.CacheProfiles.Add("Embed", cacheProfile);
                options.CacheProfiles.Add("Page", cacheProfile);
                options.CacheProfiles.Add("Archive", cacheProfile);

                options.Filters.Add(new TypeFilterAttribute(typeof(XFrameOptionsAttribute)));

                options.Filters.Add(new TypeFilterAttribute(typeof(RequirePermanentHttpsAttribute)));

                options.Filters.Add(new ExceptionReportingFilter());
            });

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        
            //TODO: Change to ServiceLifetime.Scoped once repository is no longer using static methods
            var entityLoggerFactory = new LoggerFactory().AddDebug();
            
            
            services.AddSingleton(new Func<IServiceProvider, Gov.News.Api.IClient>((serviceProvider) =>
            {
                var client = new Gov.News.Api.Client();
                client.BaseUri = new Uri(Configuration["NewsApi"]);
                return client;
            }));

            

            /*
            services.AddSingleton(new Func<IServiceProvider, Gcpe.Hub.Services.Legacy.INewslettersClient>((serviceProvider) =>
            {
                var client = new Gcpe.Hub.Services.Legacy.NewslettersClient();
                client.BaseUri = new Uri(Configuration.GetConnectionString("HubNewslettersClient"));
                return client;
            }));


            services.Configure<Data.RepositoryOptions>(Configuration.GetSection("Options:Gov.News.Data:Repository"));
                */

            
            services.AddSingleton<Repository, Repository>();
            services.AddSingleton<IHostedService, Hubs.LiveHub>();

            // Add the Configuration object so that controllers may use it through dependency injection
            services.AddSingleton<IConfiguration>(Configuration);

            services.Replace(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(TimestampLogger<>)));

            // add a health check for the news api service.
            services.AddHealthChecks(checks =>
            {
                checks.AddUrlCheck(Configuration["NewsApi"] + "/hc", new TimeSpan (0, 1, 0 )); // check the news client connection every minute.
            });
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureDevelopmentServices(IServiceCollection services)
        {
            ConfigureServices(services);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/error");
            }

            app.UseRedirect();

            app.UseStaticFiles();
            //TODO: Implement caching of static files
            //<staticContent>
            //  <clientCache cacheControlCustom="public" cacheControlMaxAge="1.00:00:00" cacheControlMode="UseMaxAge" />
            //</staticContent>

            if ((Properties.Settings.Default.SignalREnabled != null && Properties.Settings.Default.SignalREnabled.ToLower().Equals("true")))
            {
                app.UseSignalR();
            }

            app.UseMvc(routes =>
            {
                routes.RegisterRoutes();
            });
        }
    }
}