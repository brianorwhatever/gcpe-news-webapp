using System;
using System.Linq;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class NewslettersController : Shared.IndexController<Home>
    {
        public NewslettersController(Repository repository, IConfiguration configuration)
            : base(repository, configuration)
        {
        }

        public async Task<ActionResult> Index()
        {
            return await NView("Newsletters");
        }

        public async Task<ActionResult> Editions(string newsletterKey)
        {
            return await NView("Editions", newsletterKey);
        }

        public async Task<ActionResult> Edition(string newsletterKey, string editionKey)
        {
            return await NView("Edition", newsletterKey, editionKey);
        }

        public async Task<ActionResult> GetArticle(string newsletterKey, string editionKey, string articleKey)
        {
            return await NView("Article", newsletterKey, editionKey, articleKey);
        }

        public async Task<ActionResult> GetBinaryByGuid(string type, string guid)
        {
            var model = await Repository.GetEditionImageAsync(guid);

            if (model == null)
                return NotFound();

            return GetCachedImage(model.ImageBytes, model.ImageType, model.Timestamp, type == "image" ? null : model.FileName);
        }

        public async Task<ActionResult> NView(string viewName, string newsletterKey = null, string editionKey = null, string articleKey = null)
        {
            var model = new NewsletterViewModel() { Title = viewName };
            model.NewsletterListings = await Repository.GetNewslettersAsync();
            // Newsletters are only cached for 2 minutes so we don't have to take into account updates to the mega-menu(ministries), sidebar(resource links, top sectors), Live Webcast)
            if (NotModifiedSince(Enumerable.Max(model.NewsletterListings, n => n.Timestamp)))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }
            if (newsletterKey != null)
            {
                model.Newsletter = model.NewsletterListings.SingleOrDefault(x => x.Key == newsletterKey);
                if (model.Newsletter == null)
                    return await SearchNotFound();

                if (editionKey != null)
                {
                    string newsHostUri = new Uri(Configuration["NewsHostUri"]).ToString().TrimEnd('/');
                    if (articleKey == null)
                    {
                        model.Edition = await Repository.GetEditionAsync(newsletterKey, editionKey);
                        if (model.Edition == null)
                            return await SearchNotFound();

                        model.Edition.HtmlBody = model.Edition.HtmlBody.Replace("<!--REPLACE-WITH-PUBLIC-URL-->", newsHostUri);
                    }
                    else
                    {
                        model.Article = await Repository.GetArticleAsync(newsletterKey, editionKey, articleKey);

                        if (model.Article == null)
                            return await SearchNotFound();

                        model.Article.HtmlBody = model.Article.HtmlBody.Replace("<!--REPLACE-WITH-PUBLIC-URL-->", newsHostUri);
                    }
                }
            }
            await LoadAsync(model);
            return View(viewName, model);
        }
    }
}