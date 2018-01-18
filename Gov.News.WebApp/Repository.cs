using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Gov.News.Api;
using Gov.News.Api.Models;
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


            RegisterNotification<Home>(false);
            RegisterNotification<Sector>(true);
            RegisterNotification<Service>(true);
            RegisterNotification<Theme>(false);
            RegisterNotification<Tag>(false);
            RegisterNotification<Ministry>(true);

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
            var type = typeof(T);
            IDictionary<string, object> cacheForType;
            if (!_cache.TryGetValue(type, out cacheForType))
            {
                cacheForType = new Dictionary<string, object>();
                _cache.Add(type, cacheForType);
            }

            apiConnection.On<List<string>>(type.Name + "Update", updatedKeys =>
            {
                _logger.LogInformation("Received SignalR Update for type " + type.Name);
                lock (cacheForType)
                {
                    if (invalidateOnUpdate)
                    {
                        cacheForType.Clear();
                    }
                    else
                    {
                        foreach (var updatedKey in updatedKeys)
                        {
                            _logger.LogInformation("Clearing key " + updatedKey);
                            cacheForType.Remove(updatedKey);
                        }
                    }
                }
            });

            //apiConnection.InvokeAsync("SubscribeToAll");

            //apiConnection.InvokeAsync("SubscribeTo", "Ministry")
        }

        public async Task OnSignalRConnectionClosed(Exception ex)
        {
            _logger.LogInformation("SignalR Client Connection closed !");
            await StartSignalR();
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

        public async Task<IEnumerable<T>> GetListAsync<T>(Func<Task<IList<T>>> taskFn) where T : DataModel, new()
        {
            IDictionary<string, object> cacheForType = _cache[typeof(T)];
            lock (cacheForType)
            {
                if (cacheForType.Count() != 0)
                {
                    return cacheForType.Values.Select(v => (T)v);
                }
            }

            IEnumerable<T> list = await RunTaskHandlingConcurrentRequests(taskFn, typeof(T).Name);

            lock (cacheForType)
            {
                if (cacheForType.Count() == 0)
                {
                    foreach (T item in list)
                    {
                        cacheForType.Add(item.Key, item);
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

            IEnumerable<T> list = await RunTaskHandlingConcurrentRequests(taskFn, typeof(T).Name);

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

        public async Task<Ministry> GetMinistryAsync(string key)
        {
            return await GetDataModelAsync(key, () => ApiClient.Ministries.GetOneAsync(key, APIVersion));
        }

        public async Task<Minister> GetMinisterAsync(string ministryKey)
        {
            return await GetDataModelAsync(ministryKey, () => ApiClient.Ministries.GetMinisterAsync(ministryKey, APIVersion));
        }

        public async Task<Sector> GetSectorAsync(string key)
        {
            return await GetDataModelAsync(key, () => ApiClient.Sectors.GetOneAsync(key, APIVersion));
        }

        public async Task<Calendar> GetCalendarAsync(string key)
        {
            return null; // await GetDataModelAsync(key, ()=>ApiClient.Calendar.GetOneAsync(key.Replace('/','-'), APIVersion));
        }

        public async Task<Home> GetHomeAsync()
        {
            return await GetDataModelAsync("default", () => ApiClient.Home.GetAsync(APIVersion));
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

        public async Task AddPostAsync(string postKey, List<Post> posts)
        {
            var topPost = await GetPostAsync(postKey);
            if (topPost != null)
            {
                posts.Add(topPost);
            }
        }
        public async Task AddTopPostsAsync(IEnumerable<DataIndex> indexes, List<Post> posts)
        {
            foreach (var ministry in indexes)
            {
                if (posts.Count >= ProviderHelpers.MaximumSyndicationItems) break;
                await AddPostAsync(ministry.TopPostKey, posts);
            }
        }
        public async Task AddFeaturePostsAsync(IEnumerable<DataIndex> indexes, List<Post> posts)
        {
            foreach (var ministry in indexes)
            {
                if (posts.Count >= ProviderHelpers.MaximumSyndicationItems) break;
                await AddPostAsync(ministry.FeaturePostKey, posts);
            }
        }

        public async Task<FacebookPost> GetNewestFacebookPost()
        {
            return await ApiClient.FacebookPosts.GetNewestAsync(APIVersion);
        }

        public async Task<IEnumerable<Ministry>> GetMinistriesAsync(bool loadFeaturePosts = false, bool loadTopPosts = true)
        {
            var ministries = await GetListAsync(() => ApiClient.Ministries.GetAllAsync(APIVersion));
            await EnsurePostsAreCachedAsync(ministries, loadFeaturePosts, loadTopPosts);
            return ministries;
        }

        public async Task<IEnumerable<Sector>> GetSectorsAsync(bool loadFeaturePosts = false, bool loadTopPosts = true)
        {
            var sectors = await GetListAsync(() => ApiClient.Sectors.GetAllAsync(APIVersion));
            await EnsurePostsAreCachedAsync(sectors, loadFeaturePosts, loadTopPosts);
            return sectors;
        }

        private async Task EnsurePostsAreCachedAsync(IEnumerable<Category> categories, bool loadFeaturePosts, bool loadTopPosts)
        {
            var postsRefToAsk = new List<string>();
            IDictionary<string, object> cacheForType = _cache[typeof(Post)];
            lock (cacheForType)
            {
                foreach (var category in categories)
                {
                    if (loadTopPosts && category.TopPostKey != null && !cacheForType.ContainsKey(category.TopPostKey))
                    {
                        postsRefToAsk.Add(category.TopPostKey);
                    }
                    if (loadFeaturePosts && category.FeaturePostKey != null && !cacheForType.ContainsKey(category.FeaturePostKey))
                    {
                        postsRefToAsk.Add(category.TopPostKey);
                    }
                }
            }
            if (postsRefToAsk.Count > 0)
            {
                var postsAsked = await ApiClient.Posts.GetAsync(APIVersion, postsRefToAsk);

                lock (cacheForType)
                {
                    foreach (var post in postsAsked)
                    {
                        if (!cacheForType.ContainsKey(post.Key))
                        {
                            cacheForType.Add(post.Key, post);
                        }
                    }
                }
            }
        }

        public async Task<IEnumerable<Theme>> GetThemesAsync()
        {
            return await GetListAsync(() => ApiClient.Themes.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Tag>> GetTagsAsync()
        {
            return await GetListAsync(() => ApiClient.Tags.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Service>> GetServicesAsync()
        {
            return await GetListAsync(() => ApiClient.Services.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Slide>> GetSlidesAsync()
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

        public async Task<IEnumerable<Ministry>> GetPostMinistriesAsync(Post post)
        {
            return (await GetMinistriesAsync()).Where(m => post.MinistryKeys.Contains(m.Key));
        }

        public async Task<IEnumerable<Sector>> GetPostSectorsAsync(Post post)
        {
            return (await GetSectorsAsync()).Where(s => post.SectorKeys.Contains(s.Key));
        }

        public async Task<IEnumerable<ResourceLink>> GetResourceLinksAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.ResourceLinks.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Newsletter>> GetNewslettersAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.Newsletters.GetAllAsync(APIVersion));
        }

        /// <summary>
        /// Get the next count posts of type postKind for the specified index (newsroom or category)
        /// </summary>
        /// <param name="indexKind">home or one of categories</param>
        /// <param name="indexKey">default or one key of the categories (ministries, sectors, services, tags, themes)</param>
        /// <param name="postKind">One of: releases, stories, factsheets, updates or default (releases+stories except top/feature)</param>
        /// <param name="count">number of posts to return</param>
        /// <param name="skip">number of posts to skip</param>
        /// <returns></returns>
        public async Task<IList<Post>> GetLatestPostsAsync(string indexKind, string indexKey, string postKind = null, int count = 0, int skip = 0)
        {
            if (count == 0)
            {
                count = ProviderHelpers.MaximumLatestNewsItemsLoadMore;
                if (skip == 0) count += ProviderHelpers.MaximumLatestNewsItems;
            }
            return await ApiClient.Posts.GetLatestAsync(indexKind, indexKey, APIVersion, postKind, count, skip);
        }
    }
}