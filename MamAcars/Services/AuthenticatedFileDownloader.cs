using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Velopack.Sources;

namespace MamAcars.Services
{
    public class AuthenticatedFileDownloader : IFileDownloader
    {
        private readonly IFileDownloader _inner;
        private readonly string _token;

        public AuthenticatedFileDownloader(string token)
        {
            _inner = new HttpClientFileDownloader();
            _token = token;
        }

        private IDictionary<string, string> AddAuth(IDictionary<string, string>? headers)
        {
            var result = headers != null
                ? new Dictionary<string, string>(headers)
                : new Dictionary<string, string>();
            result["Authorization"] = $"Bearer {_token}";
            return result;
        }

        public Task DownloadFile(string url, string targetFile, Action<int> progress,
            IDictionary<string, string> headers, double timeout, CancellationToken ct)
            => _inner.DownloadFile(url, targetFile, progress, AddAuth(headers), timeout, ct);

        public Task<byte[]> DownloadBytes(string url, IDictionary<string, string> headers, double timeout)
            => _inner.DownloadBytes(url, AddAuth(headers), timeout);

        public Task<string> DownloadString(string url, IDictionary<string, string> headers, double timeout)
            => _inner.DownloadString(url, AddAuth(headers), timeout);
    }
}
