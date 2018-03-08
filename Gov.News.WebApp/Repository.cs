using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Gov.News.Api;
using Gov.News.Api.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Microsoft.Rest;

namespace Gov.News.Website
{
    public class Repository
    {
        public const string APIVersion = "1.0";
        public IClient ApiClient { get; private set; }
        public bool apiConnected = true;
        static public Uri ContentDeliveryUri = null;

        internal Dictionary<Type, IDictionary<string, object>> _cache = new Dictionary<Type, IDictionary<string, object>>();
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
            //_cache.Add(typeof(TwitterFeed), new Dictionary<string, object>());
        }

        /// <summary>
        /// Replaces the Index property.
        /// </summary>
        /// <returns>The top level index for the site.</returns>
        public async Task StartSignalR()
        {
            while (true)
            {
                try
                {
                    string clientUrl = ApiClient.BaseUri.ToString() + "updates";
                    _logger.LogInformation("Starting SignalR Client with URL:" + clientUrl);
                    HubConnection api = new HubConnectionBuilder()
                                                .WithUrl(clientUrl)
                                                .WithLoggerFactory(_factory) // use the same logger as the app.
                                                .Build();

                    RegisterNotification<Minister>(api, (keys) => GetMinistersAsync(keys).GetAwaiter().GetResult());
                    RegisterNotification<Post>(api, (keys) => GetPostsAsync(keys, true).GetAwaiter().GetResult());
                    RegisterNotification<Home>(api, (ignore) => GetHomeAsync().GetAwaiter().GetResult());

                    RegisterNotification<Slide>(api, (ignore) => GetSlidesAsync().GetAwaiter().GetResult());
                    RegisterNotification<ResourceLink>(api, (ignore) => GetResourceLinksAsync().GetAwaiter().GetResult());
                    RegisterNotification<Service>(api, (ignore) => GetServicesAsync().GetAwaiter().GetResult());
                    RegisterNotification<Theme>(api, (ignore) => GetThemesAsync().GetAwaiter().GetResult());
                    RegisterNotification<Tag>(api, (ignore) => GetTagsAsync().GetAwaiter().GetResult());
                    RegisterNotification<Sector>(api, (ignore) => GetSectorsAsync().GetAwaiter().GetResult());
                    RegisterNotification<Ministry>(api, (ignore) => GetMinistriesAsync().GetAwaiter().GetResult());

                    api.Closed += new Func<Exception, Task>(OnSignalRConnectionClosed);
                    await api.StartAsync();
                    apiConnected = true;
                    _logger.LogInformation("SignalR Client Started");
                    // We probably missed notifications => Clear all entries
                    ClearAllCaches();
                    return;
                }
                catch (Exception)
                {
                    Thread.Sleep(10000); // 10 seconds
                }
            }
        }

        public void RegisterNotification<T>(HubConnection api, Func<IEnumerable<string>, object> updateFn) where T : DataModel
        {
            var type = typeof(T);
            IDictionary<string, object> cacheForType;
            if (!_cache.TryGetValue(type, out cacheForType))
            {
                cacheForType = new Dictionary<string, object>();
                _cache.Add(type, cacheForType);
            }
            var notificationMethod = type.Name + "Update";
            api.On<IEnumerable<string>>(notificationMethod, updatedKeys => ClearOnChange<T>(cacheForType, updatedKeys, updateFn));

            //apiConnection.InvokeAsync("SubscribeToAll");
            //apiConnection.InvokeAsync("SubscribeTo", "Ministry")
        }

