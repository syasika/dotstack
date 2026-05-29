using DotStack.Cli.Commands;
using Shouldly;
using Xunit;

namespace DotStack.Cli.Tests;

public class S3CommandsHelpersTests
{
    // StripS3Prefix
    [Fact]
    public void StripS3Prefix_removes_prefix()
    {
        var result = S3Commands.LsCommand.StripS3Prefix("s3://my-bucket/path");
        result.ShouldBe("my-bucket/path");
    }

    [Fact]
    public void StripS3Prefix_returns_original_when_no_prefix()
    {
        var result = S3Commands.LsCommand.StripS3Prefix("my-bucket");
        result.ShouldBe("my-bucket");
    }

    [Fact]
    public void StripS3Prefix_handles_root()
    {
        var result = S3Commands.LsCommand.StripS3Prefix("s3://");
        result.ShouldBe("");
    }

    // IsS3Path
    [Fact]
    public void IsS3Path_returns_true_for_s3_path()
    {
        S3Commands.CpCommand.IsS3Path("s3://bucket/key").ShouldBeTrue();
    }

    [Fact]
    public void IsS3Path_returns_false_for_local_path()
    {
        S3Commands.CpCommand.IsS3Path("/local/file.txt").ShouldBeFalse();
    }

    [Fact]
    public void IsS3Path_returns_false_for_relative_path()
    {
        S3Commands.CpCommand.IsS3Path("file.txt").ShouldBeFalse();
    }

    // ParseS3Path
    [Fact]
    public void ParseS3Path_returns_bucket_and_key()
    {
        var (bucket, key) = S3Commands.CpCommand.ParseS3Path("s3://my-bucket/some/path/file.txt");
        bucket.ShouldBe("my-bucket");
        key.ShouldBe("some/path/file.txt");
    }

    [Fact]
    public void ParseS3Path_returns_bucket_only_when_no_key()
    {
        var (bucket, key) = S3Commands.CpCommand.ParseS3Path("s3://my-bucket");
        bucket.ShouldBe("my-bucket");
        key.ShouldBe("");
    }

    [Fact]
    public void ParseS3Path_handles_absolute_path()
    {
        // "/local/file" splits on the first '/' into ["", "local/file"]
        var (bucket, key) = S3Commands.CpCommand.ParseS3Path("/local/file");
        bucket.ShouldBe("");
        key.ShouldBe("local/file");
    }

    [Fact]
    public void ParseS3Path_handles_bucket_with_trailing_slash()
    {
        var (bucket, key) = S3Commands.CpCommand.ParseS3Path("s3://bucket/");
        bucket.ShouldBe("bucket");
        key.ShouldBe("");
    }
}
