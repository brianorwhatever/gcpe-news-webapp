using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class IndexModel
    {
        public IndexModel(DataIndex index, IEnumerable<Post> latestNews = null)
        {
            Index = index;
            LatestNews = latestNews ?? new List<Post>();
        }

        public DataIndex Index { get; set; }

        public Post TopPost { get; private set; }

        public Post FeaturePost { get; private set; }

        public IEnumerable<Post> LatestNews { get; private set; }

        public static void AddTopPostKeyToLoad(DataIndex index, IList<string> postKeys)
        {
            if (index.TopPostKey != null)
            {
                postKeys.Insert(0, index.TopPostKey);
            }
        }
        public static void AddFeaturePostKeyToLoad(DataIndex index, IList<string> postKeys)
        {
            if (index.FeaturePostKey != null)
            {
                postKeys.Insert(0, index.FeaturePostKey);
            }
        }

        public static async Task<IEnumerable<Post>> LoadTopAndFeaturePosts(DataIndex index, Repository repository)
        {
            List<string> postKeys = new List<string>();
            AddTopPostKeyToLoad(index, postKeys);
            AddFeaturePostKeyToLoad(index, postKeys);
            return await repository.GetPostsAsync(postKeys);
        }

        public void SetTopPost(IEnumerable<Post> loadedPosts)
        {
            TopPost = loadedPosts.SingleOrDefault(p => p.Key == Index.TopPostKey) ?? TopPost;
        }
        public void SetFeaturePost(IEnumerable<Post> loadedPosts)
        {
            FeaturePost = loadedPosts.SingleOrDefault(p => p.Key == Index.FeaturePostKey) ?? FeaturePost;
        }

        public static IEnumerable<string> GetTopPostKeys(IEnumerable<DataIndex> dataIndexes)
        {
            return dataIndexes.Where(m => m.TopPostKey != null).Select(m => m.TopPostKey);
        }
        public static IEnumerable<string> GetFeaturePostKeys(IEnumerable<DataIndex> dataIndexes)
        {
            return dataIndexes.Where(m => m.FeaturePostKey != null).Select(m => m.FeaturePostKey);
        }
    }
}