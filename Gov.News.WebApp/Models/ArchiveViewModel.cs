using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Models
{
    public class ArchiveViewModel : MinistryViewModel
    {
        public IOrderedEnumerable<KeyValuePair<DateTime, int>> ArchiveMonths { get; set; }
    }
}