#if USE_SIGNALR
using Microsoft.AspNet.SignalR;
# endif
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Microsoft.Extensions.Hosting;

namespace Gov.News.Website.Hubs
{
    public class LiveHub : IHostedService
#if USE_SIGNALR
        : Hub
#endif
    {
        private Task _pollingTask;
        private CancellationTokenSource _cts;
        private Repository _repository;
        private static volatile bool _isWebcasting = false;

        public LiveHub(Repository repository)
        {
            _repository = repository;
        }

        /*public static bool IsWebcasting
        {
            get { return _isWebcasting; }
        }*/

        private static volatile IEnumerable<string> _webcastingPlaylists = null;
        public static IEnumerable<string> WebcastingPlaylists
        {
            get { return _webcastingPlaylists; }
        }

        // checks whether the url passed in is accessible via a http header request
        // if not accessible it returns false
        private static async Task<bool> CheckAlive(string url)
        {
            var request = WebRequest.Create(url);
            request.Method = "GET"; //HEAD was an option until it became unavailable with 405 error
            try
            {
                using (var response = await request.GetResponseAsync())
                {
                    return true;
                }
            }
            catch (WebException)
            {
                /*  This is the expected path if the content is not available.
                    A WebException will be thrown if the status of the response is not `200 OK` */
                return false;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Store the task we're executing
            _pollingTask = StartWebcastingLivePolling(_cts.Token);
            // If the task is completed then return it, otherwise it's running
            return _pollingTask.IsCompleted ? _pollingTask : Task.CompletedTask;
        }

        async Task StartWebcastingLivePolling(CancellationToken ct)
        {
            //TODO: Toggle this feature a better way during application development
            //if (System.Diagnostics.Debugger.IsAttached)
            //    return;

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }
                try
                {
                    var homeSettings= await _repository.GetHomeAsync();
                    var manifest_url_setting = homeSettings.LiveWebcastFlashMediaManifestUrl;
                    if (manifest_url_setting == null)
                    {
                        SetDead();
                        continue;
                    }

                    var m3u_playlist_setting = homeSettings.LiveWebcastM3uPlaylist;
                    if (m3u_playlist_setting == null || !await CheckAlive(m3u_playlist_setting))
                    {
                        SetDead();
                        continue;
                    }

                    SetLive(new List<string>() { manifest_url_setting, m3u_playlist_setting });
                }
                catch
                {
                    try
                    {
                        //MIGRATION: Implement Logging
                        //System.IO.File.AppendAllText(System.Web.Hosting.HostingEnvironment.MapPath("~") + @"\..\Log Files\PollWebcastingLive.log", ex + "\r\n");
                    }
                    catch
                    {
#if DEBUG
                        throw;
#endif
                    }

                }
                finally
                {
                    await Task.Delay(new System.TimeSpan(0, 0, 15), ct);
                }
            }
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop called without start
            if (_pollingTask == null)
            {
                return;
            }

            // Signal cancellation to the executing method
            _cts.Cancel();

            // Wait until the task completes or the stop token triggers
            await Task.WhenAny(_pollingTask, Task.Delay(-1, cancellationToken));

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Call all the clients and let them know there Webcasting is not live
        private static void SetDead()
        {
            _isWebcasting = false;
            _webcastingPlaylists = null;
#if USE_SIGNALR
            if (!Properties.Settings.Default.SignalREnabled)
                return;
            var context = GlobalHost.ConnectionManager.GetHubContext<LiveHub>();
            //TODO: optionally pass the urls for preview or an image for preview
            context.Clients.All.isLive(false, null);
#endif
        }

        // Call all the clients and let them know there Webcasting is live and pass the urls
        private static void SetLive(IEnumerable<string> links)
        {
            _isWebcasting = true;
            _webcastingPlaylists = links;
#if USE_SIGNALR
            if (!Properties.Settings.Default.SignalREnabled)
                return;
            var context = GlobalHost.ConnectionManager.GetHubContext<LiveHub>();
            context.Clients.All.isLive(true, links);
#endif
        }
    }
}