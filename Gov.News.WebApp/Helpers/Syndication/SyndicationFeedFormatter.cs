using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace Gov.News.Website.Helpers.Syndication
{
    public abstract class SyndicationFeedFormatter
    {
        protected readonly SyndicationFeed feed;

        public SyndicationFeedFormatter(SyndicationFeed feed)
        {
            this.feed = feed;
        }

        public abstract void WriteTo(System.Xml.XmlWriter writer);
    }

    public class Atom10FeedFormatter : SyndicationFeedFormatter
    {
        public Atom10FeedFormatter(SyndicationFeed feed) : base(feed)
        {
        }

        public override void WriteTo(XmlWriter writer)
        {
            writer.WriteStartDocument();

            writer.WriteStartElement("feed", "http://www.w3.org/2005/Atom");
            {
                writer.WriteStartElement("title");
                {
                    writer.WriteAttributeString("type", "text");

                    writer.WriteString(feed.Title.Text);
                }
                writer.WriteEndElement();

                writer.WriteElementString("id", feed.Id);
                
                writer.WriteElementString("updated", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssK"));
                
                foreach(var link in feed.Links)
                {
                    writer.WriteStartElement("link");
                    {
                        writer.WriteAttributeString("rel", link.RelationshipType);
                        writer.WriteAttributeString("type", link.MediaType);
                        writer.WriteAttributeString("href", link.Uri.ToString());
                    }
                    writer.WriteEndElement();
                }

                foreach (var item in feed.Items)
                {
                    writer.WriteStartElement("entry");
                    {
                        writer.WriteElementString("id", item.Id);

                        writer.WriteStartElement("title");
                        {
                            writer.WriteAttributeString("type", "text");

                            writer.WriteString(item.Title.Text);
                        }
                        writer.WriteEndElement();

                        writer.WriteStartElement("summary");
                        {
                            writer.WriteAttributeString("type", "text");

                            writer.WriteString(item.Summary.Text);
                        }
                        writer.WriteEndElement();

                        writer.WriteElementString("published", item.PublishDate.ToString("yyyy-MM-ddTHH:mm:ssK"));

                        writer.WriteElementString("updated", item.LastUpdatedTime.ToString("yyyy-MM-ddTHH:mm:ssK"));

                        foreach (var author in item.Authors)
                        {
                            writer.WriteStartElement("author");
                            {
                                writer.WriteElementString("name", author.Name);
                            }
                            writer.WriteEndElement();
                        }

                        foreach (var link in item.Links)
                        {
                            writer.WriteStartElement("link");
                            {
                                writer.WriteAttributeString("rel", link.RelationshipType.ToString());

                                writer.WriteAttributeString("type", link.MediaType.ToString());

                                if (link.Length > 0)
                                {
                                    writer.WriteAttributeString("length", link.Length.ToString());
                                }

                                writer.WriteAttributeString("href", link.Uri.ToString());
                            }
                            writer.WriteEndElement();
                        }

                        foreach (var category in item.Categories)
                        {
                            writer.WriteStartElement("category");
                            {
                                writer.WriteAttributeString("term", category.Name);
                            }
                            writer.WriteEndElement();
                        }
                    }
                    writer.WriteEndElement();
                }
            }
            writer.WriteEndElement();
        }
    }

    public class Rss20FeedFormatter : SyndicationFeedFormatter
    {
        public Rss20FeedFormatter(SyndicationFeed feed) : base(feed)
        {
        }

        public override void WriteTo(XmlWriter writer)
        {
            writer.WriteStartDocument();

            writer.WriteStartElement("rss");
            {
                writer.WriteAttributeString("xmlns", "a10", null, "http://www.w3.org/2005/Atom");
                writer.WriteAttributeString("version", "2.0");

                writer.WriteStartElement("channel");
                {
                    writer.WriteElementString("title", feed.Title.Text);

                    var alternateLink = feed.Links.SingleOrDefault(e => e.RelationshipType == "alternate");

                    if (alternateLink != null)
                        writer.WriteElementString("link", alternateLink.Uri.ToString());

                    writer.WriteElementString("description", feed.Description);

                    writer.WriteElementString("a10", "id", null, feed.Id);

                    foreach (var link in feed.Links.Where(e => e.RelationshipType != "alternate"))
                    {
                        writer.WriteStartElement("a10", "link", null);
                        {
                            writer.WriteAttributeString("rel", link.RelationshipType);
                            writer.WriteAttributeString("type", link.MediaType);
                            writer.WriteAttributeString("href", link.Uri.ToString());
                        }
                        writer.WriteEndElement();
                    }

                    foreach (var item in feed.Items)
                    {
                        writer.WriteStartElement("item");
                        {
                            writer.WriteStartElement("guid");
                            {
                                writer.WriteAttributeString("isPermaLink", "false");

                                writer.WriteString(item.Id);
                            }
                            writer.WriteEndElement();

                            writer.WriteElementString("link", item.Links.Single(e => e.RelationshipType == "alternate").Uri.ToString());

                            foreach (var author in item.Authors)
                            {
                                writer.WriteStartElement("a10", "author", null);
                                {
                                    writer.WriteElementString("a10", "name", null, author.Name);
                                }
                                writer.WriteEndElement();
                            }

                            foreach (var category in item.Categories)
                            {
                                writer.WriteElementString("category", category.Name);
                            }

                            writer.WriteElementString("title", item.Title.Text);

                            writer.WriteElementString("description", item.Summary.Text);

                            writer.WriteElementString("pubDate", item.PublishDate.ToString("ddd, dd MMM yyyy HH:mm:ss") + " " + item.PublishDate.ToString("zzz").Replace(":", ""));
                            
                            foreach (var link in item.Links.Where(e => e.RelationshipType != "alternate"))
                            {
                                writer.WriteStartElement("enclosure");
                                {
                                    writer.WriteAttributeString("url", link.Uri.ToString());

                                    writer.WriteAttributeString("type", link.MediaType);

                                    if (link.Length > 0)
                                        writer.WriteAttributeString("length", link.Length.ToString());
                                }
                                writer.WriteEndElement();
                            }
                        }
                        writer.WriteEndElement();
                    }
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
    }
}
