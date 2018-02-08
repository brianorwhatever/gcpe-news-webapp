using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Extensions;

namespace Gov.News.Website.Middleware
{
    public class RedirectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;
        //private readonly INewslettersClient _newsletters;
        
        public RedirectMiddleware(RequestDelegate next, ILoggerFactory loggerFactory/*, INewslettersClient newsletters*/)
        {
            _next = next;
            _logger = loggerFactory.CreateLogger<RedirectMiddleware>();
            //_newsletters = newsletters;
        }
  
        public async Task Invoke(HttpContext context)
        {
            string host = "";
            if (context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                host = context.Request.Headers["X-Forwarded-For"];
            }

            var url = UriHelper.GetDisplayUrl(context.Request);
            var userAgent = "";
            if (context.Request.Headers.ContainsKey ("User-Agent"))
            {
                userAgent = context.Request.Headers["User-Agent"];
            }

            var remoteIpAddress = context.Connection.RemoteIpAddress;
            
            // As Kestrel does not currently have access logs, those are done here.
            _logger.LogInformation($"{DateTime.UtcNow:HH:mm:ss.fff} {remoteIpAddress} {host} {userAgent} {context.Request.Method} {url}", null);


            Uri newUri = await Redirect(context);
            if (newUri != null)
            {
                _logger.LogInformation("RedirectMiddleware Handling request: " + context.Request.Path);
                context.Response.Redirect(newUri.ToString(), false); // Not permanent
            }
        }

