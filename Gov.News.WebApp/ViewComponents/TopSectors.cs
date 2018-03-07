using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gov.News.Api.Models;
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
        private async Task<IEnumerable<IndexModel>> GetItemsAsync()
        {
            var sectors = await _repository.GetSectorsAsync();

            IEnumerable<Post> loadedPosts = await _repository.GetPostsAsync(IndexModel.GetTopPostKeys(sectors));

            var sectorModels = new List<IndexModel>();
            foreach (var sector in sectors)
            {
                var sectorModel = new IndexModel(sector);
                sectorModel.SetTopPost(loadedPosts);
                sectorModels.Add(sectorModel);
            }
            return sectorModels;
        }
    }
}
