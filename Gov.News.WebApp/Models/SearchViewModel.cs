using Gov.News.Api.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Models
{
    public class SearchViewModel : BaseViewModel
    {
        public bool Success { get; set; }

        public int Count { get; set; }

        public int FirstResult { get; set; }

        public int LastResult { get; set; }

        public int ResultsPerPage { get; set; }

        public SearchQuery Query { get; set; }

        public string UrlQueryString(int first = 0)
        {
            var parameters = new List<string>();

            if (Query.Text != null)
                parameters.Add(string.Format("q={0}", Query.Text));

            if (Query.DateRange != null)
                parameters.Add(string.Format("date={0}", Query.DateRange));

            if (Query.NewsType != null)
                parameters.Add(string.Format("content={0}", Query.NewsType));

            if (Query.Ministry != null)
                parameters.Add(string.Format("ministry={0}", Query.Ministry));

            if (Query.Sector != null)
                parameters.Add(string.Format("sector={0}", Query.Sector));

            if (first > 0)
                parameters.Add(string.Format("first={0}", first));

            return parameters.Count > 0 ? "?" + string.Join("&", parameters) : "";
        }

        public List<Result> Results { get; private set; }

        public int Page
        {
            get { return (FirstResult / ResultsPerPage) + 1; }
        }

        public int LastPage { get; set; }


        public Ministry Ministry {get; set;}
        public Sector Sector { get; set; }
        public string DateRangeText { get; set; }
        public SearchViewModel()
        {
            Success = true;
            Results = new List<Result>();
        }

        public class Result
        {
            public string Title { get; set; }

            public Uri Uri { get; set; }

            public string UriLabel { get; set; }

            public string Description { get; set; }

            public bool HasMediaAssets { get; set; }

            public Uri ThumbnailUri { get; set; }

            public DateTimeOffset PublishDate { get; set; }
        }

        public class SearchQuery
        {
            public string Text { get; set; }
            public string Ministry { get; set; }
            public string Sector { get; set; }
            public string NewsType { get; set; }
            public string DateRange { get; set; } //format like 2015-08-01..2015-08-22
        }
    }
}