using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers.Shared
{
    public class NewsroomController : BaseController
    {
        public NewsroomController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        protected async Task<ActionResult> SearchNotFound()
        {
//#if DEBUG
//            return await Task.FromResult(HttpNotFound());
//#else
            string path = Request.Path;

            string query = path.Contains('/') ? path.Substring(path.LastIndexOf('/') + 1) : path;
            query = query.Replace("-", " ");

            ViewBag.Status404NotFound = true;

            return await SearchNotFound(query);
//#endif
        }

        [Noindex]
        private async Task<ActionResult> SearchNotFound(string query)
        {
            var model = await Search(new SearchViewModel.SearchQuery() { Text = query }, 0);

            Response.StatusCode = 404;

            return View("~/Views/Default/SearchView.cshtml", model);
        }

        protected async Task<SearchViewModel> Search(SearchViewModel.SearchQuery query, int first)
        {
            var model = new SearchViewModel();
            int ResultsPerPage = 10;
            model.ResultsPerPage = ResultsPerPage;

            await LoadAsync(model);

#if !DEBUG
            try
            {
#endif
            model.Title = "Search";

            //Google Search Protocol Reference - Request Format
            //http://www.google.com/support/enterprise/static/gsa/docs/admin/72/gsa_doc_set/xml_reference/request_format.html

            string requestPath = Properties.Settings.Default.GoogleSearchApplianceUri.ToString();
            string searchText = "";

            if (!string.IsNullOrEmpty(query.Text))
                searchText = query.Text;
            else if (!string.IsNullOrEmpty(query.DateRange))
                searchText = "bc";  //Assume all stories have "bc" word in them. This is because daterange search by google appliance does not support daterange search without text query

            requestPath += String.Format("&{0}={1}", "q", UrlEncoder.Default.Encode(searchText));
            if (!string.IsNullOrEmpty(query.DateRange))
                requestPath += String.Format("+{0}:{1}", "daterange", query.DateRange);

            requestPath += String.Format("&{0}={1}", "output", "xml_no_dtd");
            requestPath += String.Format("&{0}={1}", "num", Convert.ToString(ResultsPerPage));

            requestPath += String.Format("&{0}={1}", "requiredfields", "MBCTERMS%252EcontentType:News");
            if (!string.IsNullOrEmpty(query.Sector))
                requestPath += String.Format(".{0}:{1}", "NEWSTERMS%252EsectorKeys", query.Sector);

            if (!string.IsNullOrEmpty(query.Ministry))
                requestPath += String.Format(".{0}:{1}", "NEWSTERMS%252EministryKeys", query.Ministry);

            if (!string.IsNullOrEmpty(query.NewsType))
                requestPath += String.Format(".{0}:{1}", "NEWSTERMS%252EcontentPath", query.NewsType);

            //This should match page meta data date format, which is now "yyyy-mm"
            //if (query.YearMonth != null)
            //    requestPath += String.Format("&{0}={1}", "partialfields", "DC%252Edate%252Eissued:" + query.YearMonth);

            requestPath += String.Format("&{0}={1}", "filter", "p");
            requestPath += String.Format("&{0}={1}", "rc", 1);
            //requestPath += String.Format("&{0}={1}", "sitesearch", "news.gov.bc.ca");

            requestPath += String.Format("&{0}={1}", "sort", "date:D:R:d1");

            if (first > 1)
                requestPath += String.Format("&{0}={1}", "start", first - 1);

            System.Xml.Linq.XDocument xml;

            using (Profiler.StepStatic("Calling search.gov.bc.ca"))
            {
                System.Net.WebRequest request = System.Net.WebRequest.Create(requestPath);
                request.Proxy = null;

                using (System.Net.WebResponse response = await request.GetResponseAsync())
                {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(response.GetResponseStream()))
                        xml = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
                }
            }

            model.Count = xml.Element("GSP").Element("RES") == null ? 0 : Convert.ToInt32(xml.Element("GSP").Element("RES").Element("M").Value);

            model.FirstResult = Math.Min(Math.Max(first, 1), (model.Count / ResultsPerPage) * ResultsPerPage + 1);

            model.LastResult = Math.Min(model.FirstResult + ResultsPerPage - 1, model.Count);

            model.Query = query;

            if (!string.IsNullOrEmpty(query.Ministry))
                model.Ministry = (await Repository.GetMinistryAsync(query.Ministry)).Index as Ministry;

            if (!string.IsNullOrEmpty(query.Sector))
                model.Sector = (await Repository.GetSectorAsync(query.Sector)).Index as Sector;

            if (!string.IsNullOrEmpty(query.DateRange))
            {
                var dates = query.DateRange.Replace("..", "+").Split('+');

                if (dates.Count() == 2)
                    model.DateRangeText = string.Format("{0:MMMM d, yyyy} to {1:MMMM d, yyyy}", DateTime.Parse(dates[0]), DateTime.Parse(dates[1]));
            }
            else
            {
                model.DateRangeText = string.Format("{0} to {1}", "March 12, 2011", "Present");
            }


            var results = xml.Element("GSP").Element("RES");

            if (results != null)
            {
                var result = results.Element("R");

                while (result != null)
                {
                    if (result.Name == "R")
                    {
                        string url = result.Element("U").Value;

                        string title = result.Element("T") == null ? "[no title]" : result.Element("T").Value.Replace(" | BC Newsroom", "").Replace(" | BC Gov News", "").Replace(" | BC <b>...</b>", "");
                        string description = result.Element("S").Value.Replace("�", "").Replace("<br>", "<br />").Replace("<br />", " ");

                        string size = result.Element("HAS").Element("C") == null ? "" : result.Element("HAS").Element("C").Attribute("SZ").Value;

                        string localUrl = url.Replace("http://", "").Replace("https://", "");

                        Post post = null;

                        //TODO: Use result["CRAWLDATE"] to set Date

                        try
                        {
                            if (url.StartsWith("https://news.gov.bc.ca/"))
                            {
                                string[] urlFragments = url.Split("/");
                                string postKind = urlFragments[urlFragments.Length - 2];
                                if (postKind == "stories" || postKind == "releases" || postKind == "factsheets" || postKind == "updates")
                                {
                                    post = await Repository.GetPostAsync(urlFragments[urlFragments.Length - 1]);
                                }
                            }
                        }
                        // handle cases where the search has a result not in our database.
                        catch (Exception)
                        {
                        }

                        var searchResult = new Models.SearchViewModel.Result();

                        searchResult.Title = System.Net.WebUtility.HtmlDecode(title.Replace("<b>", "").Replace("</b>", ""));
                        searchResult.Uri = new Uri(url.Replace("https://news.gov.bc.ca/", Properties.Settings.Default.NewsHostUri.ToString()));
                        searchResult.UriLabel = localUrl;
                        searchResult.Description = "";
                        searchResult.HasMediaAssets = false;

                        if (post != null)
                        {
                            searchResult.Title = post.Headline();

                            if (!string.IsNullOrEmpty(post.Summary))
                                searchResult.Description = post.Summary;

                            searchResult.HasMediaAssets = post.HasMediaAssets == true;
                            searchResult.PublishDate = post.PublishDate.HasValue ? post.PublishDate.Value : DateTimeOffset.MinValue;

                            searchResult.ThumbnailUri = post.GetThumbnailUri();
                        }
                        else
                        {
                            if (searchResult.Title.EndsWith(" | BC Gov News"))
                                searchResult.Title = searchResult.Title.Substring(0, searchResult.Title.Length - " | BC Gov News".Length);

                            if (searchResult.Title.EndsWith(" | BC ..."))
                                searchResult.Title = searchResult.Title.Substring(0, searchResult.Title.Length - " | BC ...".Length);
                        }

                        model.Results.Add(searchResult);
                    }

                    result = result.ElementsAfterSelf().FirstOrDefault();
                }
            }


            model.LastPage = Math.Min(Convert.ToInt32(Math.Ceiling((decimal)model.Count / (decimal)ResultsPerPage)), 100);
#if !DEBUG
            }
            catch
            {
                model.Success = false;

                //TODO: Report exception message
            }
#endif

            return model;
        }
        public async Task<System.IO.Stream> GetAzureStream(string blobName)
        {
            var client = new System.Net.Http.HttpClient();

            return await client.GetStreamAsync(new Uri(Repository.ContentDeliveryUri, blobName));
        }

        protected bool NotModifiedSince(DateTimeOffset? timestamp)
        {
            var modifiedSpan = timestamp - Request.GetTypedHeaders().IfModifiedSince;

            // Ignore milliseconds because browsers are not supposed to store them
            if (modifiedSpan.HasValue && modifiedSpan.Value.TotalMilliseconds < 1000)
            {
                return true;
            }
            Response.GetTypedHeaders().LastModified = timestamp;
            return false;
        }
    }
}