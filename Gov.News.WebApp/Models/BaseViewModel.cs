using System;
using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class BaseViewModel
    {
        public string Title { get; set; }

        public Uri CanonicalUri { get; set; }

        public Uri OGMetaImageUrl { get; set; }

        public ICollection<CategoryModel> Ministries { get; private set; }

        public IEnumerable<ResourceLink> ResourceLinks { get; set; }

        public bool WebcastingLive { get; set;}

        public BaseViewModel()
        {
            OGMetaImageUrl = new Uri(Properties.Settings.Default.NewsHostUri, "/Content/Images/Gov/default-og-meta-image-1.jpg");

            Ministries = new List<CategoryModel>();
        }

        public virtual string SubscribePath()
        {
            return "/subscribe";
        }
    }
}