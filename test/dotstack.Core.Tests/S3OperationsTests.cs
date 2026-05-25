using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using DotStack.Core.S3;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace DotStack.Core.Tests;

public class S3OperationsTests
{
    [Fact]
    public async Task ListBucketsAsync_returns_bucket_names()
    {
        var fake = A.Fake<IAmazonS3>();
        var resp = new ListBucketsResponse
        {
            Buckets =
            [
                new S3Bucket { BucketName = "bucket-one" },
                new S3Bucket { BucketName = "bucket-two" },
            ],
        };
        A.CallTo(() => fake.ListBucketsAsync(A<CancellationToken>._)).Returns(resp);

        var result = await S3Operations.ListBucketsAsync(fake);

        result.ShouldBe(["bucket-one", "bucket-two"]);
    }

    [Fact]
    public async Task CreateBucketAsync_calls_PutBucket()
    {
        var fake = A.Fake<IAmazonS3>();

        await S3Operations.CreateBucketAsync(fake, "test-bucket");

        A.CallTo(() => fake.PutBucketAsync("test-bucket", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteBucketAsync_calls_DeleteBucket()
    {
        var fake = A.Fake<IAmazonS3>();

        await S3Operations.DeleteBucketAsync(fake, "test-bucket");

        A.CallTo(() => fake.DeleteBucketAsync("test-bucket", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task DeleteObjectAsync_calls_DeleteObject()
    {
        var fake = A.Fake<IAmazonS3>();

        await S3Operations.DeleteObjectAsync(fake, "bucket", "some/key.txt");

        A.CallTo(() => fake.DeleteObjectAsync("bucket", "some/key.txt", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task ListObjectsAsync_returns_objects_and_prefixes()
    {
        var fake = A.Fake<IAmazonS3>();
        var request = new ListObjectsV2Request { BucketName = "bucket", Prefix = "prefix/" };
        var resp = new ListObjectsV2Response
        {
            CommonPrefixes = ["prefix/sub/", "prefix/other/"],
            S3Objects =
            [
                new Amazon.S3.Model.S3Object { Key = "prefix/file.txt", Size = 100 },
                new Amazon.S3.Model.S3Object { Key = "prefix/photo.jpg", Size = 2048 },
            ],
        };

        A.CallTo(() =>
                fake.ListObjectsV2Async(
                    A<ListObjectsV2Request>.That.Matches(r =>
                        r.BucketName == "bucket" && r.Prefix == "prefix/"
                    ),
                    A<CancellationToken>._
                )
            )
            .Returns(resp);

        var result = await S3Operations.ListObjectsAsync(fake, "bucket", "prefix/");

        result.ShouldBe([
            new Core.S3.S3Object("prefix/sub/", 0),
            new Core.S3.S3Object("prefix/other/", 0),
            new Core.S3.S3Object("prefix/file.txt", 100),
            new Core.S3.S3Object("prefix/photo.jpg", 2048),
        ]);
    }

    [Fact]
    public async Task EmptyBucketAsync_deletes_all_objects()
    {
        var fake = A.Fake<IAmazonS3>();

        // First page: 2 objects, truncated
        var page1 = new ListObjectsV2Response
        {
            S3Objects =
            [
                new Amazon.S3.Model.S3Object { Key = "a.txt" },
                new Amazon.S3.Model.S3Object { Key = "b.txt" },
            ],
            IsTruncated = true,
            NextContinuationToken = "token1",
        };

        // Second page: 1 object, not truncated
        var page2 = new ListObjectsV2Response
        {
            S3Objects = [new Amazon.S3.Model.S3Object { Key = "c.txt" }],
            IsTruncated = false,
        };

        var deleteOk = new DeleteObjectsResponse
        {
            DeletedObjects = [new DeletedObject(), new DeletedObject()],
        };
        var deleteOk2 = new DeleteObjectsResponse { DeletedObjects = [new DeletedObject()] };

        A.CallTo(() =>
                fake.ListObjectsV2Async(
                    A<ListObjectsV2Request>.That.Matches(r => r.BucketName == "bucket"),
                    A<CancellationToken>._
                )
            )
            .ReturnsNextFromSequence(page1, page2);

        A.CallTo(() =>
                fake.DeleteObjectsAsync(
                    A<DeleteObjectsRequest>.That.Matches(r => r.BucketName == "bucket"),
                    A<CancellationToken>._
                )
            )
            .ReturnsNextFromSequence(deleteOk, deleteOk2);

        await S3Operations.EmptyBucketAsync(fake, "bucket");

        A.CallTo(() => fake.ListObjectsV2Async(A<ListObjectsV2Request>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();

        A.CallTo(() => fake.DeleteObjectsAsync(A<DeleteObjectsRequest>._, A<CancellationToken>._))
            .MustHaveHappenedTwiceExactly();
    }

    [Fact]
    public async Task EmptyBucketAsync_throws_on_delete_errors()
    {
        var fake = A.Fake<IAmazonS3>();

        var page = new ListObjectsV2Response
        {
            S3Objects = [new Amazon.S3.Model.S3Object { Key = "a.txt" }],
            IsTruncated = false,
        };

        var deleteResp = new DeleteObjectsResponse
        {
            DeleteErrors = [new DeleteError { Key = "a.txt", Code = "AccessDenied" }],
        };

        A.CallTo(() => fake.ListObjectsV2Async(A<ListObjectsV2Request>._, A<CancellationToken>._))
            .Returns(page);

        A.CallTo(() => fake.DeleteObjectsAsync(A<DeleteObjectsRequest>._, A<CancellationToken>._))
            .Returns(deleteResp);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            S3Operations.EmptyBucketAsync(fake, "bucket")
        );
    }

    [Fact]
    public async Task UploadFileAsync_puts_object_from_local_path()
    {
        var fake = A.Fake<IAmazonS3>();
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            await File.WriteAllTextAsync(tempFile, "hello world");

            await S3Operations.UploadFileAsync(fake, "bucket", "remote/key.txt", tempFile);

            A.CallTo(() =>
                    fake.PutObjectAsync(
                        A<PutObjectRequest>.That.Matches(r =>
                            r.BucketName == "bucket" && r.Key == "remote/key.txt"
                        ),
                        A<CancellationToken>._
                    )
                )
                .MustHaveHappenedOnceExactly();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ListBucketsAsync_returns_empty_when_buckets_null()
    {
        var fake = A.Fake<IAmazonS3>();
        A.CallTo(() => fake.ListBucketsAsync(A<CancellationToken>._))
            .Returns(new ListBucketsResponse { Buckets = null! });

        var result = await S3Operations.ListBucketsAsync(fake);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListObjectsAsync_returns_empty_when_collections_null()
    {
        var fake = A.Fake<IAmazonS3>();
        A.CallTo(() => fake.ListObjectsV2Async(A<ListObjectsV2Request>._, A<CancellationToken>._))
            .Returns(new ListObjectsV2Response { CommonPrefixes = null!, S3Objects = null! });

        var result = await S3Operations.ListObjectsAsync(fake, "bucket");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task EmptyBucketAsync_handles_null_S3Objects()
    {
        var fake = A.Fake<IAmazonS3>();
        A.CallTo(() => fake.ListObjectsV2Async(A<ListObjectsV2Request>._, A<CancellationToken>._))
            .Returns(new ListObjectsV2Response { S3Objects = null!, IsTruncated = false });

        // Should not throw NRE
        await S3Operations.EmptyBucketAsync(fake, "bucket");
    }

    [Fact]
    public async Task DownloadFileAsync_writes_stream_to_local_path()
    {
        var fake = A.Fake<IAmazonS3>();
        var content = "downloaded content";
        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var destFile = Path.Combine(destDir, "output.txt");

        var getResp = new GetObjectResponse
        {
            BucketName = "bucket",
            Key = "remote/key.txt",
            ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        };

        A.CallTo(() => fake.GetObjectAsync("bucket", "remote/key.txt", A<CancellationToken>._))
            .Returns(getResp);

        try
        {
            var written = await S3Operations.DownloadFileAsync(
                fake,
                "bucket",
                "remote/key.txt",
                destFile
            );

            written.ShouldBe(content.Length);
            File.Exists(destFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(destFile)).ShouldBe(content);
        }
        finally
        {
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
        }
    }
}
