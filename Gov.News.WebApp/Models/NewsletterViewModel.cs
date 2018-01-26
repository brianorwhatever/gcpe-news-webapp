using System.Collections.Generic;
using System.Linq;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class NewsletterViewModel : BaseViewModel
    {
        public IEnumerable<Newsletter> NewsletterListings { get; set; }
        public Newsletter Newsletter { get; set; }
        public Edition Edition { get; set; }
        public Article Article { get; set; }

        public override string SubscribePath()
        {
            var subscribeString = "/subscribe?newsletters=";

            if (Newsletter != null)
                subscribeString +=  Newsletter.Key;

            return subscribeString;
        }
    }
}