using System;
using System.Threading.Tasks;
using Gov.News.Website.Models;
using Gov.News.Api.Models;

namespace Gov.News.Website.Providers
{
    public static class ProviderHelpers
    {
        public const int MoreNewsItems = 20;
        public const int MaximumSyndicationItems = 10;
        public const int MaximumLatestNewsItems = 3;
        public const int MaximumLatestNewsItemsLoadMore = 10;


        public static Uri Uri(Uri baseUri, string relativeUri)
        {
            return new Uri(baseUri.ToString().TrimEnd('/') + "/" + relativeUri.TrimStart('/'));
        }
        public static string FriendlyTimeSpan(DateTime? time)
        {
            TimeSpan timeSpan = DateTime.UtcNow.Subtract(time.Value);

            if (timeSpan.Days == 0 && timeSpan.Hours < 24)
            {
                if (timeSpan.Hours > 0)
                {
                    return timeSpan.Hours + "h";
                }
                else
                {
                    return timeSpan.Minutes + "m";
                }
            }
            else
            {
                var culture = new System.Globalization.CultureInfo("en-CA");
                return time.Value.ToString("MMM d", culture);
            }
        }
    }
}