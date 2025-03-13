using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using MimeKit;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.Storage
{
    public static class AzureStorageExtensions
    {
        static AzureStorageExtensions()
        {
            // Azure 的默认最低 TLS 版本为 TLS 1.2 net45必须手动开启
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
        }

        public static IServiceCollection AddCloudBlobContainer(this IServiceCollection services, string connectionString, string containerName)
        {
            var account = CloudStorageAccount.Parse(connectionString);
            var client = account.CreateCloudBlobClient();
            var container = client.GetContainerReference(containerName);
            services.AddSingleton(container);
            return services;
        }

        public static CloudBlob GetBlob(this CloudBlobContainer container, string filename)
        {
            return container.GetBlobReference(filename);
        }

        public static byte[] GetBytes(this CloudBlobContainer container, string filename)
        {
            var blob = container.GetBlob(filename);
            var bytes = new byte[blob.Properties.Length];
            var length = 1024 * 1024 * 4; // 4m
            var count = 0;
            if (blob.Properties.Length > length)
            {
                while (count < blob.Properties.Length)
                {
                    count += blob.DownloadRangeToByteArray(bytes, count, count, length);
                }
            }
            else
            {
                count = blob.DownloadToByteArray(bytes, 0);
            }
            return bytes;
        }

        public static async Task<byte[]> GetBytesAsync(this CloudBlobContainer container, string filename, CancellationToken cancellation = default(CancellationToken))
        {
            var blob = container.GetBlob(filename);
            if (!blob.Exists()) return null;
            var bytes = new byte[blob.Properties.Length];
            var length = 1024 * 1024 * 4; // 4m
            var count = 0;
            if (blob.Properties.Length > length)
            {
                while (count < blob.Properties.Length)
                {
                    count += await blob.DownloadRangeToByteArrayAsync(bytes, count, count, length, cancellation);
                }
            }
            else
            {
                count = await blob.DownloadToByteArrayAsync(bytes, 0, cancellation);
            }
            return bytes;
        }

        public static CloudBlockBlob GetBlockBlob(this CloudBlobContainer container, string filename)
        {
            return container.GetBlockBlobReference(filename);
        }

        public static CloudBlockBlob Upload(this CloudBlobContainer container, Stream stream, string filename, string mimeType = null)
        {
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename);
            blockBlob.Properties.ContentType = mimeType ?? MimeTypes.GetMimeType(filename);
            blockBlob.UploadFromStream(stream);
            return blockBlob;
        }

        public static CloudBlockBlob Upload(this CloudBlobContainer container, byte[] buffer, string filename, string mimeType = null)
        {
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(filename);
            blockBlob.Properties.ContentType = mimeType ?? MimeTypes.GetMimeType(filename);
            blockBlob.UploadFromByteArray(buffer, 0, buffer.Length);
            return blockBlob;
        }

        public static async Task<CloudBlockBlob> UploadAsync(this CloudBlobContainer container, Stream stream, string blobName, CancellationToken cancellation = default(CancellationToken))
        {
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
            blockBlob.Properties.ContentType = MimeTypes.GetMimeType(blobName);
            await blockBlob.UploadFromStreamAsync(stream, cancellation);
            return blockBlob;
        }

        public static async Task<CloudBlockBlob> UploadAsync(this CloudBlobContainer container, byte[] buffer, string blobName, CancellationToken cancellation = default(CancellationToken))
        {
            CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);
            blockBlob.Properties.ContentType = MimeTypes.GetMimeType(blobName);
            await blockBlob.UploadFromByteArrayAsync(buffer, 0, buffer.Length, cancellation);
            return blockBlob;
        }

        public static void DeleteDirectoty(this CloudBlobContainer container, string directoty)
        {
            foreach (CloudBlockBlob blob in container.GetDirectoryReference(directoty).ListBlobs(true).OfType<CloudBlockBlob>())
            {
                blob.DeleteIfExists();
            }
        }

        public static bool TryGet(this CloudBlobContainer container, string filename, out CloudBlockBlob blockBlob)
        {
            blockBlob = container.GetBlockBlobReference(filename);
            return blockBlob.Exists();
        }

        public static string GetSas(this CloudBlobContainer container, DateTimeOffset? startTime, DateTimeOffset? expiryTime)
        {
            // 配置 Shared Access Policy
            var policy = new SharedAccessBlobPolicy()
            {
                Permissions =
                    SharedAccessBlobPermissions.Read |
                    SharedAccessBlobPermissions.Write |
                    SharedAccessBlobPermissions.Delete |
                    SharedAccessBlobPermissions.List |
                    SharedAccessBlobPermissions.Add |
                    SharedAccessBlobPermissions.Create,
                SharedAccessStartTime = startTime,
                SharedAccessExpiryTime = expiryTime
            };

            // 生成 SAS Token
            var token = container.GetSharedAccessSignature(policy);

            return token;
        }

        public static string GetAbsoluteUri(this CloudBlobContainer container, string relativeUri)
        {
            return container.Uri.ToString().TrimEnd('/') + relativeUri.TrimStart('/');
        }
    }
}