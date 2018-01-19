using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class HomeViewModel : ListViewModel
    {
        public IEnumerable<Slide> SlideItems { get; set; }
        public Calendar CalendarModel { get; set; }
    }
}