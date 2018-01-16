using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Models
{
    public class FooterViewModel
    {
        public Uri LatestFlickrUri { get; set; }

        public Uri FlickrMoreUri { get; set; }

        public Uri LatestYoutubeUri { get; set; }

        public Uri YoutubeMoreUri { get; set; }

        public Uri LatestSoundcloudUri { get; set; }

        public Uri SoundcloudMoreUri { get; set; }

        public string FlickrSource { get; set; }

        public string YoutubeSource { get; set; }

        public string SoundcloudSource { get; set; }
    }
}