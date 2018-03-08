using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class CategoryController : Shared.IndexController<Category>
    {
        public CategoryController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Details(string category, string key, string postKind)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var index = await GetDataIndex(key, category) as Category;

            if (index == null)
                return await SearchNotFound();

            var model = await GetModel(index, postKind);

            ViewBag.Type = postKind;
            return View("CategoryView", model);
        }

        public async Task<ListViewModel> GetModel(Category category, string postKind)
        {
            ListViewModel model = category is Ministry ? new MinistryViewModel() : new ListViewModel();

            model.CanonicalUri = category.GetUri();
            model.FeedUri = ProviderHelpers.Uri(category.GetUri(), "feed");

            int count = ProviderHelpers.MaximumLatestNewsItemsLoadMore + ProviderHelpers.MaximumLatestNewsItems;
            var latestNews = await Repository.GetLatestPostsAsync(category, count, postKind, GetIndexFilter(category));
            model.IndexModel = new IndexModel(category, latestNews);
            model.Title = category.Name;
            model.Category = category;
            await LoadAsync(model, string.IsNullOrEmpty(postKind) ? new List<IndexModel> { model.IndexModel } : null);

            if (category is Ministry)
                await LoadMinisterData((MinistryViewModel)model, (Ministry)category);

            model.Footer = await GetFooter(category);

            return model;
        }

        public async Task LoadMinisterData(MinistryViewModel model, Ministry ministry)
        {
            using (Profiler.StepStatic("Loading Minister Data"))
            {
                model.Ministry = ministry;

                model.Minister = await Repository.GetMinisterAsync(ministry.Key);

                if (ministry.ChildMinistryKey != null)
                {
                    model.ChildMinistry = await Repository.GetMinistryAsync(ministry.ChildMinistryKey);
                }

                if (model.ChildMinistry != null && "Speeches".Equals(model.ChildMinistry.Key, StringComparison.CurrentCultureIgnoreCase))
                    model.ChildMinistry = null;

                if (model.ChildMinistry != null)
                {
                    model.ChildMinistryMinister = await Repository.GetMinisterAsync(model.ChildMinistry.Key);
                }
            }
        }

        //const int CategoriesPostsLength = 3;

        private async Task<CategoriesViewModel> Init(string categoryKind)
        {
            Models.CategoriesViewModel model = new Models.CategoriesViewModel();
            var categoryModels = new List<IndexModel>();

            model.Footer = await GetFooter(null);

            using (Profiler.StepStatic("Get Category Posts"))
            {
                //var uncachedTopFeaturePostKeys = new List<string>();
                var categories = await GetCategoryList(categoryKind);
                if (categoryKind == "ministries")
                {
                    categories = categories.Where(m => ((Ministry)m).ParentMinistryKey == null).OrderByDescending(m => m.Name == "Office of the Premier");
                }

                foreach (var category in categories)
                {
                    //TODO: Choose post type based on 'type' supplied to controller.

                    //List<Post> posts = new List<Post>();
                    //IndexModel.AddTopPostKeyToLoad(category, uncachedTopFeaturePostKeys);
                    //IndexModel.AddFeaturePostKeyToLoad(category, uncachedTopFeaturePostKeys);

                    //var keys = posts.Select(e => e.Key).Where(e => e != null).ToList();
                    //posts.AddRange(await category.Default.TakeLastAsync(3 - keys.Count(), 0, keys));

                    var latestNews = await Repository.GetLatestPostsAsync(category, 1, null, GetIndexFilter(category));
                    var categoryModel = new IndexModel(category, latestNews);
                    categoryModels.Add(categoryModel);
                    /*await PostModel.CreateAsync(await category.DefaultProperty.TakeLastAsync(CategoriesPostsLength));
                    foreach (var post in last)
                    {
                        if (posts.Count == CategoriesPostsLength)
                            break;

                        if (posts.Any(e => e != null && e.Key == post.Key))
                            continue;

                        posts.Add(post);
                    }*/

                    //categoryPosts.Add(category, last.Take(CategoriesPostsLength));
                }
                await LoadAsync(model, categoryModels);
                model.Categories = categoryModels;
            }
            return model;
        }

        public async Task<ActionResult> Index(string category)
        {
            var model = await Init(category);
            model.Title = category.ToUpper()[0] + category.Substring(1);
            return View("CategoriesView", model);
        }
    }
}