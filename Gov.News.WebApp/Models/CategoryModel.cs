using System;
using System.Collections.Generic;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class CategoryModel
    {
        public CategoryModel(Category category, Post topPost, Post featurePost = null)
        {
            Category = category;
            TopPost = topPost;
            TopPostThumbnailUri = topPost?.GetThumbnailUri();
            FeaturePost = featurePost;
            LatestNews = new List<Post>();
        }

        public Category Category { get; private set; }

        public Post TopPost { get; private set; }

        public Uri TopPostThumbnailUri { get; set; }

        public Post FeaturePost { get; set; }

        public ICollection<Post> LatestNews { get; private set; }

        public FooterViewModel Footer { get; set; }
    }
}