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
            var sectorModels = await _repository.GetSectorsAsync();

            IEnumerable<Post> topPosts = await _repository.GetPostsAsync(IndexModel.GetTopPostKeysToLoad(sectorModels));

            foreach (var sectorModel in sectorModels)
            {
                sectorModel.SetTopPost(topPosts);
            }
            return sectorModels;
        }
    }
}
