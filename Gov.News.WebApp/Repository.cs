using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
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
    public class Repository
    {
        public const string APIVersion = "1.0";
        public IClient ApiClient { get; private set; }
        public HubConnection apiConnection;
        static public Uri ContentDeliveryUri = null;

        internal Dictionary<Type, IDictionary<string, object>> _cache = new Dictionary<Type, IDictionary<string, object>>();
        private Dictionary<Type, IDictionary<string, object>> _indexCache = new Dictionary<Type, IDictionary<string, object>>();
        private Dictionary<string, Task> ConcurrentRequests = new Dictionary<string, Task>();

        private readonly ILogger<Repository> _logger;
        private readonly ILoggerFactory _factory;

        public Repository(IClient apiClient, IMemoryCache memoryCache, IConfiguration configuration, ILogger<Repository> logger, ILoggerFactory factory)
        {
            _logger = logger;
            _factory = factory;
            ApiClient = apiClient;
            ContentDeliveryUri = new Uri(configuration["NewsContentDeliveryNetwork"]);
            Task.Run(async () => await StartSignalR());

            _cache.Add(typeof(Asset), new Dictionary<string, object>());
            // changes to these objects can not be (efficiently) detected by the API, so we will have to poll for changes
            _cache.Add(typeof(Newsletter), new Dictionary<string, object>());
            _cache.Add(typeof(FacebookPost), new Dictionary<string, object>());
            _cache.Add(typeof(TwitterFeed), new Dictionary<string, object>());
        }

        /// <summary>
        /// Replaces the Index property.
        /// </summary>
        /// <returns>The top level index for the site.</returns>
        public async Task StartSignalR()
        {
            bool isReconnecting = apiConnection != null;
            while (true)
            {
                try
                {
                    string clientUrl = ApiClient.BaseUri.ToString() + "updates";
                    _logger.LogInformation("Starting SignalR Client with URL:" + clientUrl);
                    apiConnection = new HubConnectionBuilder()
                                    .WithUrl(clientUrl)
                                    .WithLoggerFactory(_factory) // use the same logger as the app.
                                    .Build();

                    RegisterNotification<Minister>(false);
                    RegisterNotification<Post>(false);
                    RegisterNotification<Slide>(true);
                    RegisterNotification<ResourceLink>(true);

                    RegisterIndexNotification<Home>(true);
                    RegisterIndexNotification<Service>(true);
                    RegisterIndexNotification<Theme>(true);
                    RegisterIndexNotification<Tag>(true);
                    RegisterIndexNotification<Sector>(true);
                    RegisterIndexNotification<Ministry>(true);

                    apiConnection.Closed += new Func<Exception, Task>(OnSignalRConnectionClosed);
                    await apiConnection.StartAsync();
                    _logger.LogInformation("SignalR Client Started");
                    if (isReconnecting)
                    {
                        // We probably missed notifications => Clear all posts
                        ClearOnChange<Post>(_cache[typeof(Post)], null);
                    }
                    return;
                }
                catch (Exception)
                {
                    apiConnection = null;
                    Thread.Sleep(10000); // 10 seconds
                }
            }
        }

        public void RegisterNotification<T>(bool invalidateOnUpdate = false) where T : DataModel
        {
            RegisterNotification<T>(_cache, invalidateOnUpdate);
        }

        public void RegisterIndexNotification<T>(bool invalidateOnUpdate = false) where T : DataModel
        {
            RegisterNotification<T>(_indexCache, invalidateOnUpdate);
        }
        public void RegisterNotification<T>(IDictionary<Type, IDictionary<string, object>> cache, bool invalidateOnUpdate = false) where T : DataModel
        {
            var type = typeof(T);
            IDictionary<string, object> cacheForType;
            if (!cache.TryGetValue(type, out cacheForType))
            {
                cacheForType = new Dictionary<string, object>();
                cache.Add(type, cacheForType);
            }
            var notificationMethod = type.Name + "Update";
            apiConnection.On<List<string>>(notificationMethod, updatedKeys => ClearOnChange<T>(cacheForType, updatedKeys, invalidateOnUpdate));

            //apiConnection.InvokeAsync("SubscribeToAll");
            //apiConnection.InvokeAsync("SubscribeTo", "Ministry")
        }

        public void ClearOnChange<T>(IDictionary<string, object> cacheForType, IList<string> updatedKeys, bool invalidateOnUpdate = true)
            where T : DataModel
        {
            var type = typeof(T);
            if (updatedKeys != null)
            {
                if (updatedKeys.Count() == 0)
                {
                    _logger.LogInformation("SignalR alpha work around ping");
                }
                else
                {
                    _logger.LogInformation("SignalR {0} Update for keys {1}", type.Name, string.Join(", ", updatedKeys));
                }
            }
            if (type == typeof(Post))
            {
                ClearAllIndexPosts();
            }
            lock (cacheForType)
            {
                if (invalidateOnUpdate || updatedKeys == null)
                {
                    cacheForType.Clear();
                    return;
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
            _logger.LogError("SignalR Client Connection closed !");
            await StartSignalR();
        }

        public void ClearAllIndexPosts()
        {
            foreach (var cacheForType in _indexCache.Values)
            {
                lock (cacheForType)
                {
                    foreach (IndexModel index in cacheForType.Values.ToList()) // ToList so we can iterate and modify the dictionary. Bruno
                    {
                        cacheForType[index.Index.Key] = new IndexModel(index.Index);
                    }
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
                        _logger.LogInformation("ConcurrentRequest: " + key);
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
            catch (Exception)
            {
                result = default(T);
            }
            if (taskAdded)
            {
                lock (ConcurrentRequests)
                {
                    ConcurrentRequests.Remove(key);
                }
            }
            return result;
        }

        #region GetAsync
        private async Task<T> GetDataModelAsync<T>(string key, Func<Task<T>> taskFn, int maxSize = 100) where T : DataModel
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            if (cacheForType.Count > maxSize)
            {
                lock (cacheForType)
                {
                    foreach (var elt in cacheForType.OrderByDescending(m => ((T)m.Value).Timestamp).TakeLast(maxSize / 2))
                    {
                        cacheForType.Remove(elt.Key);
                    }
                }
            }
            return await GetAsync(key, taskFn, cacheForType);
        }

        private async Task<IndexModel> GetIndexAsync<T>(string key, Func<Task<IndexModel>> taskFn)
        {
            return await GetAsync(key, taskFn, _indexCache[typeof(T)]);
        }

        private async Task<T> GetExpiringAsync<T>(string key, Func<Task<T>> taskFn, int expireMinutes = 2) where T : DataModel
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            T model = await GetAsync(key, taskFn, cacheForType);
            if (model != null && model.Timestamp < DateTime.Now.AddMinutes(-expireMinutes) && apiConnection != null)
            {
                lock (cacheForType)
                {
                    cacheForType.Remove(key);
                }
                model = await GetAsync(key, taskFn, cacheForType);
                model.Timestamp = DateTime.Now;
            }
            return model;

        }

        private async Task<T> GetAsync<T>(string key, Func<Task<T>> taskFn, IDictionary<string, object> cacheForType)
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
                if (model != null && !cacheForType.ContainsKey(key))
                    cacheForType.Add(key, model);
            }
            return model;
        }
        #endregion

        #region GetListAsync
        private async Task<IList<T>> GetListAsync<T>(Func<Task<IList<T>>> taskFn, IDictionary<string, object> cacheForType) where T : DataModel, new()
        {
            return await GetListAsync(taskFn, cacheForType, (item) => item);
        }

        private async Task<IList<IndexModel>> GetIndexListAsync<T>(Func<Task<IList<IndexModel>>> taskFn) where T : DataModel, new()
        {
            IDictionary<string, object> cacheForType = _indexCache[typeof(T)];
            return await GetListAsync(taskFn, cacheForType, (item) => (T)(object)item.Index); // cast to T for ConcurrentRequests type name
        }

        private async Task<IEnumerable<T>> GetExpiringListAsync<T>(Func<Task<IList<T>>> taskFn, int expireMinutes = 2) where T : DataModel, new()
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            lock (cacheForType)
            {
                var maxAge = DateTime.Now.AddMinutes(-expireMinutes);
                if (cacheForType.Any(e => ((T)e.Value).Timestamp < maxAge))
                {
                    cacheForType.Clear();
                }
            }
            return await GetListAsync(taskFn, cacheForType, (item) => { item.Timestamp = DateTime.Now; return item; });
        }

        private async Task<IList<T>> GetListAsync<T,ST>(Func<Task<IList<T>>> taskFn, IDictionary<string, object> cacheForType, Func<T, ST> dataModelFn) where ST : DataModel, new()
        {
            lock (cacheForType)
            {
                if (cacheForType.Any())
                {
                    return cacheForType.Values.Select(v => (T)v).ToList();
                }
            }

            IList<T> list = await RunTaskHandlingConcurrentRequests(taskFn, typeof(ST).Name);
            lock (cacheForType)
            {
                // Check in case a concurrent request already populated the list
                if (!cacheForType.Any())
                {
                    foreach (T item in list)
                    {
                        cacheForType.Add(dataModelFn(item).Key, item);
                    }
                }
            }
            return list;
        }

        #endregion

        public async Task<Minister> GetMinisterAsync(string ministryKey)
        {
            return await GetDataModelAsync(ministryKey, () => ApiClient.Ministries.GetMinisterAsync(ministryKey, APIVersion));
        }

        public async Task<IndexModel> GetMinistryAsync(string key)
        {
            return await GetIndexAsync<Ministry>(key, async () => (await GetMinistriesAsync(true)).SingleOrDefault(m => m.Index.Key == key));
            // Do not API fetch 1 ministry at a time as it messes up the cache
        }

        public async Task<IndexModel> GetSectorAsync(string key)
        {
            return await GetIndexAsync<Sector>(key, async () => (await GetSectorsAsync()).SingleOrDefault(s => s.Index.Key == key));
        }

        public async Task<IndexModel> GetServiceAsync(string key)
        {
            return await GetIndexAsync<Service>(key, async () => (await GetServicesAsync()).SingleOrDefault(s => s.Index.Key == key));
        }

        public async Task<IndexModel> GetTagAsync(string key)
        {
            return await GetIndexAsync<Tag>(key, async () => (await GetTagsAsync()).SingleOrDefault(t => t.Index.Key == key));
        }

        public async Task<IndexModel> GetThemeAsync(string key)
        {
            return await GetIndexAsync<Theme>(key, async () => (await GetThemesAsync()).SingleOrDefault(t => t.Index.Key == key));
        }

        public async Task<IndexModel> GetHomeAsync()
        {
            return await GetIndexAsync<Home>("default", async () => new IndexModel(await ApiClient.Home.GetAsync(APIVersion)));
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

        public async Task<FacebookPost> GetFacebookPostAsync(string uri)
        {
            return await GetExpiringAsync(uri, () => ApiClient.FacebookPosts.GetByUriAsync(APIVersion, uri));
        }

        public async Task<Post> GetPostAsync(string key)
        {
            if (key != null)
            {
                try
                {
                    return await GetDataModelAsync(key, () => ApiClient.Posts.GetOneAsync(key, APIVersion), MAX_NUM_CACHED_POSTS_PER_INDEX * 50);
                }
                catch (Exception) { }
            }
            return null;
        }

        public async Task<string> GetLatestMediaUriAsync(string mediaType)
        {
            return await ApiClient.Posts.GetLatestMediaUriAsync(mediaType, APIVersion);
        }

        public async Task<IList<IndexModel>> GetMinistriesAsync(bool includeInactive = false)
        {
            IList<IndexModel> allMinistries = await GetIndexListAsync<Ministry>(() => CategoriesAsync(ApiClient.Ministries.GetAllAsync(APIVersion)));

            if (includeInactive)
                return allMinistries;
            else
                return allMinistries.Where(m => ((Ministry)m.Index).IsActive == true).ToList();
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
            return await GetListAsync(() => ApiClient.Slides.GetAllAsync(APIVersion), _cache[typeof(Slide)]);
        }

        public async Task<IEnumerable<ResourceLink>> GetResourceLinksAsync()
        {
            return await GetListAsync(() => ApiClient.ResourceLinks.GetAllAsync(APIVersion), _cache[typeof(ResourceLink)]);
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
            DataIndex index = indexModel.Index;
            if (postKind == null)
            {
                int latestNewsCount = indexModel.LatestNews.Count();

                if (latestNewsCount > skip)
                {
                    return indexModel.LatestNews.Skip(skip).Take(count);
                }
                if (skip != latestNewsCount)
                {
                    _logger.LogError("skip ({0})/count({1}) mismatch in GetLatestPostsAsync({2})!", skip, latestNewsCount, index.Key);
                }
            }
            IList<Post> posts = await ApiClient.Posts.GetLatestAsync(index.Kind, index.Key, APIVersion, postKind, count, skip);
            if (postKind != null) return posts;

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
            return indexModel.LatestNews.Skip(skip).Take(count);
        }
    }
}