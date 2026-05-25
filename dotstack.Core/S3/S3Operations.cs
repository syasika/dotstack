using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using DotStack.Core.Aws;

namespace DotStack.Core.S3;

public record S3Object(string Key, long Size);

public static class S3Operations
{
    public static Task<List<string>> ListBucketsAsync(
        AmazonS3Client client, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.ListBuckets", "s3", async activity =>
        {
            var resp = await client.ListBucketsAsync(ct);
            activity?.SetTag("bucket.count", resp.Buckets.Count);
            return resp.Buckets.Select(b => b.BucketName).ToList();
        });

    public static Task<List<S3Object>> ListObjectsAsync(
        AmazonS3Client client, string bucket, string prefix = "",
        CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.ListObjects", "s3", async activity =>
        {
            activity?.SetTag("bucket", bucket);
            activity?.SetTag("prefix", prefix);

            var request = new ListObjectsV2Request
            {
                BucketName = bucket,
                Prefix = prefix
            };

            var resp = await client.ListObjectsV2Async(request, ct);
            var items = new List<S3Object>();

            foreach (var p in resp.CommonPrefixes)
                items.Add(new S3Object(p, 0));

            foreach (var obj in resp.S3Objects)
                items.Add(new S3Object(obj.Key, obj.Size ?? 0));

            activity?.SetTag("object.count", items.Count);
            return items;
        });

    public static Task CreateBucketAsync(
        AmazonS3Client client, string name, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.CreateBucket", "s3", async activity =>
        {
            activity?.SetTag("bucket", name);
            await client.PutBucketAsync(name, ct);
        });

    public static Task DeleteBucketAsync(
        AmazonS3Client client, string name, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.DeleteBucket", "s3", async activity =>
        {
            activity?.SetTag("bucket", name);
            await client.DeleteBucketAsync(name, ct);
        });

    public static Task EmptyBucketAsync(
        AmazonS3Client client, string bucket, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.EmptyBucket", "s3", async activity =>
        {
            activity?.SetTag("bucket", bucket);

            var listRequest = new ListObjectsV2Request { BucketName = bucket };
            ListObjectsV2Response listResp;
            var totalDeleted = 0;

            do
            {
                listResp = await client.ListObjectsV2Async(listRequest, ct);

                if (listResp.S3Objects.Count > 0)
                {
                    totalDeleted += listResp.S3Objects.Count;
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = bucket,
                        Objects = listResp.S3Objects
                            .Select(o => new KeyVersion { Key = o.Key })
                            .ToList()
                    };

                    var deleteResp = await client.DeleteObjectsAsync(deleteRequest, ct);

                    if (deleteResp.DeleteErrors.Count > 0)
                        throw new InvalidOperationException(
                            $"Failed to delete {deleteResp.DeleteErrors.Count} object(s) from bucket {bucket}");
                }

                listRequest.ContinuationToken = listResp.NextContinuationToken;
            }
            while (listResp.IsTruncated is true);

            activity?.SetTag("deleted.count", totalDeleted);
        });

    public static Task UploadFileAsync(
        AmazonS3Client client, string bucket, string key,
        string localPath, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.UploadFile", "s3", async activity =>
        {
            activity?.SetTag("bucket", bucket);
            activity?.SetTag("key", key);
            activity?.SetTag("local.path", localPath);

            var fileInfo = new FileInfo(localPath);
            activity?.SetTag("file.size", fileInfo.Exists ? fileInfo.Length : -1);

            var fileStream = File.OpenRead(localPath);
            await using (fileStream.ConfigureAwait(false))
            {
                var request = new PutObjectRequest
                {
                    BucketName = bucket,
                    Key = key,
                    InputStream = fileStream
                };
                await client.PutObjectAsync(request, ct);
            }
        });

    public static Task<long> DownloadFileAsync(
        AmazonS3Client client, string bucket, string key,
        string localPath, CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.DownloadFile", "s3", async activity =>
        {
            activity?.SetTag("bucket", bucket);
            activity?.SetTag("key", key);
            activity?.SetTag("local.path", localPath);

            var resp = await client.GetObjectAsync(bucket, key, ct);
            await using (resp.ResponseStream)
            {
                if (localPath is "." or "./")
                    localPath = Path.GetFileName(key);

                var dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var fileStream = File.Create(localPath);
                await using (fileStream.ConfigureAwait(false))
                {
                    await resp.ResponseStream.CopyToAsync(fileStream, ct);
                    var written = fileStream.Length;
                    activity?.SetTag("written.bytes", written);
                    return written;
                }
            }
        });

    public static Task DeleteObjectAsync(
        AmazonS3Client client, string bucket, string key,
        CancellationToken ct = default) =>
        AwsTracing.TraceAsync("S3.DeleteObject", "s3", async activity =>
        {
            activity?.SetTag("bucket", bucket);
            activity?.SetTag("key", key);
            await client.DeleteObjectAsync(bucket, key, ct);
        });
}
