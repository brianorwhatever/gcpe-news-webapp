using System.Collections.Generic;
using System.Linq;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class ListViewModel : BaseViewModel
    {
        public IndexModel IndexModel { get; set; }

        // for all postTypes (not just the cached LatestNews)
        public IEnumerable<Post> LatestPosts { get; set; }
        public System.Uri FeedUri { get; set; }
        public Category Category { get; set; }

        public FooterViewModel Footer { get; set; }

        public List<FacebookPost> FacebookPosts { get; set; }
        public List<TwitterPostModel> TwitterPosts { get; set; }

        /*public System.Uri MoreNewsUri { get; set; }
        public IEnumerable<FlickrAsset> TopStoryFlickrAssets { get; set; }
        public IEnumerable<YoutubeAsset> TopStoryYoutubeAssets { get; set; }

        public IEnumerable<FlickrAsset> FeatureStoryFlickrAssets { get; set; }
        public IEnumerable<YoutubeAsset> FeatureStoryYoutubeAssets { get; set; }*/
        /*
        public async Task<IEnumerable<PostModel>> GetLatestIgnorePinned(PostCollection postAccessor, int count, int skipCount = 0)
        {
            var excludeKeys = new List<string>();

            if (TopStory != null)
                excludeKeys.Add(TopStory.Post.Key);

            if (FeatureStory != null)
                excludeKeys.Add(FeatureStory.Post.Key);

            return await PostModel.CreateAsync(await postAccessor.TakeLastAsync(count, skipCount, excludeKeys));
        }

        public async Task<IEnumerable<PostModel>> GetLatestIgnorePinned(PostCollection postAccessor, int count)
        {
            var excludeKeys = new List<string>();

            if (TopStory != null)
                excludeKeys.Add(TopStory.Post.Key);

            if (FeatureStory != null)
                excludeKeys.Add(FeatureStory.Post.Key);

            List<PostModel> posts = new List<PostModel>();

            IEnumerable<PostModel> last = await PostModel.CreateAsync(await postAccessor.TakeLastAsync(count + excludeKeys.Count));

            foreach (var post in last)
            {
                if (posts.Count >= count)
                    break;

                if (excludeKeys.Contains(post.Post.Key))
                    continue;

                posts.Add(post);
            }

            return posts;
        }
        */
        public override string SubscribePath()
        {
            if (Category != null)
            {
                return "/subscribe?" + Category.Kind + "=" + Category.Key;
            }

            return base.SubscribePath();
        }
    }
}