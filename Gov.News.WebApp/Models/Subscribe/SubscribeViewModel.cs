using System;
using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models.Subscribe
{
    public class SubscribeViewModel : BaseViewModel
    {
        public Guid? Token { get; set; }

        public IEnumerable<Newsletter> Newsletters { get; set; }

        public IEnumerable<IndexModel> Sectors { get; set; }

        public IEnumerable<IndexModel> Services { get; set; }

        public IEnumerable<KeyValuePair2> Tags { get; set; }

        public IEnumerable<KeyValuePair2> Emergencies { get; set; }

        public SelectionModel Selection { get; set; }

        public bool ShowNews { get; set; }

        public bool ShowServices { get; set; }

        public string Display { get; set; }
    }
}