        public async Task<Uri> Redirect(HttpContext context)
        {
            var request = context.Request;
            string host = request.Host.Value.ToLowerInvariant();
            string path = string.Concat(request.PathBase, request.Path).ToLowerInvariant();
            string query = request.QueryString.Value;
            var response = context.Response;

            if (host == "www." + Properties.Settings.Default.NewsHostUri.Host)
            {
                Uri newUri = RedirectFromArchives(path, query);
                if (newUri == null)
                {
                    response.StatusCode = StatusCodes.Status404NotFound;
                }
                return newUri;
            }

            if ("www." + host == Properties.Settings.Default.NewsroomHostUri.Host)
            {
                //Prepend www to newsroom.gov.bc.ca
                //TODO: Change to Permanent redirect
                return new Uri(Properties.Settings.Default.NewsroomHostUri, path + query);
            }

            if (host == Properties.Settings.Default.NewsroomHostUri.Host)
            {
                Uri newUri = RedirectFromNewsroom(path, query);
                if (newUri == null)
                {
                    response.StatusCode = StatusCodes.Status404NotFound;
                }
                return newUri;
            }

            if ("www." + host == Properties.Settings.Default.NewslettersHostUri.Host)
            {
                //Prepend www to enewsletters.gov.bc.ca
                //TODO: Change to Permanent redirect
                return new Uri(Properties.Settings.Default.NewslettersHostUri, path + query);
            }

            if (host == Properties.Settings.Default.NewslettersHostUri.Host)
            {
                Uri newUri = RedirectFromNewsletters(path);
                if (newUri == null)
                {
                    response.StatusCode = StatusCodes.Status404NotFound;
                }
                return newUri;
            }

            /* do not redirect if the request scheme does not match.
             * 
            if (Properties.Settings.Default.NewsHostUri.Scheme == "https" && context.Request.Scheme == "http")
            {
                if (path.EndsWith("/") && path != "/")
                {
                    return new Uri(Properties.Settings.Default.NewsHostUri, path.TrimEnd('/') + query);
                }
                else
                {
                    return new Uri(Properties.Settings.Default.NewsHostUri, path + query);
                }
            }

            */

            if (path.EndsWith("/") && path != "/")
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.TrimEnd('/') + query);
            }

            if (path.StartsWith("/sectors/families"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/sectors/families", "/sectors/services") + query);
            }
            //This shouldn't be here, this is redirecting ministry pages and feeds that no longer exist
            //IE. Justice->Attorney-General
            //Instead all consumers of feeds should be updating their news bars etc. 
            //Aug. 4/2017 after the NDP/Green took government.
            //May you not find this causing a bug 10 years later.
            if (path.StartsWith("/ministries/justice"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/ministries/justice", "/ministries/attorney-general") + query);
            }
            if (path.StartsWith("/ministries/advanced-education") && !path.StartsWith("/ministries/advanced-education-skills")) //Need - otherwise it casues a redirect loop because the names are too similar.
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path .Replace("/ministries/advanced-education", "/ministries/advanced-education-skills-and-training") + query);
            }
            if (path.StartsWith("/ministries/technology-innovation-and-citizens-services"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("technology-innovation-and-citizens-services", "citizens-services") + query);
            }
            if (path.StartsWith("/ministries/energy-and-mines"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/ministries/energy-and-mines", "/ministries/energy-mines-and-petroleum-resources") + query);
            }
            if (path.StartsWith("/ministries/environment") && !path.StartsWith("/ministries/environment-and")) //Need - otherwise it causes a redirect loop because the names are too similar.
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/ministries/environment", "/ministries/environment-and-climate-change-strategy") + query);
            }
            if (path.StartsWith("/ministries/forests-lands-and-natural-resource-operations"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("forests-lands-and-natural-resource-operations", "forests-lands-natural-resource-operations-and-rural-development"));
            }
            if (path.StartsWith("/ministries/aboriginal-relations-and-reconciliation"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("aboriginal-relations-and-reconciliation", "indigenous-relations-and-reconciliation") + query);
            }
            if (path.StartsWith("/ministries/jobs-tourism-and-skills-training"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/jobs-tourism-and-skills-training", "/jobs-trade-and-technology") + query);
            }
            if (path.StartsWith("/ministries/international-trade"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/ministries/international-trade", "/ministries/trade") + query);
            }
            if (path.StartsWith("/ministries/social-development-and-social-innovation"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/social-development-and-social-innovation", "/social-development-and-poverty-reduction") + query);
            }
            if (path.StartsWith("/ministries/community-sport-and-cultural-development"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/community-sport-and-cultural-development", "/tourism-arts-and-culture") + query);
            }

            //End Redirects for renaming ministries.


            if (path.StartsWith("/areas/newsroom/images/social/default-og-meta-image.jpg"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Replace("/areas/newsroom/images/social/default-og-meta-image.jpg", "/Content/Images/Gov/default-og-meta-image-1.jpg") + query);
            }

            /*
            if (host.Split(':')[0] != Properties.Settings.Default.NewsHostUri.Host)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return null;
            }
            */

            Uri configUri = RedirectFromConfig(path, query);
            if (configUri == null)
            {
                await _next.Invoke(context);
            }
            return configUri;
        }

        public static Uri RedirectFromConfig(string path, string query)
        {
            //<location path="ministries/office-of-the-premier">
            //  <system.webServer>
            //    <httpRedirect enabled="true" destination="/office-of-the-premier" httpResponseStatus="Permanent" />
            //  </system.webServer>
            //</location>
            if (path.StartsWith("/ministries/office-of-the-premier"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path.Substring("/ministries".Length) + query);
            }

            //<location path="tags/speeches">
            //  <system.webServer>
            //    <httpRedirect enabled="true" destination="/office-of-the-premier/speeches" httpResponseStatus="Permanent" />
            //  </system.webServer>
            //</location>
            if (path.StartsWith("/tags/speeches"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, "/office-of-the-premier" + path.Substring("/tags".Length));
            }

            //<location path="newsletters/subscribe">
            //  <system.webServer>
            //    <httpRedirect enabled="true" destination="/subscribe?newsletters=" httpResponseStatus="Found" />
            //  </system.webServer>
            //</location>
            if (path == "/newsletters/subscribe")
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, "/subscribe?newsletters=");
            }

            //<location path="files/newsroom/downloads/media_contacts.pdf">
            //  <system.webServer>
            //    <httpRedirect enabled="true" destination="https://news.gov.bc.ca/files/Media_Contacts.pdf" httpResponseStatus="Found" />
            //  </system.webServer>
            //</location>
            if (path == "/files/newsroom/downloads/media_contacts.pdf")
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, "/files/Media_Contacts.pdf");
            }

            return null;
        }

        public static Uri RedirectFromArchives(string path, string query)
        {
            if (path == "/")
            {
                return Properties.Settings.Default.NewsHostUri;
            }

            if (path == "/robots.txt")
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, "robots.txt");
            }

            if (query.StartsWith("/?organisation_obj_id=", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(Properties.Settings.Default.ArchiveHostUri, path + query);
            }

            if (path == "/rss.xml")
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, "feed");
            }

            if (path.StartsWith("/rss/"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, "connect#rss");
            }

            if (path == "/default.aspx")
            {
                return new Uri(Properties.Settings.Default.ArchiveHostUri, path + query);
            }

            if (path == "/list.aspx")
            {
                if (query.Equals("?action=organisation", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(Properties.Settings.Default.NewsHostUri, "ministries");
                }

                if (query.Equals("?action=topics", StringComparison.OrdinalIgnoreCase))
                {
                    return new Uri(Properties.Settings.Default.NewsHostUri, "sectors");
                }

                return new Uri(Properties.Settings.Default.ArchiveHostUri, path + query);
            }

            if (path.StartsWith("/archive/2005-2009/"))
            {
                return new Uri(Properties.Settings.Default.ArchiveHostUri, path + query);
            }

            if (path.StartsWith("/archive/2001-2005/"))
            {
                return new Uri(Properties.Settings.Default.ArchiveHostUri, path + query);
            }

            if (path.StartsWith("/archive/pre2001/"))
            {
                return new Uri(Properties.Settings.Default.ArchiveHostUri, path + query);
            }

            if (path == "/archive" || path.StartsWith("/archive/"))
            {
                return new Uri(Properties.Settings.Default.NewsHostUri, path + query);
            }

            return null;
        }


        public static Uri RedirectFromNewsroom(string path, string query)
        {
            string ministryFactsheetMonthPattern = @"^/ministries/\S+/factsheets/\d{4}/\d{2}";
            string ministryStoriesMonthPattern = @"^/ministries(/[^/]+){1,2}/\d{4}/\d{2}";
            string sectorStoriesMonthPattern = @"^/sectors/[^/]+/\d{4}/\d{2}";
            string regionStoriesMonthPattern = @"^/regions/[^/]+/\d{4}/\d{2}";
            string rootStoriesMonthPattern = @"^/\d{4}/\d{2}/{0,1}$";

            string factsheetPattern = @"^/ministries/\S+/factsheets/\S+.html";
            string storyPattern = @"^/\d{4}/\d{2}/\S+.html$";

            string ministryIndexPattern = @"^/ministries(/[^/]+){1,2}/{0,1}$";

            string sectorIndexPattern = @"^/sectors/[^/]+/{0,1}$";
            string regionIndexPattern = @"^/regions/[^/]+/{0,1}$";

            IDictionary<string, string> redirectMappings = new Dictionary<string, string>();
            redirectMappings.Add("/ministries/labour-citizens-services-and-open-government",
                                 "/ministries/citizens-services");
            redirectMappings.Add("/ministries/justice",
                                 "/ministries/attorney-general");
            redirectMappings.Add("/ministries/jobs-tourism-and-skills-training",
                                 "/ministries/jobs-trade-and-technology");
            redirectMappings.Add("/ministries/energy-and-mines",
                                 "/ministries/energy-mines-and-petroleum-resources");
            redirectMappings.Add("/ministries/advanced-education",
                                 "/ministries/advanced-education-skills-and-training");
            redirectMappings.Add("/ministries/environment",
                                 "/ministries/environment-and-climate-change-strategy");
            redirectMappings.Add("/ministries/intergovernmental-relations-secretariat-1",
                                 "/ministries/intergovernmental-relations-secretariat");
            redirectMappings.Add("/ministries/social-development-social-innovation",
                                 "/ministries/social-development-and-poverty-reduction");
            redirectMappings.Add("/ministries/social-development-social-and-social-innovation",
                                 "/ministries/social-development-and-poverty-reduction");
            redirectMappings.Add("/ministries/technology-innovation-and-citizens-services",
                                 "/ministries/citizens-services");
            redirectMappings.Add("/ministries/jobs-tourism-and-skills-training/minister-of-state-for-tourism-and-small-business",
                                 "/ministries/tourism-arts-and-culture");
            redirectMappings.Add("/ministries/jobs-tourism-and-skills-training/state-small-business",
                                 "/ministries/tourism-arts-and-culture");
            redirectMappings.Add("/ministries/forests-lands-and-natural-resource-operations",
                                 "/ministries/forests-lands-natural-resource-operations-and-rural-development");
            redirectMappings.Add("/ministries/aboriginal-relations-and-reconciliation",
                                 "/ministries/indigenous-relations-and-reconciliation");
            redirectMappings.Add("/ministries/international-trade",
                                 "/ministries/trade");
            redirectMappings.Add("/ministries/community-sport-and-cultural-development",
                                 "/ministries/tourism-arts-and-culture");
            redirectMappings.Add("/ministries/natural-gas-development",
                                 "/ministries/energy-mines-and-petroleum-resources");

            redirectMappings.Add("/regions/vancouver-island-coast",
                                "/regions/vancouver-island-and-coast");
            redirectMappings.Add("/regions/vancouver-coast-mountains",
                                "/regions/vancouver-coast-and-mountains");

            Uri newsHostUri = Properties.Settings.Default.NewsHostUri;

            if (string.IsNullOrEmpty(path) || path == "/" || path == "/?")
            {
                return new Uri(newsHostUri, query);
            }

            //Replace the renamed ministries
            foreach (string key in redirectMappings.Keys)
            {
                if (path.IndexOf(key) >= 0)
                {
                    string newName = key;
                    redirectMappings.TryGetValue(key, out newName);
                    path = path.Replace(key, newName);
                    break;
                }
            }

            if (path.EndsWith("/index.html"))
            {
                path = path.Replace("/index.html", "/");
            }

            Match ministryIndexMatch = Regex.Match(path, ministryIndexPattern, RegexOptions.IgnoreCase);
            Match sectorIndexMatch = Regex.Match(path, sectorIndexPattern, RegexOptions.IgnoreCase);
            Match regionIndexMatch = Regex.Match(path, regionIndexPattern, RegexOptions.IgnoreCase);
            Match ministryStoriesMonthMatch = Regex.Match(path, ministryStoriesMonthPattern, RegexOptions.IgnoreCase);
            Match regionStoriesMonthMatch = Regex.Match(path, regionStoriesMonthPattern, RegexOptions.IgnoreCase);
            Match sectorStoriesMonthMatch = Regex.Match(path, sectorStoriesMonthPattern, RegexOptions.IgnoreCase);
            Match rootStoriesMonthMatch = Regex.Match(path, rootStoriesMonthPattern, RegexOptions.IgnoreCase);

            if (path.EndsWith("/atom.xml"))
            {
                string newPath = path.Replace("atom.xml", "feed/atom");

                if (path.Contains("/tag/"))
                {
                    newPath = newPath.Replace("/tag/", "/tags/");

                    if (path.Contains("/blue/"))
                        newPath = newPath.Replace("/blue/", "/blueprint/");

                    if (path.Contains("/familiesfirst/"))
                        newPath = newPath.Replace("/familiesfirst/", "/families-first/");
                }

                return new Uri(newsHostUri, newPath + query);
            }

            if (path.EndsWith("/more-news.html"))
            {
                string newPath = Regex.Replace(path, "/more-news.html", "/more-news", RegexOptions.IgnoreCase);

                return new Uri(newsHostUri, newPath + query);
            }

            if (path.EndsWith("/archive.html"))
            {
                string newPath = Regex.Replace(path, "/archive.html", "/archive", RegexOptions.IgnoreCase);

                return new Uri(newsHostUri, newPath + query);
            }

            if (path.EndsWith("/connect.html"))
            {
                return new Uri(newsHostUri, "connect");
            }

            if (path.EndsWith("/iframe.html"))
            {
                string newPath = Regex.Replace(path, "/iframe.html", "/embed", RegexOptions.IgnoreCase);

                if (path.Contains("/tag/"))
                {
                    newPath = newPath.Replace("/tag/", "/tags/");

                    if (path.Contains("/blue/"))
                        newPath = newPath.Replace("/blue/", "/blueprint/");

                    if (path.Contains("/familiesfirst/"))
                        newPath = newPath.Replace("/familiesfirst/", "/families-first/");
                }

                return new Uri(newsHostUri, newPath);
            }

            if (path.EndsWith("/factsheets") || path.EndsWith("/factsheets/")
                || path.EndsWith("/speeches") || path.EndsWith("/speeches/")
                || path.EndsWith("/ministries") || path.EndsWith("/ministries/")
                || path.EndsWith("/sectors") || path.EndsWith("/sectors/")
                || path.EndsWith("/regions") || path.EndsWith("/regions/")
                || sectorIndexMatch.Success || regionIndexMatch.Success)
            {
                return new Uri(newsHostUri, path.TrimEnd('/') + query);
            }

            if (path.Contains("/biography/"))
            {
                return new Uri(newsHostUri, path.TrimEnd('/') + query);
            }

            if (ministryStoriesMonthMatch.Success || sectorStoriesMonthMatch.Success
                    || regionStoriesMonthMatch.Success || rootStoriesMonthMatch.Success)
            {
                Match archiveMatch = Regex.Match(path, @"/\d{4}/\d{2}");

                string newPath = path.Insert(archiveMatch.Index, "/archive");

                return new Uri(newsHostUri, newPath.TrimEnd('/') + query);
            }

            if (path.StartsWith("/files/"))
            {
                return new Uri(newsHostUri, path.TrimEnd('/') + query);
            }

            if (path.StartsWith("/images/") || path.StartsWith("/downloads/") || path.StartsWith("/factsheets/downloads/"))
            {
                return new Uri(newsHostUri, "/files/Newsroom" + path.TrimEnd('/') + query);
            }

            if ((path.LastIndexOf('/') == 0 || path.StartsWith("/factsheets/")) && (path.EndsWith(".pdf") || path.EndsWith(".jpg") || path.EndsWith(".gif") || path.EndsWith(".docx") || path.EndsWith(".mp3") || path.EndsWith(".png")))
            {
                return new Uri(newsHostUri, "/files/Newsroom" + path.TrimEnd('/') + query);
            }

            if (path.Contains("/factsheets/"))
            {
                Match factsheetsMatch = Regex.Match(path, ministryFactsheetMonthPattern, RegexOptions.IgnoreCase);

                if (factsheetsMatch.Success)
                {
                    string newPath = path.Replace("/factsheets/", "/factsheets/archive/");

                    return new Uri(newsHostUri, newPath.TrimEnd('/') + query);
                }
                else
                {
                    factsheetsMatch = Regex.Match(path, factsheetPattern, RegexOptions.IgnoreCase);

                    if (factsheetsMatch.Success)
                    {
                        int lastIndex = path.LastIndexOf("/");
                        string key = path.Substring(lastIndex + 1).Replace(".html", "");

                        return new Uri(newsHostUri, "factsheets/" + key + query);
                    }
                }
            }

            if (ministryIndexMatch.Success)
            {
                return new Uri(newsHostUri, path.TrimEnd('/') + query);
            }

            if (path == "/search/")
            {
                if (query.StartsWith("?q=", StringComparison.OrdinalIgnoreCase))
                {
                    int resultsPageNum = query.IndexOf("&resultspagenum=", StringComparison.OrdinalIgnoreCase);

                    string newQuery = query;
                    if (resultsPageNum > -1)
                        newQuery = newQuery.Substring(0, resultsPageNum);

                    return new Uri(newsHostUri, "search" + newQuery);
                }
                else
                {
                    return new Uri(newsHostUri, path.TrimEnd('/') + query);
                }
            }


            if (path == "/subscribe.aspx" && query.StartsWith("?guid=", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(newsHostUri, (path + query).Replace(".aspx?guid", "/manage?token"));
            }

            if (path.StartsWith("/subscribe"))
            {
                return new Uri(newsHostUri, "subscribe");
            }

            if (path == "/enewsletters/subscribe.aspx" && query.StartsWith("?dlid", StringComparison.OrdinalIgnoreCase))
            {
                var redirectNewsletterSubscriptionMappings = new Dictionary<string, string>()
                {
                    { "?dlid=83", "/subscribe?newsletters=&newsletters=seniorsbcca" },
                    { "?dlid=115", "/subscribe?newsletters=&newsletters=aboriginal-healthy-living" },
                    { "?dlid=132", "/subscribe?newsletters=&newsletters=update-from-minister-thomson" },
                    { "?dlid=146", "/subscribe?newsletters=&newsletters=bc-jobs-plan-e-newsletter" },
                    { "?dlid=149", "/subscribe?newsletters=&newsletters=emergency-medical-assistants-licensing-bulletin" },
                    { "?dlid=150", "/subscribe?newsletters=&newsletters=govtogether-e-newsletter" },
                    { "?dlid=165", "/subscribe?newsletters=&newsletters=lng-in-bc" },
                    { "?dlid=172", "/subscribe?newsletters=&newsletters=education-focus-on-careers-e-newsletter" },
                    { "?dlid=174", "/subscribe?newsletters=&newsletters=bc-stats-infoline" },
                    { "?dlid=1197", "/subscribe?newsletters=&newsletters=bcs-skills-for-jobs-blueprint-enewsletter" },
                    { "?dlid=1207", "/subscribe?newsletters=&newsletters=education-service-delivery-project" },
                    { "?dlid=1237", "/subscribe?newsletters=&newsletters=bc-market-monitor" }
                };
                string pathAndQuery = path + query;

                foreach (string key in redirectNewsletterSubscriptionMappings.Keys)
                {
                    if (query.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string newName = redirectNewsletterSubscriptionMappings[key];
                        pathAndQuery = query.Replace(key, newName);
                        break;
                    }
                }

                return new Uri(newsHostUri, pathAndQuery);
            }

            if (path.StartsWith("/enewsletters/subscribe.aspx"))
            {
                return new Uri(newsHostUri, "/subscribe?newsletters=");
            }

            if (path.StartsWith("/enewsletters/unsubscribe.aspx"))
            {
                return new Uri(newsHostUri, "/subscribe/manage");
            }

            if (path == "/enewsletters/newsletter.aspx" && query.StartsWith("?nid", StringComparison.OrdinalIgnoreCase))
            {
                IDictionary<string, string> redirectNewsletterMappings = new Dictionary<string, string>();
                redirectNewsletterMappings.Add("?nid=44", "/seniorsbcca");
                redirectNewsletterMappings.Add("?nid=59", "/aboriginal-healthy-living");
                redirectNewsletterMappings.Add("?nid=70", "/update-from-minister-thomson");
                redirectNewsletterMappings.Add("?nid=75", "/bc-jobs-plan-e-newsletter");
                redirectNewsletterMappings.Add("?nid=77", "/emergency-medical-assistants-licensing-bulletin");
                redirectNewsletterMappings.Add("?nid=78", "/govtogether-e-newsletter");
                redirectNewsletterMappings.Add("?nid=86", "/lng-in-bc");
                redirectNewsletterMappings.Add("?nid=88", "/education-focus-on-careers-e-newsletter");
                redirectNewsletterMappings.Add("?nid=89", "/bc-stats-infoline");
                redirectNewsletterMappings.Add("?nid=99", "/bcs-skills-for-jobs-blueprint-enewsletter");
                redirectNewsletterMappings.Add("?nid=106", "/education-service-delivery-project");
                redirectNewsletterMappings.Add("?nid=114", "/bc-market-monitor");

                string pathAndQuery = path + query;
                foreach (string key in redirectNewsletterMappings.Keys)
                {
                    if (query.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string newName = redirectNewsletterMappings[key];
                        pathAndQuery = query.Replace(key, newName);
                        break;
                    }
                }

                return new Uri(newsHostUri, "/newsletters" + pathAndQuery);
            }

            if (path.StartsWith("/enewsletters"))
            {
                return new Uri(newsHostUri, "newsletters");
            }

            Match match = Regex.Match(path, storyPattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                int lastIndex = path.LastIndexOf("/");
                string key = path.Substring(lastIndex + 1).Replace(".html", "");

                return new Uri(newsHostUri, "stories/" + key + query);
            }

            return null;
        }

        public Uri RedirectFromNewsletters(string path)
        {
            Uri newsHostUri = Properties.Settings.Default.NewsHostUri;

            //Redirects from www.enewsletters.gov.bc.ca to www.newsroom.gov.bc.ca taken from Gov.ENewsletters.Website\Web.config.
            var legacyMappings = new Dictionary<string, string>()
            {
                { "/", "newsletters" },
                { "/newsletterlistings.aspx", "newsletters" },
                { "/default.aspx", "newsletters" },
                { "/subscriber.aspx", "subscribe?newsletters=" },
                { "/subscribeForm.aspx", "subscribe?newsletters=" },
                { "/subscribe.aspx", "subscribe?newsletters=" },
                { "/subscribe_process.aspx", "subscribe?newsletters=" },
                { "/subscribeprocessform.aspx", "subscribe?newsletters=" },
                { "/unsubscribeform.aspx", "subscribe/manage" },
                { "/unsubscribe.aspx", "subscribe/manage" },
                { "/unsubscribe_process.aspx", "subscribe/manage" },
                { "/unsubscribeprocessform.aspx", "subscribe/manage" }
            };

            if (legacyMappings.ContainsKey(path))
            {
                return new Uri(newsHostUri, legacyMappings[path]);
            }

            if (path.Contains("/edition"))
            {
                /*  TODO - replace this with API service calls.
                 * var client = _newsletters.Newsletters;

                var url = client.GetNewsUrlFromNewslettersUrl(path);
                if (url != null)
                {
                    return new Uri(newsHostUri, url);
                }
                */
            }

            //TODO: Redirects for Newsletters, Articles, Images, and Files.
            return null;
        }
    }

    public static class RedirectMiddlewareExtensions
    {
        const bool IsPermanent = false;

        public static IApplicationBuilder UseRedirect(this IApplicationBuilder app)
        {
            
#if !DEBUG
            // Disable the HTTS redirect so that the app will run in OpenShift.
            /*
            var options = new RewriteOptions();

            options = IsPermanent ? options.AddRedirectToHttpsPermanent() : options.AddRedirectToHttps();

            app.UseRewriter(options);
            */
#endif

            return app.UseMiddleware<RedirectMiddleware>();
        }
    }
}