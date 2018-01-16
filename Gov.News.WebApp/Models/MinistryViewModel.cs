using Gov.News.Api.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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