using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Helpers.Syndication
{
    public class SyndicationFeed
    {
        public IEnumerable<SyndicationItem> Items { get; private set; }

        public SyndicationFeed(IEnumerable<SyndicationItem> items)
        {
            this.Items = items;
        }

        public string Id { get; set; }

        public TextSyndicationContent Title { get; set; }

        public ICollection<SyndicationLink> Links { get; } = new List<SyndicationLink>();

        public string Description { get; set; }
    }
}
