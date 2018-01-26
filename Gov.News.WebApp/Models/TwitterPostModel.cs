using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Models
{
    public class TwitterPostModel
    {
        public string UserName { get; set; }
        public string ScreenName { get; set; }
        public Uri UserImageUri { get; set; }
        public string TimeAgo { get; set; }
        public string Content { get; set; }
        public string Posted { get; set; }
        public string RetweetedText { get; set; }
    }
}