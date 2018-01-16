#define USE_AZURE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;

using Gov.News.Api.Models;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class AssetsController : Shared.NewsroomController
    {
        public AssetsController(Repository repository, IConfiguration configuration): base(repository, configuration)
        {
        }

        public async Task<ActionResult> Single(string path)
        {
            var asset = await Repository.GetAzureAssetAsync(path);

            if (asset == null)
                return await SearchNotFound();

            if (Properties.Settings.Default.NewsMediaHostUri.Host.EndsWith(".blob.core.windows.net"))
            {
                //goto CDN.
                string url = Repository.GetBlobSasUri(asset).ToString();
                return Redirect(url);
            }
            else
            {

#if USE_AZURE
                if (path.Any(e => Path.GetInvalidPathChars().Contains(e)))
                    return await SearchNotFound();

                string fileName = Path.GetFileName(path);
                string contentType;

                if (!new FileExtensionContentTypeProvider().TryGetContentType(fileName, out contentType))
                    contentType = "application/octet-stream";

                var stream = await Repository.GetAzureAssetStream(asset, Configuration);

                if (stream == null)
                    return await SearchNotFound();

                return File(stream, contentType);
#else
                string url = Uri(Properties.Settings.Default.NewsMediaHostUri, "assets/" + asset.Key).ToString();
                return Redirect(url);
#endif
            }
        }

        public ActionResult License()
        {
            return Redirect(Properties.Settings.Default.MediaAssetsLicenseUri.ToString());
        }
    }
}