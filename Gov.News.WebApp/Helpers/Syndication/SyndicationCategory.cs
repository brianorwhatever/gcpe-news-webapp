using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Helpers.Syndication
{
    public class SyndicationCategory
    {
        public string Name { get; set; }

        public SyndicationCategory(string name)
        {
            Name = name;
        }
    }
}
