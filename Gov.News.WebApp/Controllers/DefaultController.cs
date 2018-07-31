using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gov.News.Api;
using Gov.News.Api.Models;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Link = Gov.News.Website.Models.ConnectViewModel.ExternalConnectLink;

namespace Gov.News.Website.Controllers
{
    public class DefaultController : Shared.IndexController<Home>
    {
        private readonly IHostingEnvironment _env;

        public DefaultController(Repository repository, IConfiguration configuration, IHostingEnvironment env) : base(repository, configuration)
        {
            _env = env;
        }

        [ResponseCache(CacheProfileName = "Default"), Noarchive]
        public async Task<ActionResult> Reference(string reference)
        {
            if (!Regex.IsMatch(reference, @"\d+"))
                throw new ArgumentException();

            var pair = await Repository.ApiClient.Posts.GetKeyFromReferenceAsync(string.Format("NEWS-{0}", reference), Repository.APIVersion);
            if (pair == null)
                return await NotFound();

            return Redirect(NewsroomExtensions.GetPostUri(pair.Value, pair.Key).ToString());
        }

        [ResponseCache(CacheProfileName = "Default"), Noarchive]
        public async Task<ActionResult> Index(string postKind)
        {
            var model = await GetHomePosts(postKind);

            if (model == null)
                return await SearchNotFound();

            ViewBag.GoogleSiteVerification = Properties.Settings.Default.GoogleSiteVerification;
            ViewBag.BingSiteVerification = Properties.Settings.Default.BingSiteVerification;

            return View("HomeView", model);
        }

