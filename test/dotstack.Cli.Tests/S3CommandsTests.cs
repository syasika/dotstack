using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using DotStack.Cli.Abstractions;
using DotStack.Cli.Commands;
using FakeItEasy;
using Shouldly;
using Spectre.Console.Testing;
using Xunit;

namespace DotStack.Cli.Tests;

public class S3CommandsTests
{
    private readonly TestConsole _console;
    private readonly IAwsClientFactory _factory;
    private readonly IAmazonS3 _s3;

    public S3CommandsTests()
    {
        _console = new TestConsole();
        _s3 = A.Fake<IAmazonS3>();
        _factory = A.Fake<IAwsClientFactory>();
        A.CallTo(() => _factory.CreateS3Client(A<string>._)).Returns(_s3);
    }

    // ---- LsCommand ----

    [Fact]
    public void LsCommand_no_buckets_shows_empty_message()
    {
        A.CallTo(() => _s3.ListBucketsAsync(A<CancellationToken>._))
            .Returns(new ListBucketsResponse { Buckets = [] });

        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.LsSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("No buckets");
    }

    [Fact]
    public void LsCommand_shows_buckets()
    {
        A.CallTo(() => _s3.ListBucketsAsync(A<CancellationToken>._))
            .Returns(new ListBucketsResponse
            {
                Buckets =
                [
                    new S3Bucket { BucketName = "bucket-one" },
                    new S3Bucket { BucketName = "bucket-two" },
                ],
            });

        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.LsSettings());

