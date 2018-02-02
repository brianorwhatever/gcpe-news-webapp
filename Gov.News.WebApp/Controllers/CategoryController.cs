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

            IndexModel indexModel = null;

            if (category == "ministries")
            {
                indexModel = await Repository.GetMinistryAsync(key);
            }
            else if (category == "sectors")
            {
                indexModel = await Repository.GetSectorAsync(key);
            }
            else if (category == "services")
            {
                indexModel = await Repository.GetServiceAsync(key);
            }
            else if (category == "tags")
            {
                indexModel = await Repository.GetTagAsync(key);
            }
            else if (category == "themes")
            {
                indexModel = await Repository.GetThemeAsync(key);
            }
            else
            {
                throw new NotImplementedException();
            }

            var model = await GetModel(indexModel, postKind);

            if (model == null)
                return await SearchNotFound();

            ViewBag.Type = postKind;
            return View("CategoryView", model);
        }

        public async Task<ListViewModel> GetModel(IndexModel indexModel, string postKind)
        {
            Category category = indexModel.Index as Category;
            ListViewModel model = category is Ministry ? new MinistryViewModel() : new ListViewModel();
            await LoadAsync(model);

            model.CanonicalUri = category.GetUri();
            model.FeedUri = ProviderHelpers.Uri(category.GetUri(), "feed");
            model.IndexModel = indexModel;
            model.Title = category.Name;
            model.Category = category;

            if (category is Ministry)
                await LoadMinisterData((MinistryViewModel)model, (Ministry)category);

            model.Footer = await GetFooter(category);
            model.LatestPosts = await Repository.GetLatestPostsAsync(indexModel, postKind);

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
                    model.ChildMinistry = (await Repository.GetMinistryAsync(ministry.ChildMinistryKey)).Index as Ministry;
                }

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

        private async Task LoadCategoryPosts(IEnumerable<IndexModel> categoryModels)
        {
            using (Profiler.StepStatic("Get Category Posts"))
            {
                var topFeaturePostKeys = new List<string>();

                foreach (var categoryModel in categoryModels)
                {
                    //TODO: Choose post type based on 'type' supplied to controller.

                    //List<Post> posts = new List<Post>();
                    categoryModel.AddTopPostKeyToLoad(topFeaturePostKeys);
                    categoryModel.AddFeaturePostKeyToLoad(topFeaturePostKeys);

                    //var keys = posts.Select(e => e.Key).Where(e => e != null).ToList();
                    //posts.AddRange(await category.Default.TakeLastAsync(3 - keys.Count(), 0, keys));

                    await Repository.GetLatestPostsAsync(categoryModel, null);

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
                var topFeaturePosts = await Repository.GetPostsAsync(topFeaturePostKeys);
                foreach (var categoryModel in categoryModels)
                {
                    categoryModel.SetTopPost(topFeaturePosts);
                    categoryModel.SetFeaturePost(topFeaturePosts);
                }
            }
        }

        protected async Task<CategoriesViewModel> GetViewModel(string category)
        {
            var model = await Init();

            if (category == "ministries")
            {
                model.Categories = (await Repository.GetMinistriesAsync()).Where(m => ((Ministry)m.Index).ParentMinistryKey == null).OrderByDescending(m => m.Index.Name == "Office of the Premier");
            }
            else if (category == "sectors")
            {
                model.Categories = await Repository.GetSectorsAsync();
            }
            else if (category == "services")
            {
                model.Categories = await Repository.GetServicesAsync();
            }
            else if (category == "themes")
            {
                model.Categories = await Repository.GetThemesAsync();
            }
            else if (category == "tags")
            {
                model.Categories = await Repository.GetTagsAsync();
            }

            model.Title = category.ToUpper()[0] + category.Substring(1);

            await LoadCategoryPosts(model.Categories);

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