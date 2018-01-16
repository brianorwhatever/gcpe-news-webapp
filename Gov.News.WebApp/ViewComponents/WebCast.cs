using Microsoft.AspNetCore.Mvc;
using Gov.News.Website.Hubs;

namespace ViewComponentSample.ViewComponents
{
    public class WebCastViewComponent : ViewComponent
    {
        public WebCastViewComponent()
        {
        }

        public IViewComponentResult Invoke()
        {
            var webcastingPlaylists = LiveHub.WebcastingPlaylists;
            return View("LiveVideoPlayer", webcastingPlaylists);
        }
    }
}