        public void ClearOnChange<T>(IDictionary<string, object> cacheForType, IEnumerable<string> updatedKeys, Func<IEnumerable<string>, object> updateFn)
            where T : DataModel
        {
            var type = typeof(T);
            if (updatedKeys.Count() == 0)
            {
                _logger.LogInformation("SignalR alpha work around ping");
            }
            else
            {
                _logger.LogInformation("SignalR {0} Update for keys {1}", type.Name, string.Join(", ", updatedKeys));
                bool isPostUpdate = type.Name == "Post";
                if (!isPostUpdate)
                {
                    lock (cacheForType)
                    {
                        if (type.Name == "Minister")
                        {
                            foreach (string key in updatedKeys)
                            {
                                cacheForType.Remove(key);
                            }
                        }
                        else
                        {
                            cacheForType.Clear();
                        }
                    }
                }
                // call the API server (maybe be a different one)
                var update = updateFn(updatedKeys);
                if (isPostUpdate)
                {
                    lock (cacheForType)
                    {
                        var oldestPost = cacheForType.Any() ? (Post)cacheForType.TakeLast(1).SingleOrDefault().Value : null;
                        var updatedPosts = update as IList<Post>;
                        bool need2ReSortPosts = false;
                        foreach (string key in updatedKeys)
                        {
                            Post updatedPost = updatedPosts.FirstOrDefault(p => p.Key == key);
                            if (updatedPost == null)
                            {
                                cacheForType.Remove(key); // post unpublished
                                continue;
                            }
                            if (oldestPost == null || oldestPost.PublishDate > updatedPost.PublishDate) continue; // Can't insert as it would probably out of order
                            object cachedPost;
                            cacheForType.TryGetValue(key, out cachedPost);
                            need2ReSortPosts |= cachedPost == null || ((Post)cachedPost).PublishDate != updatedPost.PublishDate;
                            cacheForType[key] = updatedPost;
                        }
                        PurgeCache<T>(cacheForType, NUM_CACHED_POSTS * 2);
                        if (need2ReSortPosts)
                        {
                            var newPostList = cacheForType.OrderByDescending(m => ((Post)m.Value).PublishDate).ToList();
                            cacheForType.Clear();
                            foreach (KeyValuePair<string, object> p in newPostList)
                            {
                                cacheForType.Add(p);
                            }
                        }
                    }
                }
            }
        }

        public async Task OnSignalRConnectionClosed(Exception ex)
        {
            apiConnected = false;
            _logger.LogError("SignalR Client Connection closed !");
            await StartSignalR();
        }

        public void ClearAllCaches()
        {
            foreach (var cacheForType in _cache.Values)
            {
                lock (cacheForType)
                {
                    cacheForType.Clear();
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

        private Func<Task<T>> ConfigureAwaitFunction<T>(Func<Task<HttpOperationResponse<T>>> taskFn) // where T is a DataModel or a List of DataModels
        {
            return async () =>
            {
                using (var _result = await taskFn().ConfigureAwait(false))
                {
                    return _result.Body;
                }
            };
        }

        private void PurgeCache<T>(IDictionary<string, object> cacheForType, int maxSize) where T : DataModel
        {
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
        }
        #region GetAsync
        private async Task<T> GetDataModelAsync<T>(string key, Func<Task<HttpOperationResponse<T>>> updateFn, int maxSize = 100) where T : DataModel
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            PurgeCache<T>(cacheForType, maxSize);
            return await GetAsync(key, ConfigureAwaitFunction(updateFn), cacheForType);
        }

        private async Task<T> GetExpiringAsync<T>(string key, Func<Task<T>> taskFn, int expireMinutes = 2) where T : DataModel
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            T model = await GetAsync(key, taskFn, cacheForType);
            if (model != null && model.Timestamp < DateTime.Now.AddMinutes(-expireMinutes) && apiConnected)
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
                if (cacheForType.TryGetValue(key, out cachedEntry) || !apiConnected)
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
        private async Task<IList<T>> GetListAsync<T>(Func<Task<HttpOperationResponse<IList<T>>>> taskFn) where T : DataModel, new()
        {
            return await GetListAsync(ConfigureAwaitFunction(taskFn), _cache[typeof(T)], (item) => item);
        }

        private async Task<IEnumerable<T>> GetExpiringListAsync<T>(Func<Task<IList<T>>> taskFn, int expireMinutes = 2) where T : DataModel, new()
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            lock (cacheForType)
            {
                var maxAge = DateTime.Now.AddMinutes(-expireMinutes);
                if (cacheForType.Any(e => ((T)e.Value).Timestamp < maxAge) && apiConnected)
                {
                    cacheForType.Clear();
                }
            }
            return await GetListAsync(taskFn, cacheForType, (item) => { item.Timestamp = DateTime.Now; return item; });
        }

        private async Task<IList<T>> GetListAsync<T, ST>(Func<Task<IList<T>>> taskFn, IDictionary<string, object> cacheForType, Func<T, ST> dataModelFn) where ST : DataModel, new()
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

        public static Dictionary<string, List<string>> MustRevalidateHeader()
        {
            var list = new List<string>() { CacheControlHeaderValue.MustRevalidateString };
            return new Dictionary<string, List<string>>() { { HeaderNames.CacheControl, list } };
        }

        public async Task<Minister> GetMinisterAsync(string ministryKey)
        {
            return await GetDataModelAsync(ministryKey, () => ApiClient.Ministries.GetMinisterWithHttpMessagesAsync(ministryKey, APIVersion, MustRevalidateHeader()));
        }

        public async Task<IList<Minister>> GetMinistersAsync(IEnumerable<string> ministryKeys)
        {
            var ministers = new List<Minister>();
            foreach (var ministryKey in ministryKeys)
            {
                ministers.Add(await GetMinisterAsync(ministryKey));
            }
            return ministers;
        }

        public async Task<Ministry> GetMinistryAsync(string key)
        {
            return (await GetMinistriesAsync(true)).SingleOrDefault(m => m.Key == key);
            // Do not API fetch 1 ministry at a time as it messes up the cache
        }

        public async Task<Sector> GetSectorAsync(string key)
        {
            return (await GetSectorsAsync()).SingleOrDefault(s => s.Key == key);
        }

        public async Task<Home> GetHomeAsync()
        {
            return await GetDataModelAsync("default", async () => await ApiClient.Home.GetWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
        }

        public async Task<Slide> GetSlideAsync(string id)
        {
            return (await GetSlidesAsync()).SingleOrDefault(s => s.Key == id);
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
                    return (await GetPostsAsync(new List<string>() { key })).FirstOrDefault();
                }
                catch (Exception) { }
            }
            return null;
        }

