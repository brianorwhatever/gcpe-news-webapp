using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Gov.News.Api.Models;

namespace Gov.News.Website.Models
{
    public class MinisterViewModel : BaseViewModel
    {
        public Minister Minister { get; set; }

        public Ministry Ministry { get; set; }

        public FooterViewModel Footer { get; set; }
    }
}