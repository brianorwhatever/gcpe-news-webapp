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
            if (TopPost == null && Index.TopPostKey != null)
            {
                postKeys.Insert(0, Index.TopPostKey);
            }
        }
        public void AddFeaturePostKeyToLoad(IList<string> postKeys)
        {
            if (FeaturePost == null && Index.FeaturePostKey != null)
            {
                postKeys.Insert(0, Index.FeaturePostKey);
            }
        }

        public async Task LoadTopAndFeaturePosts(Repository repository)
        {
            List<string> postKeys = new List<string>();
            AddTopPostKeyToLoad(postKeys);
            AddFeaturePostKeyToLoad(postKeys);
            var loadedPosts = await repository.GetPostsAsync(postKeys);
            SetTopPost(loadedPosts);
            SetFeaturePost(loadedPosts);
        }

        public void SetTopPost(IEnumerable<Post> loadedPosts)
        {
            TopPost = loadedPosts.SingleOrDefault(p => p.Key == Index.TopPostKey) ?? TopPost;
        }
        public void SetFeaturePost(IEnumerable<Post> loadedPosts)
        {
            FeaturePost = loadedPosts.SingleOrDefault(p => p.Key == Index.FeaturePostKey) ?? FeaturePost;
        }

        public static IEnumerable<string> GetUncachedTopPostKeys(IEnumerable<IndexModel> indexModels)
        {
            return indexModels.Where(m => m.TopPost == null && m.Index.TopPostKey != null).Select(m => m.Index.TopPostKey);
        }
        public static IEnumerable<string> GetUncachedFeaturePostKeys(IEnumerable<IndexModel> indexModels)
        {
            return indexModels.Where(m => m.FeaturePost == null && m.Index.FeaturePostKey != null).Select(m => m.Index.FeaturePostKey);
        }

        public static void CacheTopPosts(IEnumerable<IndexModel> indexModels, IEnumerable<Post> loadedPosts)
        {
            foreach (var indexModel in indexModels)
            {
                indexModel.SetTopPost(loadedPosts);
            }
        }
        public static void CacheFeaturePosts(IEnumerable<IndexModel> indexModels, IEnumerable<Post> loadedPosts)
        {
            foreach (var indexModel in indexModels)
            {
                indexModel.SetFeaturePost(loadedPosts);
            }
        }
    }
}