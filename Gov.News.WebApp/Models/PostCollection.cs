
using Gov.News.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Gov.News.WebApp.Models
{
    public interface IPostCollection
    {
        Task<IEnumerable<Post>> TakeLastAsync(int count);

        Task<IEnumerable<Post>> TakeLastAsync(int count, int skip, List<string> excludeKeys = null);

        Task<IEnumerable<Post>> WhereBetweenAsync(DateTimeOffset start, DateTimeOffset? end = null);

        Task<Uri> GetLatestMediaUri(string mediaType);
    }
}

