using Microsoft.AspNetCore.Html;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Models
{
    public class ContentViewModel : BaseViewModel
    {
        public string Subtitle { get; set; }

        //TEST: Converted from MvcHtmlString
        public HtmlString Html { get; set; }
    }
}