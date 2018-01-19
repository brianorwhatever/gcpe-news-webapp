using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Gov.News.Api;
using Gov.News.Api.Models;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gov.News.Website
{
    class ExpiringList<T> : List<T>
    {
        public DateTime ExpirationDate = DateTime.Now;
        public bool IsNotExpired() { return ExpirationDate > DateTime.Now; }
    }

    public class Repository
    {
        public const string APIVersion = "1.0";
        public IClient ApiClient { get; private set; }
        public HubConnection apiConnection;
        static public Uri ContentDeliveryUri = null;

        private Dictionary<Type, IDictionary<string, object>> _cache = new Dictionary<Type, IDictionary<string, object>>();
        private Dictionary<Type, ExpiringList<object>> _expiringCache = new Dictionary<Type, ExpiringList<object>>();
        private Dictionary<string, Task> ConcurrentRequests = new Dictionary<string, Task>();

        private readonly ILogger<Repository> _logger;
        private readonly ILoggerFactory _factory;

        public Repository(IClient apiClient, IMemoryCache memoryCache, IConfiguration configuration, ILogger<Repository> logger, ILoggerFactory factory)
        {
            _logger = logger;
            _factory = factory;
            ApiClient = apiClient;
            ContentDeliveryUri = new Uri(configuration["NewsContentDeliveryNetwork"]);
            StartSignalR().GetAwaiter().GetResult();
            _expiringCache.Add(typeof(ResourceLink), new ExpiringList<object>());
            _expiringCache.Add(typeof(Newsletter), new ExpiringList<object>());
            _cache.Add(typeof(Asset), new Dictionary<string, object>());
        }

        /// <summary>
        /// Replaces the Index property. 
        /// </summary>
        /// <returns>The top level index for the site.</returns>
        public async Task StartSignalR()
        {
            string clientUrl = ApiClient.BaseUri.ToString() + "updates";
            _logger.LogInformation("Starting SignalR Client with URL: " + clientUrl);
            apiConnection = new HubConnectionBuilder()
                            .WithUrl(ApiClient.BaseUri.ToString() + "updates")
                            .WithLoggerFactory(_factory) // use the same logger as the app.
                            .Build();


            RegisterNotification<Home, IndexModel>(false);
            RegisterNotification<Service, IndexModel>(true);
            RegisterNotification<Theme, IndexModel>(false);
            RegisterNotification<Tag, IndexModel>(false);
            RegisterNotification<Sector, IndexModel>(true);
            RegisterNotification<Ministry, IndexModel>(true);

            RegisterNotification<Minister>(false);
            RegisterNotification<Post>(false);
            RegisterNotification<Slide>(true);

            RegisterNotification<FacebookPost>(false);
            RegisterNotification<TwitterFeed>(false);

            apiConnection.Closed += new Func<Exception, Task>(OnSignalRConnectionClosed);
            await apiConnection.StartAsync();
            _logger.LogInformation("SignalR Client Started");
        }

        public void RegisterNotification<T>(bool invalidateOnUpdate = false) where T : DataModel
        {
            RegisterNotification<T, T>(invalidateOnUpdate);
        }
        public void RegisterNotification<T, BT>(bool invalidateOnUpdate = false) where T : DataModel
        {
            var baseType = typeof(BT);
            IDictionary<string, object> cacheForType;
            if (!_cache.TryGetValue(baseType, out cacheForType))
            {
                cacheForType = new Dictionary<string, object>();
                _cache.Add(baseType, cacheForType);
            }
            var notificationMethod = typeof(T).Name + "Update";
            apiConnection.On<List<string>>(notificationMethod, updatedKeys => ClearOnChange<T, BT>(cacheForType, updatedKeys, invalidateOnUpdate));

            //apiConnection.InvokeAsync("SubscribeToAll");
            //apiConnection.InvokeAsync("SubscribeTo", "Ministry")
        }

        public void ClearOnChange<T, BT>(IDictionary<string, object> cacheForType, IList<string> updatedKeys, bool invalidateOnUpdate = true)
            where T : DataModel
        {
            var type = typeof(T);
            var baseType = typeof(BT);
            _logger.LogInformation("Received SignalR Update for type " + type.Name);
            if (type == typeof(Post))
            {
                ClearAllIndexPosts();
            }
            lock (cacheForType)
            {
                if (invalidateOnUpdate)
                {
                    if (baseType != typeof(IndexModel))
                    {
                        cacheForType.Clear();
                        return;
                    }
                    else
                    {
                        // clear just the ones of the changed type e.g all Ministries but not Sectors
                        updatedKeys = cacheForType.Where(e => e.Key.StartsWith(type.Name)).Select(e => e.Key).ToList();
                    }
                }
                foreach (var updatedKey in updatedKeys)
                {
                    _logger.LogInformation("Clearing key " + updatedKey);
                    cacheForType.Remove(updatedKey);
                }
            }
        }

        public async Task OnSignalRConnectionClosed(Exception ex)
        {
            _logger.LogInformation("SignalR Client Connection closed !");
            // We probably missed notifications => Clear all posts
            ClearOnChange<Post, Post>(_cache[typeof(Post)], null);
            await StartSignalR();
        }

        public void ClearAllIndexPosts()
        {
            IDictionary<string, object> cacheForType = _cache[typeof(IndexModel)];
            lock (cacheForType)
            {
                foreach (var indexModel in cacheForType.Values.ToList())
                {
                    var index = ((IndexModel)indexModel).Index;
                    cacheForType[index.Key] = new IndexModel(index);
                }
            }
        }

        private async Task<T> RunTaskHandlingConcurrentRequests<T>(Func<Task<T>> taskFn, string key)
        {
            bool taskAdded = false;
            T result;
            try
            {
                key = typeof(T).Name + key;
                Task<T> task;
                lock (ConcurrentRequests)
                {
                    if (ConcurrentRequests.ContainsKey(key))
                    {
                        task = (Task<T>)ConcurrentRequests[key]; // to prevent this UI from sending the same request to the Api
                    }
                    else
                    {
                        task = taskFn();
                        ConcurrentRequests.Add(key, task);
                        taskAdded = true;
                    }
                }
                result = await task;
            }
            finally
            {
                if (taskAdded)
                {
                    lock (ConcurrentRequests)
                    {
                        ConcurrentRequests.Remove(key);
                    }
                }
            }
            return result;
        }

        public async Task<T> GetDataModelAsync<T>(string key, Func<Task<T>> taskFn, int maxSize = 100) where T : DataModel
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            if (cacheForType.Count > maxSize)
            {
                IDictionary<string, DataModel> dataModelCacheForType = (IDictionary<string, DataModel>)cacheForType;
                foreach (var elt in dataModelCacheForType.OrderBy(m => m.Value.Timestamp).TakeLast(maxSize / 2))
                {
                    cacheForType.Remove(elt.Key);
                }
            }
            return await GetAsync(key, taskFn, cacheForType);
        }

        public async Task<IndexModel> GetIndexModelAsync<T>(string key, Func<Task<IndexModel>> taskFn)
        {
            return await GetAsync(typeof(T).Name + key, taskFn, _cache[typeof(IndexModel)]);
        }

        public async Task<T> GetAsync<T>(string key, Func<Task<T>> taskFn, IDictionary<string, object> cacheForType)
        {
            lock (cacheForType)
            {
                object cachedEntry;
                if (cacheForType.TryGetValue(key, out cachedEntry))
                {
                    return (T)cachedEntry;
                }
            }
            T model = await RunTaskHandlingConcurrentRequests(taskFn, key);

            lock (cacheForType)
            {
                if (!cacheForType.ContainsKey(key))
                    cacheForType.Add(key, model);
            }
            return model;
        }

        public async Task<IList<T>> GetListAsync<T>(Func<Task<IList<T>>> taskFn) where T : DataModel, new()
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            lock (cacheForType)
            {
                if (cacheForType.Any())
                {
                    return cacheForType.Values.Select(v => (T)v).ToList();
                }
            }

            IList<T> list = await RunTaskHandlingConcurrentRequests(taskFn, typeof(T).Name);

            lock (cacheForType)
            {
                if (cacheForType.Any())
                {
                    foreach (T item in list)
                    {
                        cacheForType.Add(item.Key, item);
                    }
                }
            }
            return list;
        }
        public async Task<IList<IndexModel>> GetIndexListAsync<T>(Func<Task<IList<IndexModel>>> taskFn) where T : DataModel, new()
        {
            IDictionary<string, object> cacheForType = _cache[typeof(IndexModel)];
            string type = typeof(T).Name;
            lock (cacheForType)
            {
                var entries = cacheForType.Where(e => e.Key.StartsWith(type));
                if (entries.Any())
                {
                    return entries.Select(v => (IndexModel)v.Value).ToList();
                }
            }

            IList<IndexModel> list = await RunTaskHandlingConcurrentRequests(taskFn, typeof(IndexModel).Name);

            lock (cacheForType)
            {
                if (!cacheForType.Any(e => e.Key.StartsWith(type)))
                {
                    foreach (IndexModel item in list)
                    {
                        cacheForType.Add(type + item.Index.Key, item);
                    }
                }
            }
            return list;
        }

        public async Task<IEnumerable<T>> GetExpiringListAsync<T>(Func<Task<IList<T>>> taskFn)
        {
            ExpiringList<object> cacheForType = _expiringCache[typeof(T)];
            lock (cacheForType)
            {
                if (cacheForType.IsNotExpired())
                {
                    return cacheForType.Select(v => (T)v);
                }
                cacheForType.ExpirationDate = DateTime.Now.AddMinutes(2);
            }

            IList<T> list = await RunTaskHandlingConcurrentRequests(taskFn, typeof(T).Name);

            lock (cacheForType)
            {
                cacheForType.Clear();
                foreach (T item in list)
                {
                    cacheForType.Add(item);
                }
            }
            return list;
        }

        public async Task<Minister> GetMinisterAsync(string ministryKey)
        {
            return await GetDataModelAsync(ministryKey, () => ApiClient.Ministries.GetMinisterAsync(ministryKey, APIVersion));
        }

        public async Task<IndexModel> GetMinistryAsync(string key)
        {
            return await GetIndexModelAsync<Ministry>(key, async () => new IndexModel(await ApiClient.Ministries.GetOneAsync(key, APIVersion)));
        }

        public async Task<IndexModel> GetSectorAsync(string key)
        {
            return await GetIndexModelAsync<Sector>(key, async () => new IndexModel(await ApiClient.Sectors.GetOneAsync(key, APIVersion)));
        }

        public async Task<IndexModel> GetServiceAsync(string key)
        {
            return await GetIndexModelAsync<Service>(key, async () => new IndexModel(await ApiClient.Services.GetOneAsync(key, APIVersion)));
        }

        public async Task<IndexModel> GetTagAsync(string key)
        {
            return await GetIndexModelAsync<Tag>(key, async () => new IndexModel(await ApiClient.Tags.GetOneAsync(key, APIVersion)));
        }

        public async Task<IndexModel> GetThemeAsync(string key)
        {
            return await GetIndexModelAsync<Theme>(key, async () => new IndexModel(await ApiClient.Themes.GetOneAsync(key, APIVersion)));
        }

        public async Task<IndexModel> GetHomeAsync()
        {
            return await GetIndexModelAsync<Home>("default", async () => new IndexModel(await ApiClient.Home.GetAsync(APIVersion)));
        }

        public async Task<Calendar> GetCalendarAsync(string key)
        {
            return null; // await GetDataModelAsync(key, ()=>ApiClient.Calendar.GetOneAsync(key.Replace('/','-'), APIVersion));
        }

        public async Task<Slide> GetSlideAsync(string id)
        {
            return await GetDataModelAsync(id, () => ApiClient.Slides.GetOneAsync(id, APIVersion));
        }
        public async Task<Edition> GetEditionAsync(string newsletterKey, string editionKey)
        {
            return await ApiClient.Newsletters.GetEditionAsync(newsletterKey, editionKey, APIVersion);
        }

        public async Task<EditionImage> GetEditionImageAsync(string key)
        {
            return await ApiClient.Newsletters.GetImageAsync(key, APIVersion);
        }

        public async Task<Article> GetArticleAsync(string newsletterKey, string editionKey, string articleKey)
        {
            return await ApiClient.Newsletters.GetArticleAsync(newsletterKey, editionKey, articleKey, APIVersion);
        }

        public async Task<TwitterFeed> GetTwitterFeedAsync()
        {
            return await ApiClient.TwitterFeed.GetOneAsync(APIVersion);
        }

        public async Task<FacebookPost> GetFacebookPostAsync(string uri)
        {
            return await GetDataModelAsync(uri, () => ApiClient.FacebookPosts.GetByUriAsync(APIVersion, uri));
        }

        public async Task<Post> GetPostAsync(string key)
        {
            if (key != null)
            {
                try
                {
                    return await GetDataModelAsync(key, () => ApiClient.Posts.GetOneAsync(key, APIVersion));
                }
                catch (Exception) { }
            }
            return null;
        }

        public async Task<string> GetLatestMediaUriAsync(string mediaType)
        {
            return await ApiClient.Posts.GetLatestMediaUriAsync(mediaType, APIVersion);
        }

        public async Task<FacebookPost> GetNewestFacebookPost()
        {
            return await ApiClient.FacebookPosts.GetNewestAsync(APIVersion);
        }

        public async Task<IList<IndexModel>> GetMinistriesAsync()
        {
            return await GetIndexListAsync<Ministry>(() => CategoriesAsync(ApiClient.Ministries.GetAllAsync(APIVersion)));
        }

        private async Task<IList<IndexModel>> CategoriesAsync<T>(Task<IList<T>> task) where T : DataIndex
        {
            return (await task).Select(m => new IndexModel(m)).ToList();
        }


        public async Task<IList<IndexModel>> GetSectorsAsync()
        {
            return await GetIndexListAsync<Sector>(() => CategoriesAsync(ApiClient.Sectors.GetAllAsync(APIVersion)));
        }

        public async Task<IList<Post>> GetPostsAsync(IEnumerable<string> postKeys)
        {
            postKeys = postKeys.Distinct();
            var posts = new List<Post>();
            if (postKeys.Any())
            {
                var postKeysToFetch = new List<string>();
                IDictionary<string, object> cacheForType = _cache[typeof(Post)];
                lock (cacheForType)
                {
                    foreach (var postKey in postKeys)
                    {
                        if (!cacheForType.ContainsKey(postKey))
                        {
                            postKeysToFetch.Add(postKey);
                        }
                    }
                }
                IList<Post> postsAsked = null;
                if (postKeysToFetch.Any())
                {
                    postsAsked = await ApiClient.Posts.GetAsync(APIVersion, postKeysToFetch);
                }

                lock (cacheForType)
                {
                    foreach (var postKey in postKeys)
                    {
                        object post;
                        if (!cacheForType.TryGetValue(postKey, out post) && postsAsked != null)
                        {
                            post = postsAsked.SingleOrDefault(p => p.Key == postKey);
                            if (post == null) continue;
                            cacheForType.Add(postKey, post);
                        }
                        posts.Add((Post)post);
                    }
                }
            }
            return posts;
        }

        public async Task<IList<IndexModel>> GetServicesAsync()
        {
            return await GetIndexListAsync<Service>(() => CategoriesAsync(ApiClient.Services.GetAllAsync(APIVersion)));
        }

        public async Task<IList<IndexModel>> GetThemesAsync()
        {
            return await GetIndexListAsync<Theme>(() => CategoriesAsync(ApiClient.Themes.GetAllAsync(APIVersion)));
        }

        public async Task<IList<IndexModel>> GetTagsAsync()
        {
            return await GetIndexListAsync<Tag>(() => CategoriesAsync(ApiClient.Tags.GetAllAsync(APIVersion)));
        }

        public async Task<IList<Slide>> GetSlidesAsync()
        {
            return await GetListAsync(() => ApiClient.Slides.GetAllAsync(APIVersion));
        }

        public async Task<Asset> GetFlickrAssetAsync(string assetUri)
        {
            if (!string.IsNullOrEmpty(assetUri))
            {
                return await GetAsync(assetUri, () => FetchFlickrAssetAsync(assetUri), _cache[typeof(Asset)]);
            }
            return null;
        }

        public async Task<Asset> FetchFlickrAssetAsync(string assetUri)
        {
            var flickrAsset = new Asset(assetUri, null, null);
            using (var httpClient = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Head, assetUri);
                var response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    flickrAsset.Length = response.Content.Headers.ContentLength ?? 0;
                }
            }
            return flickrAsset;
        }

        public async Task<IEnumerable<IndexModel>> GetPostMinistriesAsync(Post post)
        {
            return (await GetMinistriesAsync()).Where(m => post.MinistryKeys.Contains(m.Index.Key));
        }

        public async Task<IEnumerable<IndexModel>> GetPostSectorsAsync(Post post)
        {
            return (await GetSectorsAsync()).Where(s => post.SectorKeys.Contains(s.Index.Key));
        }

        public async Task<IEnumerable<ResourceLink>> GetResourceLinksAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.ResourceLinks.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Newsletter>> GetNewslettersAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.Newsletters.GetAllAsync(APIVersion));
        }
        const int MAX_NUM_CACHED_POSTS_PER_INDEX = 100;
        /// <summary>
        /// Get the next count posts of type postKind for the specified index (newsroom or category)
        /// </summary>
        /// <param name="indexModel">home or one of categories</param>
        /// <param name="postKind">One of: releases, stories, factsheets, updates or default (releases+stories except top/feature)</param>
        /// <param name="skip">number of posts to skip</param>
        /// <returns></returns>
        public async Task<IEnumerable<Post>> GetLatestPostsAsync(IndexModel indexModel, string postKind = null, int skip = 0)
        {
            int count = ProviderHelpers.MaximumLatestNewsItemsLoadMore;
            if (skip == 0)
            {
                count += ProviderHelpers.MaximumLatestNewsItems;
            }
            if (postKind == null)
            {
                int latestNewsCount = indexModel.LatestNews.Count();

                if (latestNewsCount >= skip + count)
                {
                    return indexModel.LatestNews.Skip(skip).Take(count);
                }
                skip = latestNewsCount;
            }
            DataIndex index = indexModel.Index;
            IList<Post> posts = await ApiClient.Posts.GetLatestAsync(index.Kind, index.Key, APIVersion, postKind, count, skip);
            if (postKind == null)
            {
                IDictionary<string, object> cacheForType = _cache[typeof(Post)];
                lock (cacheForType)
                {
                    foreach (Post post in posts)
                    {
                        object cachedPost;
                        if (!cacheForType.TryGetValue(post.Key, out cachedPost))
                        {
                            if (skip < MAX_NUM_CACHED_POSTS_PER_INDEX)
                            {
                                cacheForType.Add(post.Key, post);
                            }
                            cachedPost = post;
                        }
                        indexModel.LatestNews.Add((Post)cachedPost); // use the post already in cache instead of the newly downloaded (for memory reuse)
                    }
                }
            }
            return posts;
        }
    }
}