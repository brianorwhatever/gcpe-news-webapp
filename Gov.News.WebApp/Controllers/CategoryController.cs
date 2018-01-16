using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class CategoryController : Shared.IndexController<Category>
    {
        public CategoryController(Repository repository, IConfiguration configuration): base(repository, configuration)
        {
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Details(string category, string key, string postKind)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            Category index = null;

            if (category == "ministries")
            {
                index = await Repository.GetMinistryAsync(key);
            }
            else if (category == "sectors")
            {
                index = await Repository.GetSectorAsync(key);
            }
            else
            {
                throw new NotImplementedException();
            //    index = await Repository.GetCategoryAsync(category, key);
            }
            var model = await GetModel(index, postKind);

            if (model == null)
                return await SearchNotFound();

            if (string.Equals(postKind, "factsheets", StringComparison.OrdinalIgnoreCase))
                ViewBag.IsFactsheetsPage = true;  // hack? :)

            ViewBag.Type = postKind ?? "";
            return View("CategoryView", model);
        }

        public async Task<ListViewModel> GetModel(Category category, string postKind)
        {
            var model = category is Ministry ? new MinistryViewModel() : new ListViewModel();
            await LoadAsync(model);

            model.CanonicalUri = category.GetUri();
            model.FeedUri = ProviderHelpers.Uri(category.GetUri(), "feed");
            model.Index = category;
            model.Title = category.Name;
            model.Category = category;

            if (category is Ministry)
                await LoadMinisterData((MinistryViewModel)model, (Ministry)category);

            if (string.IsNullOrEmpty(postKind))
            {
                model.TopStory = await Repository.GetPostAsync(category.TopPostKey);
                model.FeatureStory = await Repository.GetPostAsync(category.FeaturePostKey);
            }
            model.Footer = await GetFooter(category);
            model.LatestNews = await Repository.GetLatestPostsAsync(category.Kind, category.Key, postKind);

            await LoadSocialFeeds(model);

            return model;
        }

        public async Task LoadMinisterData(MinistryViewModel model, Ministry ministry)
        {
            using (Profiler.StepStatic("Loading Minister Data"))
            {
                model.Ministry = ministry;

                model.Minister = await Repository.GetMinisterAsync(ministry.Key);

                model.ChildMinistry = ministry.ChildMinistryKey != null ? await Repository.GetMinistryAsync(ministry.ChildMinistryKey) : null;

                if (model.ChildMinistry != null && "Speeches".Equals(model.ChildMinistry.Key, StringComparison.CurrentCultureIgnoreCase))
                    model.ChildMinistry = null;

                if (model.ChildMinistry != null)
                {
                    model.ChildMinistryMinister = await Repository.GetMinisterAsync(model.ChildMinistry.Key);
                }
            }
        }

        const int CategoriesPostsLength = 3;

        private async Task<CategoriesViewModel> Init()
        {
            Models.CategoriesViewModel model = new Models.CategoriesViewModel();
            await LoadAsync(model);

            model.Footer = await GetFooter(null);

            return model;
        }

        private async Task<Dictionary<Category, IEnumerable<Post>>> GetCategoryPosts(IEnumerable<Category> categories)
        {
            using (Profiler.StepStatic("Get Category Posts"))
            {
                var categoryPosts = new Dictionary<Category, IEnumerable<Post>>();

                foreach (var category in categories)
                {
                    //TODO: Choose post type based on 'type' supplied to controller.

                    List<Post> posts = new List<Post>();

                    if (category.TopPostKey != null)
                    {
                        posts.Add(await Repository.GetPostAsync(category.TopPostKey));
                    }

                    if (category.FeaturePostKey != null)
                    {
                        posts.Add(await Repository.GetPostAsync(category.FeaturePostKey));
                    }

                    //var keys = posts.Select(e => e.Key).Where(e => e != null).ToList();
                    //posts.AddRange(await category.Default.TakeLastAsync(3 - keys.Count(), 0, keys));

                    IList<Post> last = await Repository.GetLatestPostsAsync(category.Kind, category.Key, null, CategoriesPostsLength - posts.Count());

                    /*await PostModel.CreateAsync(await category.DefaultProperty.TakeLastAsync(CategoriesPostsLength));
                    foreach (var post in last)
                    {
                        if (posts.Count == CategoriesPostsLength)
                            break;

                        if (posts.Any(e => e != null && e.Key == post.Key))
                            continue;

                        posts.Add(post);
                    }*/
                    posts.AddRange(last);

                    categoryPosts.Add(category, posts);
                }

                return categoryPosts;
            }
        }

        protected async Task<CategoriesViewModel> GetViewModel(string category)
        {
            var model = await Init();

            if (category == "ministries")
            {
                model.Categories = (await Repository.GetMinistriesAsync()).Where(m => m.ParentMinistryKey == null).OrderByDescending(m => m.Name == "Office of the Premier");
            }
            else if (category == "sectors")
            {
                model.Categories = (await Repository.GetSectorsAsync()).ToList();
            }
            else if (category == "services")
            {
                model.Categories = (await Repository.GetServicesAsync()).ToList();
            }
            else if (category == "themes")
            {
                model.Categories = (await Repository.GetThemesAsync()).ToList();
            }
            else if (category == "tags")
            {
                model.Categories = (await Repository.GetTagsAsync()).ToList();
            }

            model.Title = category.ToUpper()[0] + category.Substring(1);

            model.CategoryPosts = await GetCategoryPosts(model.Categories);

            return model;
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex]
        public async Task<ActionResult> Index(string category)
        {
            var model = await GetViewModel(category);
            return View("CategoriesView", model);
        }
    }
}