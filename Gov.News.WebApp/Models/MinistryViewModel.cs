using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class MinistryViewModel : ListViewModel
    {
        public Ministry Ministry { get; set; }

        public Minister Minister { get; set; }

        public Ministry ChildMinistry { get; set; }

        public Minister ChildMinistryMinister { get; set; }
    }
}