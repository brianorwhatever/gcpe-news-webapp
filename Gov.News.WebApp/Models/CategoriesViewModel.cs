using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class CategoriesViewModel : BaseViewModel
    {
        public IEnumerable<IndexModel> Categories { get; set; }

        public FooterViewModel Footer { get; set; }
    }
}