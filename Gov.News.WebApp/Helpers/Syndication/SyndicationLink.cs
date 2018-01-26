using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Helpers.Syndication
{
    public class SyndicationLink
    {
        public Uri Uri { get; set; }

        public string RelationshipType { get; set; }

        public string Title { get; set; }

        public string MediaType { get; set; }

        public long Length { get; set; }

        public SyndicationLink(Uri uri, string relationshipType, string title, string mediaType, long length)
        {
            Uri = uri;
            RelationshipType = relationshipType;
            Title = title;
            MediaType = mediaType;
            Length = length;
        }
    }
}
