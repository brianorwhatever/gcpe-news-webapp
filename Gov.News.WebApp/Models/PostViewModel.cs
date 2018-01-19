using System;
using System.Collections.Generic;
using System.Linq;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class PostViewModel : BaseViewModel
    {
        public Post Post { get; private set; }
        public Uri PostThumbnailUri { get; set; }

        public PostViewModel(Post post)
        {
            Post = post;
            PostThumbnailUri = post.GetThumbnailUri();
            CanonicalUri = post.GetUri();
            Title = post.Headline();
        }

        public string ContentType
        {
            get
            {
                if (Post.Kind == "stories" || Post.Kind == "releases" || Post.Kind == "factsheets")
                {
                    return "News";
                }
                else if (Post.Kind == "updates")
                {
                    return "Update";
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public IndexModel LeadMinistry { get; set; }

        public IEnumerable<Post> RelatedArticles { get; set; }

        //TODO: Replace FacebookAsset with abstract SuperAsset
        public FacebookPost FacebookAsset { get; set; }

        public FooterViewModel Footer { get; set; }
        public Minister Minister { get; set; }

        public IDictionary<string, FacebookPost> FacebookPostDetailsDictionary { get; set; }

        public IEnumerable<string> RelatedMinistryKeys { get; set; }

        public IEnumerable<string> RelatedSectorKeys { get; set; }

        //TODO: Implement Tags meta for GSA
        //public IEnumerable<Tag> Tags { get; set; }

        //TODO: Implement Themes meta for GSA
        //public IEnumerable<Theme> Themes { get; set; }

        public string ContentPath
        {
            get
            {
                return Post.Kind;
            }
        }

        public override string SubscribePath()
        {
            return "/subscribe?ministries="
                + string.Join("&ministries=", RelatedMinistryKeys)
                + "&sectors="
                + string.Join("&sectors=", RelatedSectorKeys);
        }

        public string ProxyUrl()
        {
            if (Properties.Settings.Default.NewsMediaHostUri != null)
            {
                return Properties.Settings.Default.NewsMediaHostUri.ToString() + "embed/";
            }

            return new Uri("https://media.news.gov.bc.ca/embed/").ToString();
        }
    }
}