using System;
using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class SyndicationFeedViewModel
    {
        public string Title { get; set; }

        public string AtomId { get; set; }

        public Uri AlternateUri { get; set; }

        public IEnumerable<Post> Entries { get; set; }
    }
}