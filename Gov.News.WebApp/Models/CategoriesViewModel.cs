using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class CategoriesViewModel : BaseViewModel
    {
        public IEnumerable<Category> Categories { get; set; }

        public IDictionary<Category, IEnumerable<Post>> CategoryPosts { get; set; }

        public FooterViewModel Footer { get; set; }
    }
}