        public async Task<string> GetLatestMediaUriAsync(string mediaType)
        {
            return await ApiClient.Posts.GetLatestMediaUriAsync(mediaType, APIVersion);
        }

        public async Task<IList<Ministry>> GetMinistriesAsync(bool includeInactive = false)
        {
            IList<Ministry> allMinistries = await GetListAsync(() => ApiClient.Ministries.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));

            if (includeInactive)
                return allMinistries;
            else
                return allMinistries.Where(m => m.IsActive == true).ToList();
        }

        public async Task<IList<Sector>> GetSectorsAsync()
        {
            return await GetListAsync(() => ApiClient.Sectors.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
        }

        public async Task<IList<Post>> GetPostsAsync(IEnumerable<string> postKeys, bool revalidate = false)
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
                        object post;
                        if (revalidate || !cacheForType.TryGetValue(postKey, out post))
                        {
                            postKeysToFetch.Add(postKey);
                        }
                        else
                        {
                            posts.Add((Post)post);
                        }
                    }
                }
                if (postKeysToFetch.Any() && apiConnected)
                {
                    var customHeaders = revalidate ? MustRevalidateHeader() : null;
                    IList<Post> postsAsked = await ConfigureAwaitFunction(() => ApiClient.Posts.GetWithHttpMessagesAsync(APIVersion, postKeysToFetch, customHeaders))();

                    lock (cacheForType)
                    {
                        posts.AddRange(postsAsked);
                    }
                }
            }
            return posts;
        }

        public async Task<IList<Service>> GetServicesAsync()
        {
            return await GetListAsync(() => ApiClient.Services.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
        }

        public async Task<IList<Theme>> GetThemesAsync()
        {
            return await GetListAsync(() => ApiClient.Themes.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
        }

        public async Task<IList<Tag>> GetTagsAsync()
        {
            return await GetListAsync(() => ApiClient.Tags.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
        }

        public async Task<IList<Slide>> GetSlidesAsync()
        {
            return await GetListAsync(() => ApiClient.Slides.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
        }

        public async Task<IEnumerable<ResourceLink>> GetResourceLinksAsync()
        {
            return await GetListAsync(() => ApiClient.ResourceLinks.GetAllWithHttpMessagesAsync(APIVersion, MustRevalidateHeader()));
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

        public async Task<IEnumerable<Ministry>> GetPostMinistriesAsync(Post post)
        {
            return (await GetMinistriesAsync()).Where(m => post.MinistryKeys.Contains(m.Key));
        }

        public async Task<IEnumerable<Sector>> GetPostSectorsAsync(Post post)
        {
            return (await GetSectorsAsync()).Where(s => post.SectorKeys.Contains(s.Key));
        }

        public async Task<IEnumerable<Newsletter>> GetNewslettersAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.Newsletters.GetAllAsync(APIVersion));
        }

        const int NUM_CACHED_POSTS = 2000;
        /// <summary>
        /// Get the next count posts of type postKind for the specified index (newsroom or category)
        /// </summary>
        /// <param name="indexModel">home or one of categories</param>
        /// <param name="count">number of posts to get</param>
        /// <param name="postKind">One of: releases, stories, factsheets, updates or default (releases+stories except top/feature)</param>
        /// <param name="categoryFilter">filter on Ministry, Sector, Themes, Service or Tag</param>
        /// <param name="skip">number of posts to skip (ignoring top/feature posts</param>
        /// <returns></returns>
        public async Task<IEnumerable<Post>> GetLatestPostsAsync(DataIndex index, int count, string postKind = null, Func<Post, bool> categoryFilter = null, int skip = 0)
        {
            int postCountB4ApiCall;
            IEnumerable<Post> filteredPosts;
            IDictionary<string, object> cacheForPosts = _cache[typeof(Post)];
            lock (cacheForPosts)
            {
                postCountB4ApiCall = cacheForPosts.Count();
                var postKindFilter = postKind != null ? p => p.Kind == postKind : (Func<Post, bool>)(p => p.Kind == "releases" || p.Kind == "stories");
                filteredPosts = cacheForPosts.Select(p => (Post)p.Value).Where(postKindFilter).ToList();
            }

            bool cacheClearHappenedWhileUserIsBrowsingOldReleases = skip > filteredPosts.Count();
            bool canBeCached = (postKind == null || postKind == "factsheets") && categoryFilter == null && postCountB4ApiCall < NUM_CACHED_POSTS && !cacheClearHappenedWhileUserIsBrowsingOldReleases;

            int skipToAsk = canBeCached ? filteredPosts.Count() : skip;
            if (postKind == null)
            {
                filteredPosts = filteredPosts.Where(p => p.Key != index.TopPostKey && p.Key != index.FeaturePostKey);
            }
            if (categoryFilter != null)
            {
                filteredPosts = filteredPosts.Where(categoryFilter).ToList();
            }

            bool useCache = skip + count <= filteredPosts.Count(); // enough posts in the cache to use it?
            IEnumerable<Post> fetchedPosts = null;
            if ((canBeCached || !useCache) && apiConnected)
            {
                // Ask for more when we can cache it.
                int countToAsk = canBeCached ? ProviderHelpers.MaximumLatestNewsItemsLoadMore * 5 : count;
                fetchedPosts = await ApiClient.Posts.GetLatestAsync(index.Kind, index.Key, APIVersion, postKind, countToAsk, skipToAsk);
                if (canBeCached)
                {
                    CacheLatestPosts(fetchedPosts, cacheForPosts, postCountB4ApiCall); // we also cache top/feature posts (sorted by PublishDate)
                    if (!useCache)
                    {
                        fetchedPosts = fetchedPosts.Where(p => p.Key != index.TopPostKey && p.Key != index.FeaturePostKey);
                    }
                }
            }
            return (useCache || fetchedPosts == null ? filteredPosts.Skip(skip) : fetchedPosts).Take(count);
        }

        public void CacheLatestPosts(IEnumerable<Post> posts, IDictionary<string, object> cacheForPosts, int postCountB4ApiCall)
        {
            lock (cacheForPosts)
            {
                // check that nobody inserted some new posts
                if (postCountB4ApiCall == cacheForPosts.Count())
                {
                    foreach (Post post in posts)
                    {
                        if (!cacheForPosts.ContainsKey(post.Key))
                        {
                            cacheForPosts.Add(post.Key, post);
                        }
                    }
                }
            }
        }
    }
}