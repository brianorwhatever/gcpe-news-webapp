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
    class LimitedSizeDictionary<TKey, TValue> : Dictionary<TKey, TValue> where TValue : DataModel
    {
        int maxSize;
        public LimitedSizeDictionary(int _maxSize) : base(_maxSize)
        {
            maxSize = _maxSize;
        }

        public new void Add(TKey key, TValue value)
        {
            if (Count > maxSize)
            {
                foreach (var elt in this.OrderBy(m => m.Value.Timestamp).TakeLast(maxSize / 2))
                {
                    Remove(elt.Key);
                }
            }
            base.Add(key, value);
        }
    }

    public class Repository
    {
        public const string APIVersion = "1.0";
        public IClient ApiClient { get; private set; }
        public HubConnection apiConnection;

        private Dictionary<Type, LimitedSizeDictionary<string, DataModel>> _cache = new Dictionary<Type, LimitedSizeDictionary<string, DataModel>>();
        private Dictionary<Type, ExpiringList<object>> _expiringCache = new Dictionary<Type, ExpiringList<object>>();
        private Dictionary<string, Task> ConcurrentRequests = new Dictionary<string, Task>();

        private readonly ILogger<Repository> _logger;
        private readonly ILoggerFactory _factory;

        public Repository(IClient apiClient, IMemoryCache memoryCache, ILogger<Repository> logger, ILoggerFactory factory)
        {
            _logger = logger;
            _factory = factory;
            ApiClient = apiClient;
            StartSignalR().GetAwaiter().GetResult();
            _expiringCache.Add(typeof(ResourceLink), new ExpiringList<object>());
            _expiringCache.Add(typeof(Newsletter), new ExpiringList<object>());
            _cache.Add(typeof(Asset), new LimitedSizeDictionary<string, DataModel>(1000));
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
            RegisterNotification<TwitterFeed>(false, 20);

            apiConnection.Closed += new Func<Exception, Task>(OnSignalRConnectionClosed);
            await apiConnection.StartAsync();
            _logger.LogInformation("SignalR Client Started");
        }

        public void RegisterNotification<T>(bool invalidateOnUpdate = false, int maxSize = 100) where T : DataModel
        {
            var type = typeof(T);
            LimitedSizeDictionary<string, DataModel> cacheForType;
            if (!_cache.TryGetValue(type, out cacheForType))
            {
                cacheForType = new LimitedSizeDictionary<string, DataModel>(maxSize);
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

        public async Task<T> GetAsync<T>(string key, Func<Task<T>> taskFn) where T : DataModel
        {
            LimitedSizeDictionary<string, DataModel> cacheForType = _cache[typeof(T)];
            lock (cacheForType)
            {
                DataModel cachedEntry;
                if (cacheForType.TryGetValue(key, out cachedEntry))
                {
                    return (T)cachedEntry;
                }
            }
            DataModel model = await RunTaskHandlingConcurrentRequests(taskFn, key);

            lock (cacheForType)
            {
                if (!cacheForType.ContainsKey(key))
                    cacheForType.Add(key, model);
            }
            return (T)model;
        }

        public async Task<IEnumerable<T>> GetListAsync<T>(Func<Task<IList<T>>> taskFn) where T : DataModel, new()
        {
            LimitedSizeDictionary<string, DataModel> cacheForType = _cache[typeof(T)];
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
            return await GetAsync(key, () => ApiClient.Ministry.GetOneAsync(key, APIVersion));
        }

        public async Task<Minister> GetMinisterAsync(string ministryKey)
        {
            return await GetAsync(ministryKey, () => ApiClient.Minister.GetOneAsync(ministryKey, APIVersion));
        }

        public async Task<Sector> GetSectorAsync(string key)
        {
            return await GetAsync(key, () => ApiClient.Sector.GetOneAsync(key, APIVersion));
        }

        public async Task<Calendar> GetCalendarAsync(string key)
        {
            return null; // await GetAsync(key, ()=>ApiClient.Calendar.GetOneAsync(key.Replace('/','-'), APIVersion));
        }

        public async Task<Home> GetHomeAsync()
        {
            return await GetAsync("default", () => ApiClient.Home.GetAsync(APIVersion));
        }

        public async Task<Slide> GetSlideAsync(string id)
        {
            return await GetAsync(id, () => ApiClient.Slide.GetOneAsync(id, APIVersion));
        }
        public async Task<Edition> GetEditionAsync(string newsletterKey, string editionKey)
        {
            return await ApiClient.Edition.GetOneAsync(newsletterKey, editionKey, APIVersion);
        }

        public async Task<EditionImage> GetEditionImageAsync(string key)
        {
            return await ApiClient.Edition.GetImageAsync(key, APIVersion);
        }

        public async Task<Article> GetArticleAsync(string newsletterKey, string editionKey, string articleKey)
        {
            return await ApiClient.Article.GetOneAsync(newsletterKey, editionKey, articleKey, APIVersion);
        }

        public async Task<TwitterFeed> GetTwitterFeedAsync()
        {
            return await ApiClient.TwitterFeed.GetOneAsync(APIVersion);
        }

        public async Task<FacebookPost> GetFacebookPostAsync(string uri)
        {
            return await GetAsync(uri, () => ApiClient.FacebookPost.GetByUriAsync(APIVersion, uri));
        }

        public async Task<Post> GetPostAsync(string key)
        {
            if (key != null)
            {
                try
                {
                    return await GetAsync(key, () => ApiClient.Post.GetOneAsync(key, APIVersion));
                }
                catch (Exception) { }
            }
            return null;
        }

        public async Task<string> GetLatestMediaUriAsync(string mediaType)
        {
            return await ApiClient.Post.GetLatestMediaUriAsync(mediaType, APIVersion);
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
            return await ApiClient.FacebookPost.GetNewestAsync(APIVersion);
        }

        public async Task<IEnumerable<Ministry>> GetMinistriesAsync(bool loadFeaturePosts = false, bool loadTopPosts = true)
        {
            var ministries = await GetListAsync(() => ApiClient.Ministry.GetAllAsync(APIVersion));
            await EnsurePostsAreCachedAsync(ministries, loadFeaturePosts, loadTopPosts);
            return ministries;
        }

        public async Task<IEnumerable<Category>> GetSectorsAsync(bool loadFeaturePosts = false, bool loadTopPosts = true)
        {
            var sectors = await GetListAsync(() => ApiClient.Sector.GetAllAsync(APIVersion));
            await EnsurePostsAreCachedAsync(sectors, loadFeaturePosts, loadTopPosts);
            return sectors;
        }

        private async Task EnsurePostsAreCachedAsync(IEnumerable<Category> categories, bool loadFeaturePosts, bool loadTopPosts)
        {
            var postsRefToAsk = new List<string>();
            LimitedSizeDictionary<string, DataModel> cacheForType = _cache[typeof(Post)];
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
                var postsAsked = await ApiClient.Post.GetPostsAsync(APIVersion, postsRefToAsk);

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
            return await GetListAsync(() => ApiClient.Theme.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Tag>> GetTagsAsync()
        {
            return await GetListAsync(() => ApiClient.Tag.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Service>> GetServicesAsync()
        {
            return await GetListAsync(() => ApiClient.Service.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Slide>> GetSlidesAsync()
        {
            return await GetListAsync(() => ApiClient.Slide.GetAllAsync(APIVersion));
        }

        public Uri GetBlobSasUri(AzureAsset azureAsset)
        {
            string url = ApiClient.AzureAsset.GetBlobSasUri(APIVersion, azureAsset.Key);
            return new Uri(url);
        }

        public async Task<Asset> GetFlickrAssetAsync(string assetUri)
        {
            if (!string.IsNullOrEmpty(assetUri))
            {
                return await GetAsync(assetUri, () => FetchFlickrAssetAsync(assetUri));
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

        public async Task<AzureAsset> GetAzureAssetAsync(string path)
        {
            return await GetAsync(path, () => ApiClient.AzureAsset.GetOneAsync(path, APIVersion));
        }

        public async Task<IEnumerable<AzureAsset>> GetAzureAssetsFromPostAsync(Post post)
        {
            return await ApiClient.AzureAsset.GetFromPostAsync(post.Key, APIVersion);
        }

        public async Task<IEnumerable<Ministry>> GetMinistriesAsync(Post post)
        {
            return await ApiClient.Ministry.GetFromPostAsync(post.Key, APIVersion);
        }

        public async Task<IEnumerable<Sector>> GetSectorsAsync(Post post)
        {
            return await ApiClient.Sector.GetFromPostAsync(post.Key, APIVersion);
        }

        public async Task<IEnumerable<ResourceLink>> GetResourceLinksAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.ResourceLink.GetAllAsync(APIVersion));
        }

        public async Task<IEnumerable<Newsletter>> GetNewslettersAsync()
        {
            return await GetExpiringListAsync(() => ApiClient.Newsletter.GetAllAsync(APIVersion));
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
            return await ApiClient.Post.GetLatestPostsAsync(indexKind, indexKey, APIVersion, postKind, count, skip);
        }

        public async Task<System.IO.Stream> GetAzureFileStream(string blobName, IConfiguration Configuration)
        {
            var container = new Microsoft.WindowsAzure.Storage.Blob.CloudBlobContainer(new Uri(Configuration["NewsFilesContainer"]));

            var blob = container.GetBlobReference(blobName);

            if (!await blob.ExistsAsync())
                return null;

            var client = new System.Net.Http.HttpClient();

            return await client.GetStreamAsync(new Uri(Configuration["NewsContentDeliveryNetwork"] + "files/" + blobName));
        }

        public async Task<System.IO.Stream> GetAzureAssetStream(AzureAsset asset, IConfiguration Configuration)
        {

            var azureAsset = (AzureAsset)asset;

            var blob = azureAsset.GetBlobReference(Configuration);

            if (!await blob.ExistsAsync())
                return null;

            var client = new System.Net.Http.HttpClient();

            return await client.GetStreamAsync(azureAsset.ContentDeliveryUri);
        }
    }
}