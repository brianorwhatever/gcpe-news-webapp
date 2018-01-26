using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Api.Models;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class NewslettersController : Shared.IndexController<Home>
    {
        public NewslettersController(Repository repository, IConfiguration configuration): base(repository, configuration)
        {
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex]
        public async Task<ActionResult> Index()
        {
            var model = await GetNewsletterModel();
            return View("Newsletters", model);
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Editions(string newsletterKey)
        {
            var model = await GetEditionsModel(newsletterKey);

            if (model == null)
                return await SearchNotFound();

            return View("Editions", model);
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Edition(string newsletterKey, string editionKey)
        {
            var model = await GetEditionModel(newsletterKey, editionKey);

            if (model == null)
                return await SearchNotFound();

            return View("Edition", model);
        }

        public async Task<ActionResult> GetBinaryByGuid(string type, string guid)
        {
            var model = await Repository.GetEditionImageAsync(guid);

            if (model == null)
                return NotFound();
            
            return GetCachedImage(model.ImageBytes, model.ImageType, model.Timestamp, type == "image" ? null : model.FileName);
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> GetArticle(string newletterKey, string editionKey, string articleKey)
        {
            var model = await GetArticleModel(newletterKey, editionKey, articleKey);

            if (model == null)
                return await SearchNotFound();

            return View("Article", model);
        }

        public async Task<NewsletterViewModel> GetNewsletterModel()
        {
            var model = await GetBaseModel();
            model.Title = "Newsletters";
            return model;
        }

        public async Task<NewsletterViewModel> GetEditionsModel(string newsletterKey)
        {
            var model = await GetBaseModel(newsletterKey);
            model.Title = "Editions";

            return model;
        }

        public async Task<NewsletterViewModel> GetEditionModel(string newsletterKey, string editionKey)
        {
            var model = await GetBaseModel(newsletterKey);
            model.Title = "Editions";

            model.Edition = await Repository.GetEditionAsync(newsletterKey, editionKey);
            if (model.Edition == null)
                return null;

            model.Edition.HtmlBody = model.Edition.HtmlBody.Replace("<!--REPLACE-WITH-PUBLIC-URL-->", new Uri(Configuration["NewsHostUri"]).ToString().TrimEnd('/'));
            return model;
        }

        public async Task<NewsletterViewModel> GetArticleModel(string newsletterKey, string editionKey, string articleKey)
        {
            var model = await GetBaseModel(newsletterKey);
            model.Title = "Articles";

            var articleModel = await Repository.GetArticleAsync(newsletterKey, editionKey, articleKey);

            if (articleModel == null)
                return null;

            articleModel.HtmlBody = articleModel.HtmlBody.Replace("<!--REPLACE-WITH-PUBLIC-URL-->", new Uri(Configuration["NewsHostUri"]).ToString().TrimEnd('/'));

            model.Article = articleModel;
            return model;
        }

        private async Task<NewsletterViewModel> GetBaseModel(string newsletterKey = null)
        {
            var model = new NewsletterViewModel();
            await LoadAsync(model);
            model.NewsletterListings = await Repository.GetNewslettersAsync();
            if (newsletterKey != null)
            {
                model.Newsletter = model.NewsletterListings.SingleOrDefault(x => x.Key == newsletterKey);
                if (model.Newsletter == null)
                    return null;
            }
            return model;
        }
    }
}