        [Route("robots.txt")]
        public ContentResult DynamicRobotsFile()
        {
            StringBuilder content = new StringBuilder();

            if (!_env.IsProduction())
            {
                content.AppendLine("user-agent: *");
                content.AppendLine("Disallow: /");
            }

            return this.Content(content.ToString(), "text/plain", Encoding.UTF8);
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> Top(string type, string format)
        {
            if (!string.Equals(type, "feed", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            var topKeys = IndexModel.GetTopPostKeys(await GetAllCategories());

            var model = await GetSyndicationFeedViewModel("Top Stories", topKeys);
            return await GetNewsFeedContent(format, model, false, false);
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> Feature(string type, string format)
        {
            if (!string.Equals(type, "feed", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            var featureKeys = IndexModel.GetFeaturePostKeys(await GetAllCategories());

            var model = await GetSyndicationFeedViewModel("Featured Stories", featureKeys);
            return await GetNewsFeedContent(format, model, false, false);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Connect()
        {
            var model = await GetConnectModel();
            return View("ConnectView", model);
        }

        [Noindex]
        public async Task<ActionResult> Search(string q = null, string ministry = null, string sector = null, string content = null, string daterange = null, int first = 0)
        {
            var model = await Search(new SearchViewModel.SearchQuery()
            {
                Text = q,
                Ministry = ministry,
                Sector = sector,
                DateRange = daterange,
                NewsType = content
            }, first);

            return View("SearchView", model);
        }

        public new Task<ActionResult> NotFound()
        {
            return SearchNotFound();
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Sitemap()
        {
            List<Uri> model = new List<Uri>();

            var defaultPostKeys = await Repository.ApiClient.Posts.GetAllKeysAsync("home", "default", Repository.APIVersion);

            foreach (var pair in defaultPostKeys)
            {
                model.Add(NewsroomExtensions.GetPostUri(pair.Value, pair.Key));
            }

            return View("SitemapView", model);
        }

        public async Task<ActionResult> CarouselImage(string slideId)
        {
            var slide = await Repository.GetSlideAsync(slideId);

            return GetCachedImage(slide?.Image, slide?.ImageType, slide?.Timestamp, null);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Privacy()
        {
            var model = await GetBaseModel();
            model.Title = "Privacy";
            return View("PrivacyView", model);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Live()
        {
            var model = await GetBaseModel();
            model.Title = "Live";
            return View("LiveView", model);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public ActionResult SiteStatus(bool? showErrors)
        {
            List<string> model = new List<string>();

            model.Add(SiteStatusString("Subscribe API call: ", showErrors, () =>
            {
                IList<KeyValuePair2> tags = Repository.ApiClient.Subscribe.SubscriptionItemsAsync("tags", Repository.APIVersion).Result;
                return tags.Count() > 0 ? "OK" : "Failed";
            }));

            model.Add(SiteStatusString("Newsletters count:  ", showErrors, () =>
            {
                IEnumerable<Newsletter> newsletters = Repository.GetNewslettersAsync().Result;
                return newsletters.Count().ToString();
            }));

            Post post = null;
            model.Add(SiteStatusString("Hub DB access, post key: ", showErrors, () =>
            {
                post = Repository.GetPostAsync("2017FLNR0208-001391").Result;
                return post.Key;
            }));

            model.Add(SiteStatusString("Media proxy url: ", showErrors, () =>
            {
                var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Referrer = new Uri(string.Concat(Request.Scheme, "://", Request.Host.ToUriComponent(), Request.PathBase.ToUriComponent(), Request.Path, Request.QueryString));

                var result = client.GetAsync(post.AssetUrl).Result.ReasonPhrase;

                if (result != "OK") throw new Exception(result);
                return "OK";
            }));

            model.Add(SiteStatusString("Post cache size: ", showErrors, () =>
            {
                return Repository._cache[typeof(Post)].Count().ToString();
            }));

            model.Add(SiteStatusString("Facebook Post cache size: ", showErrors, () =>
            {
                return Repository._cache[typeof(FacebookPost)].Count().ToString();
            }));

            return View("SiteStatus", model);
        }
        public static string SiteStatusString(string s, bool? showErrors, Func<string> func)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

            string value;
            try
            {
                value = func();
                s = "OK: " + s;
            }
            catch (Exception ex)
            {
                if (showErrors == false) throw ex;
                Exception inner = ex.InnerException;
                if (inner == null) value = ex.Message;
                else value = (inner.InnerException ?? inner).Message;
            }

            timer.Stop();
            s += value + " (" + (timer.ElapsedMilliseconds) + " ms)";

            return s;
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Contacts()
        {
            var model = await GetBaseModel();
            model.Title = "Media Contacts";
            return View("CommContacts", model);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public ActionResult Contact()
        {
            return Redirect(Properties.Settings.Default.ContactUri.ToString());
        }

        public ActionResult Error()
        {
            var exception = HttpContext.Features.Get<IExceptionHandlerFeature>();

            ViewData["statusCode"] = HttpContext.Response.StatusCode;
            ViewData["message"] = exception.Error.Message;
            ViewData["stackTrace"] = exception.Error.StackTrace;

            return View("ErrorView");
        }

        public async Task<ConnectViewModel> GetConnectModel()
        {
            var model = new ConnectViewModel();
            await LoadAsync(model);

            model.FacebookLinks = new Link[]
            {
                        new Link() { Url = "http://www.facebook.com/BCProvincialGovernment", Title = "Government of British Columbia", Summary = "Join us for BC news, information and updates" },
                        new Link() { Url = "http://www.facebook.com/BCJobsPlan" , Title = "BC Jobs Plan" , Summary = "Get updates on 'Canada Starts Here: The BC Jobs Plan'" },
                        new Link() { Url = "http://www.facebook.com/johnhorganbc", Title = "Premier John Horgan", Summary = "Connect with the Premier of British Columbia" },
                        new Link() { Url = "http://www.facebook.com/AboriginalEdBC", Title = "Aboriginal Education BC" },
                        new Link() { Url = "http://www.facebook.com/BizPaLBC", Title = "BC BizPaL" },
                        new Link() { Url = "http://www.facebook.com/ImmunizeBC", Title = "BC Centre for Disease Control: ImmunizeBC" },
                        new Link() { Url = "http://www.facebook.com/BCEducationandLiteracy", Title = "BC Education and Literacy" },
                        new Link() { Url = "http://www.facebook.com/pages/BCIC/124363430933347", Title = "BC Innovation Council" },
                        new Link() { Url = "http://www.facebook.com/YourBCParks", Title = "BC Parks" },
                        new Link() { Url = "http://www.facebook.com/BCForestFireInfo", Title = "BC Wildfire Service" },
                        new Link() { Url = "http://www.facebook.com/pages/Columbia-River-Treaty-Review/471508369560835?fref=ts", Title = "Columbia River Treaty Review" },
                        new Link() { Url = "http://www.facebook.com/pages/Conservation-Officer-Service/282011641840394", Title = "Conservation Officer Service" },
                        new Link() { Url = "http://www.facebook.com/pages/GoodNeighbourBC/124539291010254", Title = "GoodNeighbourBC" },
                        new Link() { Url = "http://www.facebook.com/HealthyFamiliesBC", Title = "Healthy Families BC" },
                        new Link() { Url = "https://www.facebook.com/QuitNowBC", Title = "QuitNowBC" },
                        new Link() { Url = "http://www.facebook.com/BCRecSitesandTrails", Title = "Rec Sites and Trails BC" },
                        new Link() { Url = "http://www.facebook.com/ServiceBC", Title = "Service BC" },
                        new Link() { Url = "http://www.facebook.com/HelloBC", Title = "Tourism British Columbia" },
                        new Link() { Url = "http://www.facebook.com/TranBC", Title = "TranBC" },
                        new Link() { Url = "http://www.facebook.com/WorkBC", Title = "WorkBC" },
            };


            model.YoutubeLinks = new Link[]
            {
                         new Link() { Url = "http://www.youtube.com/ProvinceofBC", Title = "Province of BC", Summary = "Subscribe to get the latest videos from the Government of British Columbia" },
                         new Link() { Url = "http://www.youtube.com/bchousing1", Title = "BC Housing" },
                         new Link() { Url = "http://www.youtube.com/BCPublicService", Title = "BC Public Service" },
                         new Link() { Url = "http://www.youtube.com/user/BCTradeInvest", Title = "BC Trade & Invest" },
                         new Link() { Url = "http://www.youtube.com/MinistryofTranBC", Title = "BC Ministry of Transportation" },
                         new Link() { Url = "http://www.youtube.com/CareerTrekBC", Title = "Career Trek BC" },
                         new Link() { Url = "http://www.youtube.com/EmergencyInfoBC", Title = "PreparedBC" },
                         new Link() { Url = "http://www.youtube.com/user/healthyfamiliesbc", Title = "Healthy Families BC" },
                         new Link() { Url = "http://www.youtube.com/user/immunizebc", Title = "ImmunizeBC" },
                         new Link() { Url = "http://www.youtube.com/LiveSmartBC", Title = "LiveSmart BC" },
                         new Link() { Url = "https://www.youtube.com/user/QuitNowBC", Title = "QuitNowBC" },
                         new Link() { Url = "http://www.youtube.com/RoadSafetyBCGov", Title = "RoadSafetyBC" },
                         new Link() { Url = "http://www.youtube.com/user/TourismBC", Title = "Tourism British Columbia" },
                         new Link() { Url = "http://www.youtube.com/user/WelcomeBCca", Title = "WelcomeBC" },
                         new Link() { Url = "http://www.youtube.com/workbc", Title = "WorkBC" },
            };

            model.FlickrLinks = new Link[]
            {
                         new Link() { Url = "http://www.flickr.com/photos/bcgovphotos", Title = "Province of BC", Summary = "View and share the latest photos from the Government of British Columbia" },
                         new Link() { Url = "http://www.flickr.com/photos/tourism_bc/", Title = "Destination British Columbia's photostream" },
                         new Link() { Url = "http://www.flickr.com/photos/tranbc/", Title = "BC Ministry of Transportation & Infrastructure's photostream" },
                         new Link() { Url = "http://www.flickr.com/photos/emergencyinfobc", Title = "PreparedBC's photostream" },
                         new Link() { Url = "http://www.flickr.com/photos/bc_housing", Title = "BC Housing's photostream" },
            };

            model.TwitterLinks = new Link[]
            {
                        new Link() { Url = "https://twitter.com/BCGovNews", Title = "@BCGovNews", Summary = "Read daily news tweets from the Government of British Columbia" },
                        new Link() { Url = "http://twitter.com/AboriginalEdBC", Title = "@AboriginalEdBC", Summary = "Discuss barriers and challenges faced by BC's Aboriginal students" },
                        new Link() { Url = "http://twitter.com/bcedplan", Title = "@BCEdPlan", Summary = "How education is changing to meet the needs of today's learners" },
                        new Link() { Url = "http://twitter.com/bcgovfireinfo", Title = "@BCGovFireInfo", Summary = "Find updates on significant wildfires around the province" },
                        new Link() { Url = "http://twitter.com/BCGovRangelands", Title = "@BCGovRangelands" , Summary = "Get info on cutting and grazing on Crown range lands across BC" },
                        new Link() { Url = "http://twitter.com/BCgovtjobs", Title = "@BCGovtJobs", Summary = "Looking for a job? Find links to BC Public Service job postings" },
                        new Link() { Url = "http://twitter.com/BC_Housing", Title = "@BC_Housing", Summary = "Learn about housing solutions and the latest projects in BC" },
                        new Link() { Url = "http://twitter.com/bcic", Title = "@BCIC", Summary = "Info on developing entrepreneurial talent and commercializing technology in BC" },
                        new Link() { Url = "https://twitter.com/BCJobsPlan", Title = "@BCJobsPlan", Summary = "Join the discussion on Canada Starts Here: The BC Jobs Plan" },
                        new Link() { Url = "http://twitter.com/bcstats", Title = "@BCStats", Summary = "Get the facts from BC's central statistical agency" },
                        new Link() { Url = "https://twitter.com/BCTradeInvest", Title = "@BCTradeInvest", Summary = "Find expertise to help your business grow internationally" },
                        new Link() { Url = "http://twitter.com/CRTreaty", Title = "@CRTreaty", Summary = "Join the discussion on the Columbia River Treaty Review" },
                        new Link() { Url = "http://twitter.com/data_bc", Title = "@Data_BC" , Summary = "Find data, services, apps and tools to inspire change or develop new ideas" },
                        new Link() { Url = "http://twitter.com/DriveBC", Title = "@DriveBC" , Summary = "Get developing road closure & weather information in BC" },
                        new Link() { Url = "https://twitter.com/DriveBC_c", Title = "@DriveBC_C" , Summary = "Travel info for the Cariboo region" },
                        new Link() { Url = "https://twitter.com/DriveBC_K", Title = "@DriveBC_K" , Summary = "Travel info for the Kootenays" },
                        new Link() { Url = "https://twitter.com/DriveBC_LM", Title = "@DriveBC_LM" , Summary = "Travel info for the Lower Mainland" },
                        new Link() { Url = "https://twitter.com/DriveBC_nc", Title = "@DriveBC_NC" , Summary = "Travel info for the North Central region" },
                        new Link() { Url = "https://twitter.com/DriveBC_ne", Title = "@DriveBC_NE" , Summary = "Travel info for the North East region" },
                        new Link() { Url = "https://twitter.com/drivebc_nw", Title = "@DriveBC_NW" , Summary = "Travel info for the North West region" },
                        new Link() { Url = "https://twitter.com/DriveBC_tok", Title = "@DriveBC_TOK" , Summary = "Travel info for the Thompson Okanagan region" },
                        new Link() { Url = "https://twitter.com/DriveBC_VI", Title = "@DriveBC_VI" , Summary = "Travel info for Vancouver Island" },
                        new Link() { Url = "http://twitter.com/emergencyinfobc", Title = "@EmergencyInfoBC" , Summary = "Receive information during extreme weather and natural disasters" },
                        new Link() { Url = "https://twitter.com/PreparedBC", Title = "@PreparedBC" , Summary = "Ready for a disaster? Get preparedness tips & recovery info here." },
                        new Link() { Url = "http://www.twitter.com/EnvReportBC", Title = "@EnvReportBC", Summary = "Get environmental data and information for British Columbia" },
                        new Link() { Url = "https://twitter.com/ERASEbullyingBC", Title = "@ERASEbullyingBC", Summary = "Important info on bullying prevention and safer schools" },
                        new Link() { Url = "http://twitter.com/govTogetherBC", Title = "@govTogetherBC" , Summary = "Find consultation and engagement opportunities in BC" },
                        new Link() { Url = "http://twitter.com/HealthyFamilyBC", Title = "@HealthyFamilyBC" , Summary = "Join the Healthy Families discussion and share your ideas" },
                        new Link() { Url = "http://twitter.com/HelloBC", Title = "@HelloBC", Summary = "Find and share tourism and travel tips for British Columbia" },
                        new Link() { Url = "https://twitter.com/immunizebc", Title = "@ImmunizeBC" , Summary = "Find info about immunization as best protection against many diseases" },
                        new Link() { Url = "http://twitter.com/LiveSmartBCca", Title = "@LiveSmartBCca" , Summary = "Learn how to make green choices that save you money" },
                        new Link() { Url = "http://twitter.com/MyBCLibrary", Title = "@MyBCLibrary" , Summary = "Promoting the value of libraries in your community" },
                        new Link() { Url = "http://twitter.com/quitnowbc", Title = "@QuitNowBC", Summary = "Want to quit smoking? We can help!" },
                        new Link() { Url = "http://twitter.com/RoadSafetyBC", Title = "@RoadSafetyBC" , Summary = "Get information on road safety and driver behaviour" },
                        new Link() { Url = "https://twitter.com/SBRoundtableBC", Title = "@SBRoundtableBC" , Summary = "Consulting with small businesses on issues, strategies & actions" },
                        new Link() { Url = "http://www.twitter.com/SmartFarmBC", Title = "@SmartFarmBC", Summary = "Helping BC Farmers learn and implement best practices" },
                        new Link() { Url = "http://twitter.com/studentaidbc", Title = "@StudentAidBC" , Summary = "Learn more about student loans, grants and scholarships in BC" },
                        new Link() { Url = "http://twitter.com/TranBC", Title = "@TranBC" , Summary = "Engaging on BC transportation and infrastructure services, projects and safety" },
                        new Link() { Url = "https://twitter.com/TranBC_BVLDS", Title = "@TranBC_BVLDS" , Summary = "Keeping the Bulkley Valley, Lakes, and Stikine District informed" },
                        new Link() { Url = "https://twitter.com/TranBC_Cariboo", Title = "@TranBC_Cariboo" , Summary = "Keeping the Cariboo District informed " },
                        new Link() { Url = "https://twitter.com/TranBC_FtGeorge", Title = "@TranBC_FtGeorge" , Summary = "Keeping the Fort George District informed" },
                        new Link() { Url = "https://twitter.com/TranBC_LMD", Title = "@TranBC_LMD" , Summary = "Latest info from the Lower Mainland" },
                        new Link() { Url = "https://twitter.com/TranBC_Peace", Title = "@TranBC_Peace" , Summary = "Keeping the Peace District informed on local news and events" },
                        new Link() { Url = "https://twitter.com/TranBC_Powell", Title = "@TranBC_Powell" , Summary = "Keeping the Powell River area informed" },
                        new Link() { Url = "https://twitter.com/TranBCRockyMtn", Title = "@TranBCRockyMtn" , Summary = "Information regarding the Rocky Mountain District" },
                        new Link() { Url = "https://twitter.com/TranBC_Skeena", Title = "@TranBC_Skeena" , Summary = "Keeping the Skeena area informed" },
                        new Link() { Url = "https://twitter.com/TranBCVanIsle", Title = "@TranBCVanIsle" , Summary = "Local road and transportation information for Vancouver Islanders" },
                        new Link() { Url = "http://twitter.com/WorkBC", Title = "@WorkBC", Summary = "Explore career paths and get tips for finding jobs in British Columbia" },
            };

            model.UstreamLinks = new Link[]
            {
                        new Link() {Url ="http://www.ustream.tv/user/EmergencyInfoBC", Title = "EmergencyInfoBC" }
            };

            model.PinterestLinks = new Link[]
            {
                        new Link() { Url ="http://pinterest.com/hellobc", Title = "Destination British Columbia" },
                        new Link() { Url ="http://pinterest.com/TranBC", Title = "TranBC" },
                        new Link() { Url ="http://pinterest.com/EmergencyInfoBC", Title = "PreparedBC" },
                        new Link() { Url ="http://www.pinterest.com/HealthyFamilyBC", Title = "Healthy Families BC" },
            };

            model.BlogsLinks = new Link[]
            {
                        new Link() { Url = "http://engage.bcedplan.ca/", Title = "BC's Education Plan" },
                        new Link() { Url = "http://www.bcic.ca/blog#", Title = "BC Innovation Council" },
                        new Link() { Url = "http://www.britishcolumbia.ca/Global/Blog/", Title = "BC Trade and Invest" },
                        new Link() { Url = "http://blog.data.gov.bc.ca/", Title = "DataBC" },
                        new Link() { Url = "http://embracebc.tumblr.com/", Title = "EmbraceBC" },
                        new Link() { Url = "http://emergencyinfobc.gov.bc.ca/", Title = "EmergencyInfoBC" },
                        new Link() { Url = "http://www2.gov.bc.ca/govtogetherbc/index.page", Title = "GovTogetherBC" },
                        new Link() { Url = "http://www.healthyfamiliesbc.ca/communities/", Title = "HealthyFamiliesBC" },
                        new Link() { Url = "http://www.hellobc.com/british-columbia/blog.aspx", Title = "HelloBC(Tourism)" },
                        new Link() { Url = "http://blog.gov.bc.ca/livingwatersmart/", Title = "Living Water Smart" },
                        new Link() { Url = "http://www.tranbc.ca/", Title = "TranBC" },
                        new Link() { Url = "https://www.workbc.ca/blog.aspx", Title = "WorkBC" },
            };

            var rssLinks = new List<Link>()
                    {
                        new Link() {Url ="https://news.gov.bc.ca/feed", Title = "BC Gov News" },
                        new Link() {Url ="https://news.gov.bc.ca/factsheets/feed", Title = "Factsheets & Opinion Editorials" },
                        new Link() {Url ="http://www.emergencyinfobc.gov.bc.ca/category/alerts/feed", Title = "EmergencyInfoBC" },
                        new Link() {Url ="http://www.healthlinkbc.ca/publichealthalerts", Title = "HealthLinkBC" },
                    };
            var ministries = model.Ministries.Select(m => m.Index).OrderBy(c => c.Name == "Office of the Premier" ? 0 : 1).ThenBy(c => c.Name);
            var sectors = await Repository.GetSectorsAsync();
            var categories = ministries.Union(sectors.OrderBy(c => c.Name)).ToList();
            foreach (var category in categories)
                rssLinks.Add(new Link() { Url = category.GetUri().ToString().TrimEnd('/') + "/feed", Title = category.Name });

            model.RssLinks = rssLinks.ToArray();

            return model;
        }

        public async Task<BaseViewModel> GetBaseModel()
        {
            var model = new BaseViewModel();
            await LoadAsync(model);
            return model;
        }

        public async Task<IList<DataIndex>> GetAllCategories()
        {
            var categoryModels = new List<DataIndex> { await Repository.GetHomeAsync() };
            categoryModels.AddRange(await Repository.GetMinistriesAsync());
            categoryModels.AddRange(await Repository.GetSectorsAsync());
            //categoryModels.AddRange(await Repository.GetThemesAsync());
            return categoryModels;
        }

        public async Task<SyndicationFeedViewModel> GetSyndicationFeedViewModel(string title, IEnumerable<string> postKeys)
        {
            var model = new SyndicationFeedViewModel();
            model.AlternateUri = new Uri(Configuration["NewsHostUri"]);

            model.Title = title;
            model.AlternateUri = null;
            model.Entries = (await Repository.GetPostsAsync(postKeys.Take(ProviderHelpers.MaximumSyndicationItems))).Where(e => e != null);

            return model;
        }
    }
}