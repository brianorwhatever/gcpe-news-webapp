using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Properties
{
    //This class is to support the migration from MVC 5 to MVC Core 1.1; it will be
    //replaced with IOptions or another convention through dependancy injection.
    public class Settings
    {
        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings();
        }

        public Uri NewsHostUri { get; set; }

        public Uri NewsMediaHostUri { get; set; }

        public string NewsMediaProxyKey { get; set; }

#if USE_SIGNALR
        public string SignalREnabled { get; set; } = "true";
#endif
        public string SignalREnabled { get; } = "false";

        public string EnableDynamicFooter { get; set; } = "false";

        public Uri GoogleSearchApplianceUri { get; set; }

        public DateTimeOffset RssFeedCutoffDate { get; set; }

        public string MediaAssetsLicenseUri { get; set; }

        public string GoogleSiteVerification { get; set; }

        public string BingSiteVerification { get; set; }

        public Uri CalendarUri { get; set; }

        public Uri ContactUri { get; set; }

        public string ContentFilesUnc { get; set; }

        public bool EnableExceptionReporting { get; set; }

        public string LocalDataModelCacheUnc { get; set; }

        public Uri ArchiveHostUri { get; set; }

        public Uri NewslettersHostUri { get; set; }

        public Uri NewsroomHostUri { get; set; }
    }
}
