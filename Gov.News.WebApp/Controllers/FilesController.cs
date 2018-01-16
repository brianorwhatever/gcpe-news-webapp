#define USE_AZURE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;

namespace Gov.News.Website.Controllers
{
    public class FilesController : Shared.NewsroomController
    {
        public FilesController(Repository repository, IConfiguration configuration): base(repository, configuration)
        {
        }
        
#if USE_AZURE
        public async Task<ActionResult> Single(string path)
        {
            //Microsoft Word calls with "Microsoft-WebDAV-MiniRedir/6.1.7601" user agent when opening a Word document from corporate network.
            if (string.IsNullOrEmpty(path))
                return StatusCode(StatusCodes.Status403Forbidden);

            if (path.Any(e => Path.GetInvalidPathChars().Contains(e)))
                return await SearchNotFound();

            string extension = Path.GetExtension(path);

            string localPath = Path.GetFullPath(Path.Combine(Properties.Settings.Default.ContentFilesUnc, path));

            //Verify that file is not outside of the "files" folder.
            if (!localPath.StartsWith(Path.Combine(Properties.Settings.Default.ContentFilesUnc).TrimEnd('\\') + "\\"))
                return await SearchNotFound();

            string contentType;
            if (!new FileExtensionContentTypeProvider().TryGetContentType(localPath, out contentType))
                contentType = "application/octet-stream";

            var stream = await Repository.GetAzureFileStream(path.ToLower(), Configuration);
            if (stream == null)
                stream = await Repository.GetAzureFileStream(path, Configuration); // for backwards compatibility

            if (stream == null)
                return await SearchNotFound();

            return File(stream, contentType);
        }
#else
        public async Task<ActionResult> Single(string path)
        {
            //Microsoft Word calls with "Microsoft-WebDAV-MiniRedir/6.1.7601" user agent when opening a Word document from corporate network.
            if (string.IsNullOrEmpty(path))
                return StatusCode(StatusCodes.Status403Forbidden);

            if (path.Any(e => Path.GetInvalidPathChars().Contains(e)))
                return await SearchNotFound();

            string extension = Path.GetExtension(path);

            string localPath = Path.GetFullPath(Path.Combine(Properties.Settings.Default.ContentFilesUnc, path));

            //Verify that file is not outside of the "files" folder.
            if (!localPath.StartsWith(Path.Combine(Properties.Settings.Default.ContentFilesUnc).TrimEnd('\\') + "\\"))
                return await SearchNotFound();

            string contentType;
            if(!new FileExtensionContentTypeProvider().TryGetContentType(localPath, out contentType))
                contentType = "application/octet-stream";

            if (System.IO.File.Exists(localPath))
            {
                //var file = System.IO.File.ReadAllBytes(localPath);
                //return File(file, contentType);

                return PhysicalFile(localPath, contentType);
            }

            return await SearchNotFound();
        }
#endif
    }
}