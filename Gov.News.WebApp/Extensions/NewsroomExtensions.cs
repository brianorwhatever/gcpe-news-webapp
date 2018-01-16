using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Gov.News.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Gov.News.Website
{
    public static class NewsroomExtensions
    {
        private static Uri AppendUriSegment(Uri uri, string segment)
        {
            if (uri.AbsolutePath.EndsWith("/"))
            {
                return new Uri(uri, segment);
            }
            else
            {
                var baseUri = new Uri(uri.ToString() + "/");
                return new Uri(baseUri, segment);
            }
        }

        public static Uri GetThumbnailUri(this Post post)
        {
            if (post.FacebookPictureUri != null)
            {
                return new Uri(post.FacebookPictureUri);
            }
            Uri assetUri;
            if (!Uri.TryCreate(post.AssetUrl, UriKind.Absolute, out assetUri)) return null;

            Uri thumbnailUri = null;

            if (assetUri.Host == "www.youtube.com")
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(assetUri.Query);

                if (query.ContainsKey("v"))
                {
                    var videoId = query["v"];

                    var youtubeIframeUrl = string.Format("//www.youtube.com/embed/{0}?rel=0&amp;modestbranding=1&amp;wmode=transparent", videoId);

                    thumbnailUri = new Uri(string.Format("https://img.youtube.com/vi/{0}/0.jpg", videoId));
                }
            }
            else if (assetUri.Host.EndsWith("staticflickr.com"))
            {
                var flickrRegex = new Regex(@"https?:\/\/farm([0-9]+)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)_([a-z]\.jpg)");

                var flickrMatch = flickrRegex.Match(assetUri.ToString());

                if (flickrMatch.Success)
                {
                    // Get the "n" size flickr image (320x320)
                    //assetUrl = assetUrl.Replace(flickrMatch.Groups[5].Value, "n.jpg");
                    //TODO: var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[3].Value);

                    thumbnailUri = assetUri;
                }
            }
            else if (post.AssetUrl.ToLower() == "https://news.gov.bc.ca/live")
            {
                thumbnailUri = new Uri("https://news.gov.bc.ca/Content/Images/Gov/Live_Webcast.png");
            }
            return thumbnailUri;
        }

        public static Uri GetUri(this Post post)
        {
            return GetPostUri(post.Kind, post.Key);
        }

        public static Uri GetPostUri(string postKind, string postKey)
        {
            Uri uri = AppendUriSegment(Properties.Settings.Default.NewsHostUri, postKind);

            return AppendUriSegment(uri, UrlEncoder.Default.Encode(postKey));
        }

        public static Uri GetMinisterUri(this Ministry ministry)
        {
            var uri = Properties.Settings.Default.NewsHostUri;

            if (ministry.Key != "office-of-the-premier")
                uri = AppendUriSegment(uri, "ministries");

            uri = AppendUriSegment(uri, ministry.Key);

            uri = AppendUriSegment(uri, "biography");

            return uri;
        }

        public static Uri GetUri(this AzureAsset asset)
        {
            var uri = Properties.Settings.Default.NewsHostUri;

            uri = AppendUriSegment(uri, "assets");

            uri = AppendUriSegment(uri, asset.Key.Substring(0, asset.Key.LastIndexOf('/') + 1));

            uri = AppendUriSegment(uri, asset.Label); // Label has the correct casing

            return uri;
        }

        public static Uri GetUri(this Category category)
        {
            var uri = Properties.Settings.Default.NewsHostUri;

            if (category.Kind == "ministries")
            {
                if (category.Key != "office-of-the-premier")
                {
                    uri = AppendUriSegment(uri, "ministries");

                    var parentMinistryKey = ((Ministry)category).ParentMinistryKey;
                    if (parentMinistryKey != null)
                    {
                        uri = AppendUriSegment(uri, UrlEncoder.Default.Encode(parentMinistryKey));
                    }
                }
            }
            else if (category.Kind == "tags")
            {
                if (category.Key == "speeches")
                {
                    uri = AppendUriSegment(uri, "office-of-the-premier");
                }
                else
                {
                    uri = AppendUriSegment(uri, "tags");
                }
            }
            else
            {
                uri = AppendUriSegment(uri, category.Kind);
            }

            uri = AppendUriSegment(uri, UrlEncoder.Default.Encode(category.Key));

            return uri;
        }

        public static Uri GetUri(this FlickrAsset flickrAsset)
        {
            return new Uri(String.Format("https://www.flickr.com/photos/{0}/{1}/",
                "bcgovphotos",
                flickrAsset.PhotoSecret));
        }

        public static Uri GetResourceUri(this FlickrAsset flickrAsset)
        {
            return new Uri(String.Format("https://farm{0}.staticflickr.com/{1}/{2}_{3}_n.jpg",
                flickrAsset.PhotoFarm,
                flickrAsset.PhotoServer,
                flickrAsset.PhotoSecret,
                flickrAsset.PhotoUserPathAlias));
        }

        public static Uri GetPermanentUri(this Post entry)
        {
            if (entry.Reference.StartsWith("NEWS-"))
                return AppendUriSegment(Properties.Settings.Default.NewsHostUri, entry.Reference.Substring(5));

            return entry.GetUri();
        }

        public static string Headline(this Post post)
        {
            return post.Documents.First().Headline;
        }

        public static string GetShortSummary(this Post entry, int? count)
        {
            //TODO: Determine correct way to handle an empty Summary
            string[] words = entry.Summary.Split();
            string shortSummary = entry.Summary;
            if (count != null || words.Count() > count)
            {
                //Find the end of sentence
                int index = (int)count;
                for(; index < words.Count(); index ++)
                {
                    if(words[index].EndsWith("."))
                    {
                        break;
                    }
                }
                if (index >= words.Count())
                    index = words.Count() - 1;
                shortSummary = string.Join(" ", words.Take(index + 1));
                if (!words[index].EndsWith("."))
                {
                    shortSummary += "...";
                }
            }
            return shortSummary;
        }

        public static  CloudBlob GetBlobReference(this AzureAsset azureAsset, IConfiguration Configuration)
        {
            // Get the asset container URI from configuration.
            Uri assetsContainerUri = new Uri(Configuration["NewsAssetsContainer"]);

            if (assetsContainerUri == null)
                throw new InvalidOperationException();

            CloudBlobContainer container = new CloudBlobContainer(assetsContainerUri);

            return container.GetBlockBlobReference(azureAsset.Key);
        }


        public static Uri ToUri(this string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return null;
            }
            return new Uri(uri);
        }
    }
}