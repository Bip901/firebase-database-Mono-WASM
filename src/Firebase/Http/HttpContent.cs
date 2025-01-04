using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Firebase.Database.Http
{
    public abstract class HttpContent : IDisposable
    {
        public abstract Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken = default);

        public async Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default)
        {
            using (StreamReader reader = new StreamReader(await ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), Encoding.UTF8))
            {
                return await reader.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        public abstract void Dispose();
    }
}
