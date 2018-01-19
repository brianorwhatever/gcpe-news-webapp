using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Gov.News.Api.Models;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class MinistriesController : CategoryController
    {
        public MinistriesController(Repository repository, IConfiguration configuration): base(repository, configuration)
        {
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Biography(string key)
        {
            var model = await GetBiography(key);
            if (model == null)
                return await SearchNotFound();

            ViewData.Add("CustomContentClass", "detail");
            ViewData.Add("CustomTitleClass", "ministry");
            return View("Biography", model);
        }

        private async Task<MinisterViewModel> GetBiography(string key)
        {
            Debug.Assert(key != null);

            Ministry index = (await Repository.GetMinistryAsync(key)).Index as Ministry;

            if (index == null)
                return default(MinisterViewModel);

            MinisterViewModel model = new MinisterViewModel();
            model.Minister = await Repository.GetMinisterAsync(index.Key);
            if (model.Minister == null || model.Minister.Headline == "")
                return null;
            
            await LoadAsync(model);
            model.Ministry = index;
            model.Title = model.Minister.Headline;

            model.Footer = await GetFooter(index);

            return model;
        }
    }
}