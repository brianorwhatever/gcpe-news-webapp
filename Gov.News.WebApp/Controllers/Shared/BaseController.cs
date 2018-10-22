using Gov.News.Api.Models;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Controllers.Shared
{
    //[SessionState(System.Web.SessionState.SessionStateBehavior.Disabled)]
    public abstract class BaseController : Controller
    {
        protected Repository Repository;
        protected IConfiguration Configuration;

        public BaseController(Repository repository, IConfiguration configuration)
        {
            Repository = repository;
            Configuration = configuration;
        }

        public async Task LoadAsync(BaseViewModel model, List<IndexModel> additionalIndexModels = null)
        {
            using (Profiler.StepStatic("Create Ministry Models"))
            {
                var ministries = await Repository.GetMinistriesAsync();
                List<string> uncachedPostKeys = IndexModel.GetTopPostKeys(ministries).ToList();
                uncachedPostKeys.AddRange(IndexModel.GetFeaturePostKeys(ministries));
                if (additionalIndexModels != null)
                {
                    var additionalIndexes = additionalIndexModels.Select(m => m.Index);
                    uncachedPostKeys.AddRange(IndexModel.GetTopPostKeys(additionalIndexes));
                    uncachedPostKeys.AddRange(IndexModel.GetFeaturePostKeys(additionalIndexes));
                }
                IEnumerable<Post> loadedPosts = await Repository.GetPostsAsync(uncachedPostKeys);

                foreach (var ministry in ministries)
                {
                    var ministryModel = new IndexModel(ministry);
                    ministryModel.SetTopPost(loadedPosts);
                    ministryModel.SetFeaturePost(loadedPosts);
                    model.Ministries.Add(ministryModel);
                }
                if (additionalIndexModels != null)
                {
                    foreach (var indexModel in additionalIndexModels)
                    {
                        indexModel.SetTopPost(loadedPosts);
                        indexModel.SetFeaturePost(loadedPosts);
                    }
                }
            }

            model.ResourceLinks = await Repository.GetResourceLinksAsync();
            var homeSettings = await Repository.GetHomeAsync();
            model.WebcastingLive = !string.IsNullOrEmpty(homeSettings?.LiveWebcastM3uPlaylist);
        }

        public class NewsroomFilter : MemoryStream
        {
            private readonly Stream response;

            public NewsroomFilter(Stream response)
            {
                this.response = response;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                string html = System.Text.Encoding.UTF8.GetString(buffer);

                html = html.Replace("http://www.newsroom.gov.bc.ca/", "https://www.newsroom.gov.bc.ca/");

                buffer = System.Text.Encoding.UTF8.GetBytes(html);
                response.Write(buffer, offset, buffer.Length);
            }
        }

        public Uri Uri(Uri baseUri, string relativeUri)
        {
            return new Uri(baseUri.ToString().TrimEnd('/') + "/" + relativeUri.TrimStart('/'));
        }

        public async Task<FooterViewModel> GetFooter(Category category)
        {
            var footer = new FooterViewModel();

            footer.FlickrSource = "";
            //TODO: This should be filtered by post kind e.g. Repository.Index.Factsheets
            if (category != null) // && (category.Kind == "ministries" || category.Kind == "sectors"))
            {
                footer.FlickrMoreUri = new Uri(category.FlickrUri == null ? "http://www.flickr.com/photos/bcgovphotos/show/" : category.FlickrUri);
                footer.YoutubeMoreUri = new Uri(category.YoutubeUri == null ? "https://www.youtube.com/user/ProvinceofBC/" : category.YoutubeUri);
                footer.SoundcloudMoreUri = new Uri(category.AudioUri == null ? "https://soundcloud.com/bcgov/" : category.AudioUri);

                if (category.Name.ToUpper() == "OFFICE OF THE PREMIER" || category.Name.ToUpper() == "SPEECHES")
                {
                    footer.FlickrSource = "Office of the Premier";
                    footer.YoutubeSource = "Office of the Premier";
                    footer.SoundcloudSource = "Office of the Premier";
                }
                else
                {
                    footer.FlickrSource = category.GetType().Name;
                    footer.YoutubeSource = category.GetType().Name;
                    footer.SoundcloudSource = category.GetType().Name;
                }
            }

            if (Properties.Settings.Default.EnableDynamicFooter != null && Properties.Settings.Default.EnableDynamicFooter.ToLower().Equals("true"))
            {
                footer.LatestFlickrUri = (await Repository.GetLatestMediaUriAsync("flickr")).ToUri();
                footer.LatestYoutubeUri = (await Repository.GetLatestMediaUriAsync("youtube")).ToUri();
            }

            if (footer.FlickrMoreUri == null)
            {
                footer.FlickrSource = "BC Government";
                footer.FlickrMoreUri = new System.Uri("http://www.flickr.com/photos/bcgovphotos/show/");
            }

            if (footer.YoutubeMoreUri == null)
            {
                footer.YoutubeSource = "BC Government";
                footer.YoutubeMoreUri = new System.Uri("https://www.youtube.com/user/ProvinceofBC/");
            }

            if (footer.SoundcloudMoreUri == null)
            {
                footer.SoundcloudSource = "BC Government";
                footer.SoundcloudMoreUri = new System.Uri("https://soundcloud.com/bcgov/");
            }

            return footer;
        }
    }
}