        result.ShouldBe(0);
        _console.Output.ShouldContain("Buckets (2)");
        _console.Output.ShouldContain("bucket-one");
        _console.Output.ShouldContain("bucket-two");
    }

    [Fact]
    public void LsCommand_with_bucket_shows_objects()
    {
        A.CallTo(() => _s3.ListObjectsV2Async(
                A<ListObjectsV2Request>.That.Matches(r => r.BucketName == "my-bucket"),
                A<CancellationToken>._))
            .Returns(new ListObjectsV2Response
            {
                S3Objects =
                [
                    new Amazon.S3.Model.S3Object { Key = "file1.txt", Size = 100 },
                    new Amazon.S3.Model.S3Object { Key = "file2.txt", Size = 200 },
                ],
                CommonPrefixes = [],
            });

        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.LsSettings { Bucket = "my-bucket" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("file1.txt");
        _console.Output.ShouldContain("file2.txt");
    }

    [Fact]
    public void LsCommand_with_bucket_no_objects_shows_empty()
    {
        A.CallTo(() => _s3.ListObjectsV2Async(
                A<ListObjectsV2Request>._, A<CancellationToken>._))
            .Returns(new ListObjectsV2Response { S3Objects = [], CommonPrefixes = [] });

        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.LsSettings { Bucket = "empty-bucket" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("No objects");
    }

    [Fact]
    public void LsCommand_with_s3_bucket_path_lists_objects()
    {
        A.CallTo(() => _s3.ListObjectsV2Async(
                A<ListObjectsV2Request>.That.Matches(r =>
                    r.BucketName == "my-bucket" && r.Prefix == "prefix/"),
                A<CancellationToken>._))
            .Returns(new ListObjectsV2Response
            {
                S3Objects = [new Amazon.S3.Model.S3Object { Key = "prefix/file.txt", Size = 42 }],
                CommonPrefixes = [],
            });

        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.LsSettings { Bucket = "s3://my-bucket/prefix/" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("prefix/file.txt");
    }

    [Fact]
    public void LsCommand_shows_folders()
    {
        A.CallTo(() => _s3.ListObjectsV2Async(
                A<ListObjectsV2Request>._, A<CancellationToken>._))
            .Returns(new ListObjectsV2Response
            {
                CommonPrefixes = ["folder1/", "folder2/"],
                S3Objects = [],
            });

        var cmd = new S3Commands.LsCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.LsSettings { Bucket = "b" });

        result.ShouldBe(0);
        _console.Output.ShouldContain("folder1/");
        _console.Output.ShouldContain("folder2/");
    }

    // ---- MbCommand ----

    [Fact]
    public void MbCommand_creates_bucket()
    {
        var cmd = new S3Commands.MbCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.BucketSettings { Bucket = "new-bucket" });

        result.ShouldBe(0);
        A.CallTo(() => _s3.PutBucketAsync("new-bucket", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("new-bucket");
        _console.Output.ShouldContain("created");
    }

    [Fact]
    public void MbCommand_creates_bucket_with_s3_prefix()
    {
        var cmd = new S3Commands.MbCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.BucketSettings { Bucket = "s3://prefixed-bucket" });

        result.ShouldBe(0);
        A.CallTo(() => _s3.PutBucketAsync("prefixed-bucket", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    // ---- RbCommand ----

    [Fact]
    public void RbCommand_removes_bucket()
    {
        var cmd = new S3Commands.RbCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.BucketForceSettings { Bucket = "old-bucket" });

        result.ShouldBe(0);
        A.CallTo(() => _s3.DeleteBucketAsync("old-bucket", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        _console.Output.ShouldContain("old-bucket");
        _console.Output.ShouldContain("removed");
    }

    [Fact]
    public void RbCommand_force_empties_then_removes_bucket()
    {
        A.CallTo(() => _s3.ListObjectsV2Async(A<ListObjectsV2Request>._, A<CancellationToken>._))
            .Returns(new ListObjectsV2Response { S3Objects = [], IsTruncated = false });

        var cmd = new S3Commands.RbCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.BucketForceSettings { Bucket = "old-bucket", Force = true });

        result.ShouldBe(0);
        A.CallTo(() => _s3.DeleteBucketAsync("old-bucket", A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    // ---- CpCommand ----

    [Fact]
    public void CpCommand_downloads_s3_to_local()
    {
        var content = "test content";
        var getResp = new GetObjectResponse
        {
            BucketName = "bucket",
            Key = "remote/file.txt",
            ResponseStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        };
        A.CallTo(() => _s3.GetObjectAsync("bucket", "remote/file.txt", A<CancellationToken>._))
            .Returns(getResp);

        var destDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, "output.txt");

        try
        {
            var cmd = new S3Commands.CpCommand(_console, _factory);
            var result = cmd.Execute(new S3Commands.CpSettings
            {
                Source = "s3://bucket/remote/file.txt",
                Destination = destFile,
            });

            result.ShouldBe(0);
            _console.Output.ShouldContain("Downloaded");
            _console.Output.ShouldContain(content.Length.ToString());
        }
        finally
        {
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);
        }
    }

    [Fact]
    public void CpCommand_uploads_local_to_s3()
    {
        var srcFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            File.WriteAllText(srcFile, "upload content");

            var cmd = new S3Commands.CpCommand(_console, _factory);
            var result = cmd.Execute(new S3Commands.CpSettings
            {
                Source = srcFile,
                Destination = "s3://bucket/remote/key.txt",
            });

            result.ShouldBe(0);
            A.CallTo(() => _s3.PutObjectAsync(
                    A<PutObjectRequest>.That.Matches(r =>
                        r.BucketName == "bucket" && r.Key == "remote/key.txt"),
                    A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            _console.Output.ShouldContain("Uploaded");
        }
        finally
        {
            if (File.Exists(srcFile))
                File.Delete(srcFile);
        }
    }

    [Fact]
    public void CpCommand_invalid_s3_source_returns_error()
    {
        var cmd = new S3Commands.CpCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.CpSettings
        {
            Source = "s3://bucket-only",
            Destination = "/local/path",
        });

        result.ShouldBe(1);
        _console.Output.ShouldContain("Invalid s3 path");
    }

    [Fact]
    public void CpCommand_invalid_s3_destination_returns_error()
    {
        var cmd = new S3Commands.CpCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.CpSettings
        {
            Source = "/local/file",
            Destination = "s3://bucket-only",
        });

        result.ShouldBe(1);
        _console.Output.ShouldContain("Invalid s3 path");
    }

    [Fact]
    public void CpCommand_both_local_paths_returns_error()
    {
        var cmd = new S3Commands.CpCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.CpSettings
        {
            Source = "/local/a",
            Destination = "/local/b",
        });

        result.ShouldBe(1);
        _console.Output.ShouldContain("One argument must be an s3:// path");
    }

    [Fact]
    public void CpCommand_both_s3_paths_returns_error()
    {
        var cmd = new S3Commands.CpCommand(_console, _factory);
        var result = cmd.Execute(new S3Commands.CpSettings
        {
            Source = "s3://a/x",
            Destination = "s3://b/y",
        });

        result.ShouldBe(1);
        _console.Output.ShouldContain("One argument must be an s3:// path");
    }
}
