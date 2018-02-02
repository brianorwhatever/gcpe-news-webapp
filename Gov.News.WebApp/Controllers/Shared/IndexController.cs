using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Helpers;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace Gov.News.Website.Controllers.Shared
{
    public class IndexController<T> : Shared.NewsroomController where T : DataIndex
    {
        public IndexController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> Feed(string key, string type, string format)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var model = await GetFeedModel(key, type);

            if (model == null)
                return NotFound();

            return await GetNewsFeedContent(format, model, Request.Query["newsOnDemand"] == "1", true);
        }

        [Obsolete]
        public ActionResult Embed(string key, string type, string format)
        {
            return StatusCode(StatusCodes.Status410Gone);
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex, Obsolete]
        public ActionResult MoreNews(string key, string type)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var ministry = this is MinistriesController ? key : null;
            var sector = this is CategoryController && !(this is MinistriesController) ? key : null;
            //var newstype = ConvertTypeToSingular(type);
            return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = type });
        }

        [ResponseCache(CacheProfileName = "Archive"), Obsolete]
        public ActionResult Archive(string key, string type)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var ministry = this is MinistriesController ? key : null;
            var sector = this is CategoryController && !(this is MinistriesController) ? key : null;
            ///var newstype = ConvertTypeToSingular(type);
            return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = type });
        }

        [ResponseCache(CacheProfileName = "Archive"), Noindex, Obsolete]
        public ActionResult Month(string key, string type, int year, int? month)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var ministry = this is MinistriesController ? key : null;
            var sector = this is CategoryController && !(this is MinistriesController) ? key : null;
            //var newstype = ConvertTypeToSingular(type);
            if (month.HasValue)
            {
                var yearmonth = new DateTime(year, month.Value, 1).ToString("yyyy-MM-dd") + ".." + new DateTime(year, month.Value, 1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
                return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = type, daterange = yearmonth });
            }
            else
            {
                var yearmonth = new DateTime(year, 1, 1).ToString("yyyy-MM-dd") + ".." + new DateTime(year, 1, 1).AddYears(1).AddDays(-1).ToString("yyyy-MM-dd");
                return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = type, daterange = yearmonth });
            }
        }

        protected ActionResult GetCachedImage(byte[] imageBytes, string imageType, DateTimeOffset? timestampModified, string fileName)
        {
            if (imageBytes == null)
                return NotFound();

            DateTime imageTimeStamp = timestampModified.Value.UtcDateTime.AddTicks(-(timestampModified.Value.Ticks % TimeSpan.TicksPerSecond)); // truncate to seconds
            //TEST: Verify these headers are set correctly and received by the client browser
            HttpContext.Response.Headers["Cache-Control"] = new string[] { "private", "max-age=" + new TimeSpan(0, 1, 0).TotalSeconds.ToString("0") };
            HttpContext.Response.Headers["Last-Modified"] = imageTimeStamp.ToUniversalTime().ToString("R");

            DateTime ifModifiedSince;
            if (DateTime.TryParse(HttpContext.Request.Headers["If-Modified-Since"], out ifModifiedSince)
             && ifModifiedSince.ToUniversalTime() >= imageTimeStamp)
            {
                // The requested image has not changed
                HttpContext.Response.StatusCode = StatusCodes.Status304NotModified;
                return Content(string.Empty);
            }

            return File(imageBytes, imageType, fileName);
        }

        protected async Task<ActionResult> GetNewsFeedContent(string format, SyndicationFeedViewModel model, bool newsOnDemand, bool resetRssFeeds)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            NewsSyndicationFeed feed = new NewsSyndicationFeed(format, model, Repository);

            string content = await feed.GetContentAsync(ControllerContext, newsOnDemand, resetRssFeeds);

            string contentType;
            if (format == "atom")
            {
                contentType = "application/atom+xml";
            }
            else if (format == null || format == "rss2")
            {
                contentType = "application/rss+xml";
            }
            else
            {
                throw new NotImplementedException();
            }
            return Content(content, contentType);
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex]
        public virtual async Task<ActionResult> MoreArticles(string key, string postKind, int offset = 0)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            //TEST: Is this the same as ControllerContext.RequestContext.HttpContext.Request.IsAjaxRequest()
            if (ControllerContext.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                //Set a limit on how many articles a user can load
                //if (offset > 20)
                //   return new EmptyResult();

                var indexModel = await GetIndexModel(key);

                var model = await GetMoreArticlesModel(indexModel, postKind, offset);

                return PartialView("MoreArticles", model);
            }
            else
            {
                return NotFound();
            }
        }

        private async Task<HomeViewModel> GetMoreArticlesModel(IndexModel indexModel, string postKind, int skipCount = 0)
        {
            var model = new HomeViewModel() { IndexModel = indexModel };

            model.LatestPosts = await Repository.GetLatestPostsAsync(indexModel, postKind, skipCount);
            return model;
        }

        public async Task<HomeViewModel> GetHomePosts(string postKind)
        {
            IndexModel homeModel = await Repository.GetHomeAsync();

            var model = new HomeViewModel() { IndexModel = homeModel };
            await LoadAsync(model);

            model.LatestPosts = await Repository.GetLatestPostsAsync(homeModel, postKind);

            if (string.IsNullOrEmpty(postKind))
            {
                model.Title = "Home";

                model.FeedUri = ProviderHelpers.Uri(new Uri (Configuration["NewsHostUri"]), "feed");

                await homeModel.LoadTopAndFeaturePosts(Repository);

                model.SlideItems = await Repository.GetSlidesAsync();

                //await LoadSectors(model);
                //await ProviderHelpers.LoadMediaAssets(model);
            }
            else
            {
                model.Title = Char.ToUpper(postKind[0]) + postKind.Substring(1);
                model.FeedUri = ProviderHelpers.Uri(new Uri(Configuration["NewsHostUri"]), postKind + "/" + "feed");
            }

            //model.MoreNewsUri = ProviderHelpers.Uri(Properties.Settings.Default.NewsHostUri, "releases/archive");

            model.Footer = await GetFooter(null);

            return model;
        }

        /*public async Task LoadSectors(HomeViewModel model)
        {
            using (Profiler.StepStatic("Loading Sectors"))
            {
                model.PostSectors = new Dictionary<Post, IEnumerable<Category>>();
                if (model.TopStory != null)
                {
                    IEnumerable<Category> sectors = await Repository.GetPostSectorsAsync(model.TopStory);
                    model.PostSectors.Add(model.TopStory, sectors);
                }

                IEnumerable<Category> postSectors = null;
                if (model.FeatureStory != null && !model.PostSectors.TryGetValue(model.FeatureStory, out postSectors))
                {
                    postSectors = await Repository.GetPostSectorsAsync(model.FeatureStory);
                    model.PostSectors.Add(model.FeatureStory, postSectors);
                }

                if (model.LatestNews != null)
                {
                    foreach (Post post in model.LatestNews)
                    {
                        if (!model.PostSectors.TryGetValue(post, out postSectors))
                        {
                            postSectors = await Repository.GetPostSectorsAsync(post);
                            model.PostSectors.Add(post, postSectors);
                        }
                    }
                }
            }
        }*/

        private async Task<SyndicationFeedViewModel> GetFeedModel(string key, string postKind)
        {
            IndexModel indexModel = await GetIndexModel(key);

            var model = new SyndicationFeedViewModel();
            model.AlternateUri = new Uri(Configuration["NewsHostUri"]);

            var posts = new List<Post>();
            if (string.IsNullOrEmpty(postKind))
            {
                model.Title = indexModel.Index.Name;
                await indexModel.LoadTopAndFeaturePosts(Repository);
                if (indexModel.TopPost != null)
                    posts.Add(indexModel.TopPost);

                if (indexModel.FeaturePost != null)
                    posts.Add(indexModel.FeaturePost);
            }
            else
            {
                model.Title = indexModel.Index.Name + " " + char.ToUpper(postKind[0]) + postKind.Substring(1);
            }
            posts.AddRange(await Repository.GetLatestPostsAsync(indexModel, postKind));
            model.Entries = posts.Take(ProviderHelpers.MaximumSyndicationItems);

            return model;
        }

        protected async Task<IndexModel> GetIndexModel(string key)
        {
            if (this is DefaultController || this is PostsController)
            {
                return await Repository.GetHomeAsync();
            }
            else
            {
                var collection = (string)RouteData.Values["category"];
                switch (collection)
                {
                    case "ministries":
                        return await Repository.GetMinistryAsync(key);
                    case "sectors":
                        return await Repository.GetSectorAsync(key);
                    case "services":
                        return await Repository.GetServiceAsync(key);
                    case "tags":
                        return await Repository.GetTagAsync(key);
                    case "themes":
                        return await Repository.GetThemeAsync(key);
                }
                throw new NotImplementedException();
            }
        }
        class Replacement : IComparable
        {
            public int Start;
            public int End;
            public string Text;

            public int CompareTo(object obj)
            {
                return -Start.CompareTo(((Replacement)obj).Start);
            }
        }
    }
}