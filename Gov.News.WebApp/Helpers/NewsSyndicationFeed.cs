using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Helpers.Syndication;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Mvc;

namespace Gov.News.Website
{
    public class NewsSyndicationFeed
    {
        public SyndicationFeedViewModel Model { get; private set; }

        public string Format { get; private set; }

        private readonly Repository _repository;

        public NewsSyndicationFeed(string format, SyndicationFeedViewModel model, Repository repository)
        {
            Model = model;
            _repository = repository;

            if (string.IsNullOrEmpty(format))
                Format = "rss2";
            else if (format == "atom" || format == "rss2")
                Format = format;
            else
                throw new ArgumentException("The syndication format must be \"atom\" or \"rss2\".", "format");
        }

        public async Task<string> GetContentAsync(ControllerContext context, bool newsOnDemand, bool resetRssFeeds)
        {
            List<SyndicationItem> items = new List<SyndicationItem>();

            foreach (var e in Model.Entries)
            {
                if (newsOnDemand && !(bool)e.IsNewsOnDemand)
                    continue;

                SyndicationItem item = await ToSyndicationItem(e);

                if (resetRssFeeds)
                {
                    if (Format != "rss2" || item.PublishDate > Website.Properties.Settings.Default.RssFeedCutoffDate)
                    {
                        items.Add(item);
                    }
                }
                else
                {
                    items.Add(item);
                }
            }

            SyndicationFeed feed = new SyndicationFeed(items);

            var request = context.HttpContext.Request;
            feed.Id = string.Concat(request.Scheme, "://", request.Host.ToUriComponent(), request.PathBase.ToUriComponent(), request.Path.ToUriComponent()).ToLowerInvariant(); //Excluding request.QueryString.ToUriComponent()
            Uri feedUri = new Uri(feed.Id);

            feed.Title = new TextSyndicationContent((string.IsNullOrEmpty(Model.Title) ? "" : Model.Title + " | ") + "BC Gov News");

            //feed.AttributeExtensions.Add(new System.Xml.XmlQualifiedName("treatAs", "http://www.microsoft.com/schemas/rss/core/2005"), "list");

            if (Model.AlternateUri != null)
                feed.Links.Add(new SyndicationLink(Model.AlternateUri, "alternate", null, "text/html", 0));

            SyndicationFeedFormatter formatter;

            if (Format == "atom")
            {
                //context.HttpContext.Response.ContentType = "application/atom+xml";

                feed.Links.Add(new SyndicationLink(feedUri, "self", null, "application/atom+xml", 0));

                formatter = new Atom10FeedFormatter(feed);
            }
            else if (Format == "rss2")
            {
                //context.HttpContext.Response.ContentType = "application/rss+xml";

                feed.Links.Add(new SyndicationLink(feedUri, "self", null, "application/rss+xml", 0));

                formatter = new Rss20FeedFormatter(feed);
            }
            else
            {
                throw new NotImplementedException();
            }

            using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
            {
                using (System.Xml.XmlWriter writer = System.Xml.XmlWriter.Create(stream, new System.Xml.XmlWriterSettings() { Indent = true }))
                {
                    formatter.WriteTo(writer);
                }

                return System.Text.UTF8Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private async Task<SyndicationItem> ToSyndicationItem(Post entry)
        {
            SyndicationItem item = new SyndicationItem();

            item.Id = entry.AtomId;

            item.Title = new TextSyndicationContent(entry.Headline());

            item.Summary = new TextSyndicationContent(entry.Summary);

            SyndicationPerson author = new SyndicationPerson();
            author.Name = "Province of BC";
            item.Authors.Add(author);

            if (Format == "atom")
            {
                item.LastUpdatedTime = (DateTimeOffset) entry.PublishDate;
            }

            var ministry = (await _repository.GetMinistryAsync(entry.LeadMinistryKey))?.Index as Category;

            if (ministry != null)
                item.Categories.Add(new SyndicationCategory(ministry.Name));

            Uri itemUri = entry.GetUri();
            item.Links.Add(new SyndicationLink(itemUri, "alternate", "", "text/html", 0));

            var thumbnailUri = entry.GetThumbnailUri();
            if (thumbnailUri != null)
            {
                if (thumbnailUri.Host.EndsWith("staticflickr.com"))
                {
                    //TODO: No name for asset
                    Asset flickrAsset = await _repository.GetFlickrAssetAsync(entry.AssetUrl);
                    item.Links.Add(new SyndicationLink(thumbnailUri, "enclosure", "", "image/jpeg", (long)flickrAsset.Length));
                }
                else
                {
                    //TODO: Get meta information for image from Graph API
                    item.Links.Add(new SyndicationLink(thumbnailUri, "enclosure", "", "image/jpeg", 0));
                }
            }

            item.PublishDate = (DateTimeOffset) entry.PublishDate;

            return item;
        }
    }
}