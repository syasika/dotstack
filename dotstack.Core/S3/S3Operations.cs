using System.Diagnostics;
using Amazon.S3;
using Amazon.S3.Model;
using DotStack.Core.Aws;
using DotStack.Core.Telemetry;

namespace DotStack.Core.S3;

public record S3Object(string Key, long Size);

public static class S3Operations
{
    public static async Task<List<string>> ListBucketsAsync(
        AmazonS3Client client, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.ListBuckets");
        activity?.SetTag("service", "s3");
        try
        {
            var resp = await client.ListBucketsAsync(ct);
            activity?.SetTag("bucket.count", resp.Buckets.Count);
            return resp.Buckets.Select(b => b.BucketName).ToList();
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task<List<S3Object>> ListObjectsAsync(
        AmazonS3Client client, string bucket, string prefix = "",
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.ListObjects");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("prefix", prefix);
        try
        {
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
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task CreateBucketAsync(
        AmazonS3Client client, string name, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.CreateBucket");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", name);
        try
        {
            await client.PutBucketAsync(name, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task DeleteBucketAsync(
        AmazonS3Client client, string name, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.DeleteBucket");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", name);
        try
        {
            await client.DeleteBucketAsync(name, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task EmptyBucketAsync(
        AmazonS3Client client, string bucket, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.EmptyBucket");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", bucket);
        try
        {
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
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task UploadFileAsync(
        AmazonS3Client client, string bucket, string key,
        string localPath, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.UploadFile");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);
        activity?.SetTag("local.path", localPath);
        try
        {
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
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task<long> DownloadFileAsync(
        AmazonS3Client client, string bucket, string key,
        string localPath, CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.DownloadFile");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);
        activity?.SetTag("local.path", localPath);
        try
        {
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
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }

    public static async Task DeleteObjectAsync(
        AmazonS3Client client, string bucket, string key,
        CancellationToken ct = default)
    {
        using var activity = ActivitySources.DotStack.StartActivity("S3.DeleteObject");
        activity?.SetTag("service", "s3");
        activity?.SetTag("bucket", bucket);
        activity?.SetTag("key", key);
        try
        {
            await client.DeleteObjectAsync(bucket, key, ct);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw AwsExceptionHelper.ToFriendlyError(ex, "S3");
        }
    }
}
