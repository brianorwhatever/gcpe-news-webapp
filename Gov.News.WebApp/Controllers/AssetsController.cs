#define USE_AZURE

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class AssetsController : Shared.NewsroomController
    {
        public AssetsController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        public async Task<ActionResult> Single(string path)
        {
#if USE_AZURE
            // Return search not found if the path is null or has invalid characters.
            if (path == null || path.Any(e => Path.GetInvalidPathChars().Contains(e)))
                return await SearchNotFound();

            string fileName = Path.GetFileName(path);
            string contentType;

            if (!new FileExtensionContentTypeProvider().TryGetContentType(fileName, out contentType))
                contentType = "application/octet-stream";

            try
            {
                var stream = await GetAzureStream("assets/" + path);
                return File(stream, contentType);
            }
            catch
            {
                return await SearchNotFound();
            }

#else
            string url = Uri(Properties.Settings.Default.NewsMediaHostUri, path).ToString();
            return Redirect(url);
#endif
        }

        public ActionResult License()
        {
            return Redirect(Properties.Settings.Default.MediaAssetsLicenseUri.ToString());
        }
    }
}