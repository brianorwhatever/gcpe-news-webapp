using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Models.Subscribe
{
    public class SelectionModel
    {
        public string EmailAddress { get; set; }

        public IEnumerable<string> Services { get; set; }

        public IEnumerable<string> Ministries { get; set; }

        public IEnumerable<string> Sectors { get; set; }

        public IEnumerable<string> Tags { get; set; }

        public IEnumerable<string> Emergency { get; set; }

        public IEnumerable<string> Newsletters { get; set; }

        public string MediaDistributionLists { get; set; }

        public bool NewsAsItHappens { get; set; }

        public bool NewsDailyDigest { get; set; }

        public bool Contains(string category, string key)
        {
            if (category == "ministries")
                return Ministries != null && Ministries.Contains(key);

            if (category == "sectors")
                return Sectors != null && Sectors.Contains(key);

            if (category == "tags")
                return Tags != null && Tags.Contains(key);

            if (category == "services")
                return Services != null && Services.Contains(key);

            if (category == "newsletters")
                return Newsletters != null && Newsletters.Contains(key);

            if (category == "emergency")
                return Emergency != null && Emergency.Contains(key);

            return false;
        }

        public bool Any()
        {
            return (Ministries?.Any() ?? false) || (Sectors?.Any() ?? false) || (Tags?.Any() ?? false) || (Services?.Any() ?? false) || (Newsletters?.Any() ?? false) || (Emergency?.Any() ?? false);
        }
    }
}