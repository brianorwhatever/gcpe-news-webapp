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

                var index = await GetDataIndex(key);

                var model = await GetMoreArticlesModel(index, postKind, offset);

                return PartialView("MoreArticles", model);
            }
            else
            {
                return NotFound();
            }
        }

        private async Task<HomeViewModel> GetMoreArticlesModel(DataIndex _index, string postKind, int skipCount = 0)
        {
            var model = new HomeViewModel() { Index = _index };
            model.TopStory = await Repository.GetPostAsync(_index.TopPostKey);
            model.FeatureStory = await Repository.GetPostAsync(_index.FeaturePostKey);

            string indexKind = (_index is Category) ? ((Category)_index).Kind : "home";

            model.LatestNews = await Repository.GetLatestPostsAsync(indexKind, _index.Key, postKind, 0, skipCount);

            return model;
        }

        public async Task<HomeViewModel> GetHomePosts(string postKind)
        {
            Home _index = await Repository.GetHomeAsync();

            var model = new HomeViewModel() { Index = _index };
            await LoadAsync(model);
            await LoadSocialFeeds(model);

            IList<Post> data = await Repository.GetLatestPostsAsync("home", _index.Key, postKind);

            if (string.IsNullOrEmpty(postKind))
            {
                model.Title = "Home";
                model.FeedUri = ProviderHelpers.Uri(new Uri (Configuration["NewsHostUri"]), "feed");

                model.TopStory = await Repository.GetPostAsync(_index.TopPostKey);
                model.FeatureStory = await Repository.GetPostAsync(_index.FeaturePostKey);
                model.SlideItems = await Repository.GetSlidesAsync();

                await LoadSectors(model);
                //await ProviderHelpers.LoadMediaAssets(model);
            }
            else
            {
                model.Title = Char.ToUpper(postKind[0]) + postKind.Substring(1);
                model.FeedUri = ProviderHelpers.Uri(new Uri(Configuration["NewsHostUri"]), postKind + "/" + "feed");
            }

            model.LatestNews = data;
            //model.MoreNewsUri = ProviderHelpers.Uri(Properties.Settings.Default.NewsHostUri, "releases/archive");

            model.Footer = await GetFooter(null);

            return model;
        }

        public async Task LoadSectors(HomeViewModel model)
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
        }

        private async Task<SyndicationFeedViewModel> GetFeedModel(string key, string postKind)
        {
            var index = await GetDataIndex(key);
            string indexKind = (index is Category) ? ((Category)index).Kind : "home";

            var model = new SyndicationFeedViewModel();
            model.AlternateUri = new Uri(Configuration["NewsHostUri"]);

            if (string.IsNullOrEmpty(postKind))
            {
                model.Title = index.FullName;

                var posts = new List<Post>();

                var topPost = await Repository.GetPostAsync(index.TopPostKey);
                if (topPost != null)
                    posts.Add(topPost);

                var featurePost = await Repository.GetPostAsync(index.FeaturePostKey);
                if (featurePost != null)
                    posts.Add(featurePost);

                posts.AddRange(await Repository.GetLatestPostsAsync(indexKind, index.Key, postKind, ProviderHelpers.MaximumSyndicationItems - posts.Count()));
                model.Entries = posts;
            }
            else
            {
                model.Title = index.FullName + " " + char.ToUpper(postKind[0]) + postKind.Substring(1);
                model.Entries = await Repository.GetLatestPostsAsync(indexKind, index.Key, postKind, ProviderHelpers.MaximumSyndicationItems);
            }

            return model;
        }

        protected async Task<DataIndex> GetDataIndex(string key)
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

        public async Task<List<TwitterPostModel>> LoadTwitterPosts()
        {
            using (Profiler.StepStatic("Load Twitter Posts"))
            {
                // TODO - Replace with API service call.
                var screenname = "";// Data.Repository.Options.TwitterScreenName;

                var twitterFeed = await Repository.GetTwitterFeedAsync();
                var model = new List<TwitterPostModel>();
                if (twitterFeed != null)
                {
                    //try
                    //{
                        JArray jsonDat = JArray.Parse(twitterFeed.TimelineJson);

                        for (int x = 0; x < jsonDat.Count(); x++)
                        {
                            List<Replacement> replacements = new List<Replacement>();

                            DateTime postedTime = DateTime.ParseExact(jsonDat[x]["created_at"].ToString(), "ddd MMM dd HH:mm:ss +ffff yyyy", new System.Globalization.CultureInfo("en-CA"));
                            string text = jsonDat[x]["text"].ToString();
                            string initialScreenName = jsonDat[x]["user"]["screen_name"].ToString();
                            string initialUserName = jsonDat[x]["user"]["name"].ToString();
                            string entryScreenName = jsonDat[x]["user"]["screen_name"].ToString();
                            string entryUserName = jsonDat[x]["user"]["name"].ToString();
                            string initialUserImageUrl = jsonDat[x]["user"]["profile_image_url_https"].ToString();

                            JArray hashtags = JArray.Parse(jsonDat[x]["entities"]["hashtags"].ToString());
                            JArray urls = JArray.Parse(jsonDat[x]["entities"]["urls"].ToString());
                            JArray userMentions = JArray.Parse(jsonDat[x]["entities"]["user_mentions"].ToString());
                            JArray medias = new JArray();
                            if (jsonDat[x]["entities"]["media"] != null)
                                medias = JArray.Parse(jsonDat[x]["entities"]["media"].ToString());

                            if (jsonDat[x]["retweeted_status"] != null)
                            {
                                initialScreenName = jsonDat[x]["retweeted_status"]["user"]["screen_name"].ToString();
                                initialUserName = jsonDat[x]["retweeted_status"]["user"]["name"].ToString();
                                initialUserImageUrl = jsonDat[x]["retweeted_status"]["user"]["profile_image_url_https"].ToString();
                                text = jsonDat[x]["retweeted_status"]["text"].ToString();
                                hashtags = JArray.Parse(jsonDat[x]["retweeted_status"]["entities"]["hashtags"].ToString());
                                urls = JArray.Parse(jsonDat[x]["retweeted_status"]["entities"]["urls"].ToString());
                                userMentions = JArray.Parse(jsonDat[x]["retweeted_status"]["entities"]["user_mentions"].ToString());
                                if (jsonDat[x]["retweeted_status"]["entities"]["media"] != null)
                                    medias = JArray.Parse(jsonDat[x]["retweeted_status"]["entities"]["media"].ToString());
                            }
                            for (int y = hashtags.Count() - 1; y >= 0; y--)
                            {
                                JArray indexes = JArray.Parse(hashtags[y]["indices"].ToString());

                                replacements.Add(new Replacement()
                                {
                                    Start = int.Parse(indexes[0].ToString()),
                                    End = int.Parse(indexes[1].ToString()),
                                    Text = "<a href=\"https://twitter.com/hashtag/" + hashtags[y]["text"].ToString() + "?src=hash\" target=\"_blank\">#" + hashtags[y]["text"].ToString() + "</a>"
                                });
                            }
                            for (int y = urls.Count() - 1; y >= 0; y--)
                            {
                                JArray indexes = JArray.Parse(urls[y]["indices"].ToString());

                                replacements.Add(new Replacement()
                                {
                                    Start = int.Parse(indexes[0].ToString()),
                                    End = int.Parse(indexes[1].ToString()),
                                    Text = "<a href=\"" + urls[y]["url"].ToString() + "\" data-expanded-url=\"" + urls[y]["expanded_url"].ToString() + "\" title=\"" + urls[y]["expanded_url"].ToString() + "\" target=\"_blank\">" + urls[y]["display_url"].ToString() + "</a>"
                                });
                            }
                            for (int y = userMentions.Count() - 1; y >= 0; y--)
                            {
                                JArray indexes = JArray.Parse(userMentions[y]["indices"].ToString());

                                replacements.Add(new Replacement()
                                {
                                    Start = int.Parse(indexes[0].ToString()),
                                    End = int.Parse(indexes[1].ToString()),
                                    Text = "<a href=\"https://twitter.com/" + userMentions[y]["screen_name"].ToString() + "\" target=\"_blank\">@" + userMentions[y]["screen_name"].ToString() + "</a>"
                                });
                            }

                            for (int y = medias.Count() - 1; y >= 0; y--)
                            {
                                JArray indexes = JArray.Parse(medias[y]["indices"].ToString());
                                string type = medias[y]["type"].ToString();
                                if (type == "photo")
                                    replacements.Add(new Replacement()
                                    {
                                        Start = int.Parse(indexes[0].ToString()),
                                        End = int.Parse(indexes[1].ToString()),
                                        Text = "<a href=\"" + medias[y]["expanded_url"].ToString() + "\" target=\"_blank\"><img src=\"" + new Uri(medias[y]["media_url_https"].ToString()).ToProxyUrl() + "\"></a>"
                                    });
                            }

                            replacements.Sort();

                            foreach (var replacement in replacements)
                            {
                                text = text.Remove(replacement.Start) + replacement.Text + text.Substring(replacement.End);
                            }

                            model.Add(new TwitterPostModel
                            {
                                UserName = initialUserName,
                                ScreenName = initialScreenName,
                                UserImageUri = new Uri(initialUserImageUrl),
                                Posted = postedTime.ToString("MMMM d, yyyy h:mm tt"),
                                TimeAgo = ProviderHelpers.FriendlyTimeSpan(postedTime),
                                Content = text,
                                RetweetedText = screenname == initialScreenName ? "" : "Retweeted by <a href=\"https://twitter.com/" + entryScreenName + "\" target=\"_blank\" color=\"gray\">" + entryUserName + "</a>"
                            });
                        }
                    /*}
                    catch (Exception ex)
                    {
                        Program.ReportException(null, ex);
                    }*/
                }
                ////TODO: Replace mock data with calls to Twitter API
                //var model = new List<TwitterPostModel>
                //{
                //    new TwitterPostModel
                //    {
                //        TimeAgo = "2m",
                //        Posted = "9:50am - 21 May 2015",
                //        Content = @"Offered at @<a href=""#"">MyBCLibrary</a>, Summer Reading Club builds literacy skills & encourages creative discovery, <a href=""#"">http://ow.ly/PQV2C</a>"
                //    },
                //    new TwitterPostModel
                //    {
                //        TimeAgo = "5h",
                //        Posted = "9:30am - 21 May 2015",
                //        Content = @"For updates on major #<a href=""#"">BCWildfires</a>, follow @<a href=""#"">BCGovFireInfo</a> - To report wildfire, call 1-800-663-5555 or *5555 on a cellphone"
                //    },
                //    new TwitterPostModel
                //    {
                //        TimeAgo = "6h",
                //        Posted = "9:30am - 20 May 2015",
                //        Content = @"$598k helps students in northwestern #<a href=""#"">BC</a> develop skills for in-demand trades jobs, <a href=""#"">http://ow.ly/PIFHc</a> #<a href=""#"">findyourfit</a> #<a href=""#"">Terrace</a>"
                //    }
                //};
                return model;
            }
        }
        public async Task LoadSocialFeeds(ListViewModel model)
        {
            model.FacebookPosts = new List<FacebookPost>();

            FacebookPost facebookPost = await Repository.GetNewestFacebookPost();

            if (facebookPost != null)
                model.FacebookPosts.Add(facebookPost);

            model.TwitterPosts = await LoadTwitterPosts();
        }
    }
}