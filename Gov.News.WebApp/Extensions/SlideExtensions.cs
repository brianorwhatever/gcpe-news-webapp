using Gov.News.Api;
using Gov.News.Api.Models;
using System;
using System.Threading.Tasks;
using Gov.News.Website;

namespace Gov.News.WebApp.Extensions
{
    public static class SlideExtensions
    {
        public static Uri GetActionUri(this Slide slide)
        {
            return new Uri(slide.ActionUri);
        }

        public static Uri GetFacebookPostUri(this Slide slide)
        {
            return new Uri(slide.FacebookPostUri);
        }

    }
}
    