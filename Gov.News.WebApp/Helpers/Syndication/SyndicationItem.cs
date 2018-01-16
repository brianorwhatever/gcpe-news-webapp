using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Helpers.Syndication
{
    public class SyndicationItem
    {
        public string Id { get; set; }

        public TextSyndicationContent Title { get; set; }

        public TextSyndicationContent Summary { get; set; }

        public DateTimeOffset PublishDate { get; set; }

        public ICollection<SyndicationLink> Links { get; } = new List<SyndicationLink>();

        public ICollection<SyndicationPerson> Authors { get; } = new List<SyndicationPerson>();

        public ICollection<SyndicationCategory> Categories { get; } = new List<SyndicationCategory>();

        public DateTimeOffset LastUpdatedTime { get; set; }
    }
}
