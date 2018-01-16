using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Helpers.Syndication
{
    public class TextSyndicationContent
    {
        public string Text { get; private set; }

        public TextSyndicationContent(string text)
        {
            Text = text;
        }
    }
}
