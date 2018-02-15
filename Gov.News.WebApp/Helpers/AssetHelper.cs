using Gov.News.Api.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Gov.News.Website.Helpers
{
    public static class AssetHelper
    {
        public static readonly Regex AssetRegex = new Regex("<asset>(?<url>[^<]+)</asset>");

        private static string ReturnMediaAssetWrapper(string mediaProvider, string mediaId, string mediaUrl = "")
        {
            System.Text.StringBuilder wrapper = new System.Text.StringBuilder();
            Uri youtubeImageUri = null;
            string mediaProviderUrl = "";
            string privacyUrl = "http://news.gov.bc.ca/privacy";
            string placeholderThumbnailUrl = "/Content/Images/Gov/BC_Gov_News_1280x720.png";
            string mediaType = "";

            switch (mediaProvider)
            {
                case "facebook":
                    mediaProviderUrl = "facebook.com";
                    mediaType = "video";
                    break;
                case "soundcloud":
                    mediaProviderUrl = "soundcloud.com";
                    mediaType = "audio";
                    break;
                case "youtube":
                    mediaProviderUrl = "youtube.com";
                    mediaType = "video";
                    break;
            }

            wrapper.AppendFormat("<div id=\"media-wrapper\" class=\"{0}-wrapper asset {0} {1}\" data-media-type=\"{0}\" data-media-id=\"{2}\">", mediaProvider, mediaType, mediaId);
            wrapper.Append("<div class=\"media-player-container\">");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"placeholder-container\">");
            if (mediaProvider == "youtube")
            {
                youtubeImageUri = new Uri(string.Format("https://img.youtube.com/vi/{0}/maxresdefault.jpg", mediaId));
                wrapper.AppendFormat("<img src=\"{0}\" onError=\"this.onerror=null; this.src='{1}';\"/>", youtubeImageUri.ToProxyUrl(), placeholderThumbnailUrl);
            }
            else if (mediaProvider == "facebook")
            {
                wrapper.AppendFormat("<img id='placeholder-image'; src=\"{0}\" onError=\"this.onerror=null; this.src='{1}';\"/>", mediaUrl, placeholderThumbnailUrl);
            }
            wrapper.Append("<div class=\"overlay-container\">");
            wrapper.Append("<div class=\"outer\">");
            wrapper.Append("<div class=\"inner not-expanded\">");
            wrapper.Append("<div class=\"play-button\">");
            wrapper.Append("<a href=\"javascript:void(0);\" title=\"Play\"></a>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"play-instructions\">");
            wrapper.Append("<div class=\"preface\">");
            wrapper.AppendFormat("Press play again to access content from <strong>{0}</strong>. For more information, please read our <a href=\"{1}\">Privacy</a> statement.", mediaProviderUrl, privacyUrl);
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"controls\">");
            wrapper.Append("<div>");
            wrapper.Append("<label>");
            wrapper.Append("<span>");
            wrapper.Append("<input type=\"checkbox\" value=\"1\" class=\"save-preference\" />");
            wrapper.Append("</span>");
            wrapper.AppendFormat("Always allow content from <strong>{0}</strong>", mediaProviderUrl);
            wrapper.Append("</label>");
            wrapper.Append("</div>");
            wrapper.Append("<div>");
            wrapper.Append("<span>Your preference will be saved using cookies.</span>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"play-close\">");
            wrapper.Append("<a href=\"javascript:void(0);\" title=\"Close\"></a>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"clear\"></div>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"clear\"></div>");
            wrapper.Append("</div>");

            return wrapper.ToString();
        }

        public static HtmlString RenderFacebookPostAsset(string keyUrl, Uri pictureUri)
        {
            var facebookRegex = new Regex(@"videos/");
            var facebookMatch = facebookRegex.Match(keyUrl);
            string result = "";

            if (facebookMatch.Success)
            {
                string videoId = "";
                Uri uri = new Uri(keyUrl);

                if (uri.Segments.Length > 0)
                {
                    foreach (string segment in uri.Segments)
                    {
                        if (segment != "/")
                        {
                            videoId += segment;
                        }
                    }

                    if (videoId.Length > 0)
                    {
                        videoId = videoId.Substring(0, videoId.Length - 1);
                    }
                }

                result = ReturnMediaAssetWrapper("facebook", videoId, pictureUri.ToProxyUrl());
            }
            else
            {
                result = string.Format("<a href=\"{0}\"><img src=\"{1}\" alt=\"Article Image\" /></a>", keyUrl, pictureUri.ToProxyUrl());
            }

            return new HtmlString(result);
        }

        public static HtmlString RenderAssetsInHtml(string bodyHtml, int? maxWidth = null, IDictionary<string, FacebookPost> facebookDetailsDictionary = null)
        {
            var newhtml = AssetRegex.Replace(bodyHtml, new MatchEvaluator(match =>
            {
                string result;

                string url = match.Groups["url"].Value;

                try
                {
                    Uri uri = new Uri(url);
                    var width = maxWidth ?? 304;

                    if (uri.Host == "www.youtube.com")
                    {
                        var height = maxWidth * 9 / 15;
                        var query = QueryHelpers.ParseQuery(uri.Query);

                        if (query.ContainsKey("v"))
                        {
                            var videoId = query["v"];

                            result = ReturnMediaAssetWrapper("youtube", videoId);
                        }
                        else
                        {
                            result = "";
                        }
                    }
                    else if (uri.Host.EndsWith("staticflickr.com"))
                    {

                        var flickrRegex = new Regex(@"https?:\/\/farm([0-9]+)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)(_[a-z])?\.jpg");
                        var flickrMatch = flickrRegex.Match(url);

                        if (flickrMatch.Success)
                        {
                            var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[3].Value);
                            result = string.Format(
                                         "<div>" +
                                              "<a href='{0}'>" +
                                                  "<img src='{1}'/>" +
                                              "</a>" +
                                         "</div>"
                                         , flickrUrl, uri.ToProxyUrl());
                        }
                        else
                        {
                            result = string.Format(
                                         "<div>" +
                                         "<img src='{0}'/>" +
                                         "</div>"
                                         , uri.ToProxyUrl());
                        }
                    }
                    else if (uri.Host == "w.soundcloud.com")
                    {
                        var query = WebUtility.UrlDecode(uri.Query);
                        //Alternate: uri.Query.Replace("%2F", "/");

                        var soundcloudRegex = new Regex(@"tracks/(\d+)&");
                        var soundcloudMatch = soundcloudRegex.Match(query);

                        if (soundcloudMatch.Success)
                        {
                            var mediaId = soundcloudMatch.Groups[1].Value;
                            result = ReturnMediaAssetWrapper("soundcloud", mediaId);
                        }
                        else
                        {
                            result = "";
                        }
                    }
                    else if (uri.Host == "www.facebook.com")
                    {
                        if (facebookDetailsDictionary != null)
                        {
                            var facebookPost = facebookDetailsDictionary.FirstOrDefault(d => d.Key == url).Value;
                            if (facebookPost != null)
                                result = GetEmbeddedFacebookHtmlString(facebookPost);
                            else
                                result = string.Format(
                                            "<div>" +
                                                "<div class='fb-post' data-href='{0}'></div>" +
                                            "</div>"
                                            , url);
                        }
                        else
                        {
                            result = string.Format(
                                            "<div>" +
                                                "<div class='fb-post' data-href='{0}'></div>" +
                                            "</div>"
                                            , url);
                        }

                    }
                    else
                    {
                        result = "<a href='" + url + "'>" + url + "</a>";
                    }
                }
                catch (UriFormatException)
                {
                    result = "<!--" + url + "-->";
                }

                return "<!--googleoff: all-->" + result + "<!--googleon: all-->";
            }));

            return new HtmlString(newhtml);
        }

        [Obsolete]
        public static HtmlString RenderPostAssetThumbnail(Uri uri, bool renderFlickrAsBackground = false)
        {
            string assetHtml = "";

            try
            {
                if (uri == null)
                    return HtmlString.Empty;

                if (uri.Host == "www.youtube.com")
                {

                    var query = QueryHelpers.ParseQuery(uri.Query);

                    if (!query.ContainsKey("v"))
                        return HtmlString.Empty;

                    var videoId = query["v"];

                    var youtubeIframeUrl = string.Format("//www.youtube.com/embed/{0}?rel=0&amp;modestbranding=1&amp;wmode=transparent", videoId);
                    var imgUri = new Uri(string.Format("https://img.youtube.com/vi/{0}/0.jpg", videoId));

                    assetHtml = string.Format(
                                        "<div class='asset youtube'>" +
                                            "<a href='https://www.youtube.com/watch?v={0}'>" +
                                                "<img src='{1}'/>" +
                                            "</a>" +
                                        "</div>"
                                        , videoId, imgUri.ToProxyUrl());
                }
                else if (uri.Host.EndsWith("staticflickr.com"))
                {
                    var flickrRegex = new Regex(@"https?:\/\/farm([0-9]+)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)(_[a-z])?\.jpg");
                    var flickrMatch = flickrRegex.Match(uri.ToString());

                    if (flickrMatch.Success)
                    {
                        // Get the "n" size flickr image (320x320)
                        //assetUrl = assetUrl.Replace(flickrMatch.Groups[5].Value, "n.jpg");
                        var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[3].Value);
                        if (renderFlickrAsBackground)
                        {
                            assetHtml = string.Format(
                                            "<div class='asset flickr background'>" +
                                                    // "<a href='{0}'>" +
                                                    "<div class='image-div' style='background-image: url({1})'></div>" +
                                            //  "</a>" +
                                            "</div>"
                                            , flickrUrl, uri.ToProxyUrl());
                        }
                        else
                        {
                            assetHtml = string.Format(
                                            "<div class='asset flickr'>" +
                                                   // "<a href='{0}'>" +
                                                   "<img src='{1}'/>" +
                                            // "</a>" +
                                            "</div>"
                                            , flickrUrl, uri.ToProxyUrl());
                        }
                    }
                    else
                    {
                        assetHtml = string.Format(
                                        "<div>" +
                                        "<img src='{0}'/>" +
                                        "</div>"
                                        , uri.ToProxyUrl());
                    }
                }
            }
            catch (UriFormatException)
            {
                assetHtml = "<!--" + uri.ToString() + "-->";
            }

            return new HtmlString(assetHtml);
        }

        public static HtmlString RenderPostAsset(Uri uri, int? maxWidth = null)
        {
            //    <div class="facebook-post">
            //    <img src="@Url.Content("~/Content/Images/Placeholder/" + Model.FacebookPost.PictureUrl)" alt="Article Image"/>
            //    <div class="social-media-bar">
            //        <ul>
            //            <li class="facebook-info">Like, Comment or Share this story on Facebook</li>
            //        </ul>
            //    </div>
            //    <div class="facebook-article">
            //        <div class="feed-header">
            //            <a href="#" class="link-button facebook-like">Like on Facebook</a>
            //            <h5>
            //                <img src="@Url.Content("~/Content/Images/SocialMedia/" + Model.FacebookPost.PosterLogo)" />
            //                <a href="#">@Model.FacebookPost.Poster</a><br/>
            //                <span>@Model.FacebookPost.PosterSubtitle &#9679; @string.Format("{0:n0}", Model.FacebookPost.PosterLikes) Likes</span>
            //            </h5>
            //        </div>

            //        @Model.FacebookPost.Content.AsHtmlParagraphs()

            //        <div class="post-details">@Model.FacebookPost.PostLikes Likes &#9679; @Model.FacebookPost.PostComments Comment &#9679; @Model.FacebookPost.PostShares Share</div>
            //        <div class="sharing-options">
            //            <ul>
            //                <li class="facebook-action facebook-like"><a href="#">Like</a></li>
            //                <li class="facebook-action facebook-comment"><a href="#">Comment</a></li>
            //                <li class="facebook-action facebook-share"><a href="#">Share</a></li>
            //            </ul>
            //        </div>
            //    </div>
            //</div>

            string assetHtml = "";

            try
            {
                if (uri == null)
                    return HtmlString.Empty;

                if (uri.Host == "www.youtube.com")
                {
                    var width = maxWidth ?? 304;
                    var height = width * 9 / 16; // 16:9 aspect ratio
                    var query = QueryHelpers.ParseQuery(uri.Query);

                    if (!query.ContainsKey("v"))
                        return HtmlString.Empty;

                    var videoId = query["v"];

                    assetHtml = ReturnMediaAssetWrapper("youtube", videoId);

                }
                else if (uri.Host.EndsWith("staticflickr.com"))
                {

                    var flickrRegex = new Regex(@"https?:\/\/farm([0-9]+)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)(_[a-z])?\.jpg");
                    var flickrMatch = flickrRegex.Match(uri.ToString());

                    if (flickrMatch.Success)
                    {
                        var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[3].Value);
                        assetHtml = string.Format(
                                        "<div class='asset flickr'>" +
                                            "<a href='{0}'>" +
                                                "<img src='{1}'/>" +
                                            "</a>" +
                                        "</div>"
                                        , flickrUrl, uri.ToProxyUrl());
                    }
                    else
                    {

                        assetHtml = string.Format(
                                       "<div>" +
                                       "<img src='{0}'/>" +
                                       "</div>"
                                       , uri.ToProxyUrl());
                    }
                }
                else if (uri.Host == "w.soundcloud.com")
                {
                    var soundcloudRegex = new Regex(@"tracks/(\d+)&");
                    var soundcloudMatch = soundcloudRegex.Match(uri.ToString());

                    if (soundcloudMatch.Success)
                    {
                        var mediaId = soundcloudMatch.Groups[1].Value;
                        assetHtml = ReturnMediaAssetWrapper("soundcloud", mediaId);
                    }
                    else
                    {
                        assetHtml = "";
                    }
                }
                else if (uri.Host == "www.facebook.com")
                {
                    assetHtml = string.Format(
                                            "<div class='asset facebook'>" +
                                                "<div class='fb-post' data-href='{0}'></div>" +
                                            "</div>"
                                            , uri.ToString());
                }
            }
            catch (UriFormatException)
            {
                assetHtml = "<!--" + uri.ToString() + "-->";
            }

            return new HtmlString(assetHtml);
        }

        public static HtmlString RenderEmbeddedFacebookAsset(FacebookPost facebookPost)
        {
            return new HtmlString(GetEmbeddedFacebookHtmlString(facebookPost));
        }

        static string GetEmbeddedFacebookHtmlString(FacebookPost facebookPost)
        {
            return "<div class='feature-post' style='display: block;'>" +
                        "<div class='facebook-post' style='position: relative;'>" +
                            "<div class='facebook-article'>" +
                                "<div class='feed-header'>" +
                                    "<a href='" + facebookPost.PosterUrl() + "' target='_blank' class='link-button facebook-like'>Like on Facebook</a>" +
                                        "<h5>" +
                                             "<img src='" + facebookPost.PosterLogo.ToUri().ToProxyUrl() + "' />" +
                                             facebookPost.Poster + "<br />" +

                                            "<span class='post-details' style='padding:0'>" + facebookPost.PosterSubtitle + " &#9679;" + string.Format("{0:n0}", facebookPost.PosterLikes) + " Likes</span>" +
                                        "</h5>" +
                                    "</div>" +
                                    "<div class='clearfix'></div>" +

                                    "<div style='float:left; margin:10px;display:none;'>" +
                                        "<img src='" + facebookPost.PosterLogo.ToUri().ToProxyUrl() + "' /><br />" +
                                        "<span class='post-details' style='padding-left:0; padding-top:5px;'>" + facebookPost.PosterSubtitle + "</span>" + /*@* TODO &#9679; @string.Format("{0:n0}", Model.FacebookPost.PosterLikes) Likes*@*/
                                    "</div>" +
                                    //(facebookPost.Type == "photo" && !string.IsNullOrWhiteSpace(facebookPost.PictureUrl) ? "<img src='" + facebookPost.PostImageFileName + "'/><br />" : "") +
                                    ConvertUrlsToLinks(facebookPost.Content).Replace("\n", "<br />").AsHtmlParagraphs() +

                                    "<div class='clearfix'></div>" +
                                    "\n<div class='sharing-options'><ul>" +
                                        /*  <li class="facebook-action facebook-like" data-facebook-object-id="facebookPost.FacebookObjectId"><a href = "#" > Like </ a ></ li >
                                            < li class="facebook-action facebook-comment"><a href = "#" > Comment </ a ></ li >
                                                < li class="facebook-action facebook-share" data-url="facebookPost.PosterUrl()"><a href = "#" > Share </ a ></ li >
                                                </ ul >
                                            </ div > *@*/
                                        /* "<div class='facebook-comment-dialog'>" +
                                             "<textarea id='facebook-comment-message-1' placeholder='Comment on this story'></textarea>" +
                                             "<input type='submit' class='facebook-comment-trigger' data-slider-id='1' data-facebook-object-id='" + facebookPost.FacebookObjectId + "' value='Comment' />" +
                                             "<a class='facebook-comment-close' href='#'>Close</a>" */
                                        "<li class='facebook-info'><a href = '" + facebookPost.Key + "' target='_blank'>Like or Comment<span class='on-story'> on this post</span><span class='on-facebook'> on Facebook</span></a></li>" +
                                    "</ul ></div >" +
                                "</div>" +
                            "</div>" +
                        "</div>";
        }
        public static string ConvertUrlsToLinks(string txt)
        {
            //Regex regx = new Regex("(https?://|www\\.)([\\w+?\\.\\w+])+([a-zA-Z0-9\\~\\!\\@\\#\\$\\%\\^\\&amp;\\*\\(\\)_\\-\\=\\+\\/\\?\\.\\:\\;\\'\\,]*)?", RegexOptions.IgnoreCase);

            string pattern = @"((https?:\/\/\w+)|www)" //Matches "http://host", "https://host" or "www"
                           + @"(\.[\w\-]+)*"           //Matches zero or more occurances of ".intermediate-host"
                           + @"\.\w+"                  //Matches ".tld"
                           + @"(\/[\w\-]*)*";          //Matches zero or more occurances of "/" or "/path-segment"

            Regex regx = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection mactches = regx.Matches(txt);

            foreach (Match match in mactches)
            {
                Regex facebookLink = new Regex("https://www.facebook.com", RegexOptions.IgnoreCase);
                MatchCollection facebookMatches = facebookLink.Matches(match.Value);
                Regex brokenLinkWithEllipsis = new Regex(".*\\.\\.\\.$");
                MatchCollection brokenLinkMatches = brokenLinkWithEllipsis.Matches(match.Value);
                if (facebookMatches.Count > 0 || (brokenLinkMatches.Count > 0))
                {
                    continue;
                }
                txt = txt.Replace(match.Value, "<a href='" + new UriBuilder(match.Value).Uri.ToString() + "'>" + match.Value + "</a>");
            }
            return txt;
        }
    }
}
