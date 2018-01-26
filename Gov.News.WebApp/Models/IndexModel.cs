using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class IndexModel
    {
        public IndexModel()
        {
            LatestNews = new List<Post>();
        }

        public IndexModel(DataIndex index)
        {
            Index = index;
            LatestNews = new List<Post>();
        }

        public DataIndex Index { get; set; }

        public Post TopPost { get; private set; }

        public Post FeaturePost { get; private set; }

        // cache for the default postType (releases+stories not factsheets)
        public IList<Post> LatestNews { get; private set; }

        public void AddTopPostKeyToLoad(IList<string> postKeys)
        {
            if (TopPost == null)
            {
                postKeys.Insert(0, Index.TopPostKey);
            }
        }
        public void AddFeaturePostKeyToLoad(IList<string> postKeys)
        {
            if (FeaturePost == null)
            {
                postKeys.Insert(0, Index.FeaturePostKey);
            }
        }
        public async Task LoadTopAndFeaturePosts(Repository repository)
        {
            List<string> postKeys = new List<string>();
            AddTopPostKeyToLoad(postKeys);
            AddFeaturePostKeyToLoad(postKeys);
            var posts = await repository.GetPostsAsync(postKeys);
            SetTopPost(posts);
            SetFeaturePost(posts);
        }

        public void SetTopPost(IEnumerable<Post> posts)
        {
            TopPost = posts.SingleOrDefault(p => p.Key == Index.TopPostKey) ?? TopPost;
        }
        public void SetFeaturePost(IEnumerable<Post> posts)
        {
            FeaturePost = posts.SingleOrDefault(p => p.Key == Index.FeaturePostKey) ?? FeaturePost;
        }

        public static IEnumerable<string> GetTopPostKeysToLoad(IEnumerable<IndexModel> indexes)
        {
            return indexes.Where(m => m.TopPost == null).Select(m => m.Index.TopPostKey);
        }
        public static IEnumerable<string> GetFeaturePostKeysToLoad(IEnumerable<IndexModel> indexes)
        {
            return indexes.Where(m => m.FeaturePost == null).Select(m => m.Index.FeaturePostKey);
        }
    }
}