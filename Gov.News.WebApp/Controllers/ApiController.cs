using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.Website.Controllers
{
    [Route("api")]
    public class ApiController
    {
        [HttpGet("live/playlist")]
        public IEnumerable<string> GetLivePlaylist()
        {
            if (!Hubs.LiveHub.IsWebcasting)
                return new string[0];

            return Hubs.LiveHub.WebcastingPlaylists;
        }
    }
}
