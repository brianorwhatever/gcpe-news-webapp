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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers.Shared
{
    public class PostsController : IndexController<DataIndex>
    {
        public PostsController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        public async Task<ActionResult> Details(string key)
        {
            var post = await Repository.GetPostAsync(key);

            var model = await LoadDetails(post);

            if (post?.RedirectUri != null)
                return Redirect(post.RedirectUri.ToString());

            if (model == null)
                return await SearchNotFound();

            // We can't just use post.Timestamp here because we would have to take into account the mega-menu(ministries), sidebar(media contacts, featured topics an services, related newsletters and posts), Live Webcast)
            //if (NotModifiedSince(post.Timestamp))
            //    return StatusCode(StatusCodes.Status304NotModified);

            ViewData.Add("CustomContentClass", "detail");
            return View("PostView", model);
        }

        public async Task<ActionResult> Image(string key)
        {
            var post = key != null ? await Repository.GetPostAsync(key) : null;

            if (post == null)
                return await SearchNotFound();

            if (NotModifiedSince(post.Timestamp))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

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
            Ministry ministry = post != null ? await Repository.GetMinistryAsync(post.LeadMinistryKey) : null;
            if (ministry == null) return null;

            PostViewModel model = new PostViewModel(post);

            await LoadAsync(model);

            model.LeadMinistry = model.Ministries.FirstOrDefault(m => m.Index.Key == post.LeadMinistryKey);

            model.Minister = await Repository.GetMinisterAsync(post.LeadMinistryKey);

            model.RelatedMinistryKeys = (await Repository.GetPostMinistriesAsync(post)).Select(m => m.Key);

            model.RelatedSectorKeys = (await Repository.GetPostSectorsAsync(post)).Select(m => m.Key);

            //Load [RelatedArticlesLength] posts, excluding the current post
            List<Post> posts = new List<Post>();
            if (model.LeadMinistry.TopPost != null)
            {
                posts.Add(model.LeadMinistry.TopPost);
            }
            if (model.LeadMinistry.FeaturePost != null)
            {
                posts.Add(model.LeadMinistry.FeaturePost);
            }
            posts.AddRange(await Repository.GetLatestPostsAsync(ministry, RelatedArticlesLength - posts.Count + 1, null, MinistryFilter(ministry.Key)));
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
