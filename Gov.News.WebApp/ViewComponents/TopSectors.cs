using System.Collections.Generic;
using System.Threading.Tasks;
using Gov.News.Website;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Mvc;

namespace ViewComponentSample.ViewComponents
{
    public class TopSectorsViewComponent : ViewComponent
    {
        protected Repository _repository;

        public TopSectorsViewComponent(Repository repository)
        {
            _repository = repository;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var items = await GetItemsAsync();
            return View("TopSectors", items);
        }
        private async Task<ICollection<CategoryModel>> GetItemsAsync()
        {
            var sectors = await _repository.GetSectorsAsync(false);

            var sectorModels = new List<CategoryModel>();
            foreach (var sector in sectors)
            {
                var sectorModel = new CategoryModel(sector, await _repository.GetPostAsync(sector.TopPostKey));
                sectorModels.Add(sectorModel);
            }
            return sectorModels;
        }
    }
}
