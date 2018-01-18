using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Helpers;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers.Shared
{
    public class PostsController : IndexController<DataIndex>
    {
        public PostsController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }
        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Details(string key)
        {
            var post = await Repository.GetPostAsync(key);

            if (post == null)
                return await SearchNotFound();

            var model = await LoadDetails(post);

            if (model == null)
                return await SearchNotFound();

            if (model.Post.RedirectUri != null)
                return Redirect(model.Post.RedirectUri.ToString());

            //TODO: Test NotModified handling
            //var ifModifiedSince = Request.Headers["If-Modified-Since"];
            //if (ifModifiedSince != null)
            //{
            //    var modifiedSince = DateTime.Parse(ifModifiedSince).ToLocalTime();

            //    if (modifiedSince >= model.Post.Timestamp)
            //        return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotModified);
            //}

            //TEST: Verify this header is set correctly and received by the client browser
            HttpContext.Response.Headers["Last-Modified"] = model.Post.Timestamp.Value.ToUniversalTime().ToString("R");

            ViewData.Add("CustomContentClass", "detail");
            return View("PostView", model);
        }
        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Image(string key)
        {
            if (key == null)
                return await SearchNotFound();

            var post = await Repository.GetPostAsync(key);

            var thumbnailUri = post.GetThumbnailUri();

            var thumbnailUriProxy = thumbnailUri.ToProxyUrl();

            var client = new System.Net.Http.HttpClient(new HttpClientHandler() { UseDefaultCredentials = true });

            client.DefaultRequestHeaders.Referrer = new Uri(string.Concat(Request.Scheme, "://", Request.Host.ToUriComponent(), Request.PathBase.ToUriComponent(), Request.Path, Request.QueryString));

            //Originally tried to get the stream directly using the httpclient as per below, but from the stream there is no way to 
            //accurately tell the content type. Which is needed in order to send the stream correctly. 

            //var stream = await client.GetStreamAsync(thumbnailUriProxy);

            //Instead we simply get the HTTP Request Response 
            var result = await client.GetAsync(thumbnailUriProxy);

            //Get the type from the response as interpreted by the proxy server
            var type = result.Content.Headers.ContentType;

            //Then create the stream based on the response content.
            var stream = await result.Content.ReadAsStreamAsync();

            if (stream == null)
                return await SearchNotFound();

            HttpContext.Response.Headers["Last-Modified"] = post.Timestamp.Value.ToUniversalTime().ToString("R");

            return File(stream, type.ToString());
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex]
        public async Task<ActionResult> Index(string key, string postKind)
        {
            var model = await GetHomePosts(postKind);

            if (model == null)
                return await SearchNotFound();

            return View("PostsView", model);
        }

        const int RelatedArticlesLength = 3;

        public async Task<PostViewModel> LoadDetails(Post post)
        {
            PostViewModel model = new PostViewModel(post);

            await LoadAsync(model);

            var ministry = await Repository.GetMinistryAsync(post.LeadMinistryKey);
            var ministryModel = new CategoryModel(ministry,
                await Repository.GetPostAsync(ministry.TopPostKey), await Repository.GetPostAsync(ministry.FeaturePostKey));

            var posts = await Repository.GetLatestPostsAsync(ministry.Kind, ministry.Key, null, 3);
            foreach (var news in posts)
            {
                ministryModel.LatestNews.Add(news);
            }
            model.LeadMinistry = ministryModel;
            model.Minister = await Repository.GetMinisterAsync(ministry.Key);

            model.RelatedMinistries = await Repository.GetPostMinistriesAsync(post);

            model.RelatedSectors = await Repository.GetPostSectorsAsync(post);

            //Load [RelatedArticlesLength] posts, excluding the current post
            if (ministryModel.FeaturePost != null)
            {
                posts.Insert(0, ministryModel.FeaturePost);
            }
            if (ministryModel.TopPost != null)
            {
                posts.Insert(0, ministryModel.TopPost);
            }
            model.RelatedArticles = posts.Where(e => e.Key != model.Post.Key).Take(RelatedArticlesLength);

            if (post.AssetUrl != null)
            {
                if (post.AssetUrl.Contains("facebook"))
                {
                    model.FacebookAsset = await Repository.GetFacebookPostAsync(post.AssetUrl);
                }
            }

            model.Footer = await GetFooter(ministry);

            model.FacebookPostDetailsDictionary = await GetFacebookAssetDetails(post.Documents);

            return model;
        }
        public async Task<IDictionary<string, FacebookPost>> GetFacebookAssetDetails(IEnumerable<Document> documents)
        {
            var assetHtmlDetails = new Dictionary<string, FacebookPost>();
            foreach (var doc in documents)
            {
                MatchCollection matches = AssetHelper.AssetRegex.Matches(doc.DetailsHtml);
                foreach (Match itemMatch in matches)
                {
                    string url = itemMatch.Groups["url"].Value;
                    try
                    {
                        Uri uri = new Uri(url);

                        if (uri.Host == "www.facebook.com" && !assetHtmlDetails.Any(a => a.Key == url))
                        {
                            var model = await Repository.GetFacebookPostAsync(url);
                            if (model != null)
                            {
                                assetHtmlDetails.Add(url, model);
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }
            return assetHtmlDetails;
        }

    }
}
