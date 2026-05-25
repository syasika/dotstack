using Spectre.Console;
using Spectre.Console.Cli;
using DotStack.Core.Aws;
using DotStack.Core.S3;

namespace DotStack.Cli.Commands;

public static class S3Commands
{
    public sealed class LsSettings : EndpointSettings
    {
        [CommandArgument(0, "[bucket]")]
        public string? Bucket { get; set; }
    }

    public sealed class BucketSettings : EndpointSettings
    {
        [CommandArgument(0, "<bucket>")]
        public string Bucket { get; set; } = "";
    }

    public sealed class BucketForceSettings : EndpointSettings
    {
        [CommandArgument(0, "<bucket>")]
        public string Bucket { get; set; } = "";

        [CommandOption("-f|--force")]
        public bool Force { get; set; }
    }

    public sealed class CpSettings : EndpointSettings
    {
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = "";

        [CommandArgument(1, "<destination>")]
        public string Destination { get; set; } = "";
    }

    public class LsCommand : Command<LsSettings>
    {
        protected override int Execute(CommandContext context, LsSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateS3Client(settings.EndpointUrl);

            if (settings.Bucket is null)
            {
                var buckets = S3Operations.ListBucketsAsync(client, cancellationToken).GetAwaiter().GetResult();
                if (buckets.Count == 0) { AnsiConsole.MarkupLine("[grey italic]No buckets.[/]"); return 0; }
                AnsiConsole.MarkupLine($"[bold white on #0066CC] Buckets ({buckets.Count}) [/]");
                foreach (var b in buckets) AnsiConsole.MarkupLine($"  📦 [bold #0044CC]{b}[/]");
                return 0;
            }

            var bucket = StripS3Prefix(settings.Bucket);
            var prefix = "";
            var slash = bucket.IndexOf('/');
            if (slash >= 0) { prefix = bucket[(slash + 1)..]; bucket = bucket[..slash]; }

            var objects = S3Operations.ListObjectsAsync(client, bucket, prefix, cancellationToken).GetAwaiter().GetResult();
            if (objects.Count == 0) { AnsiConsole.MarkupLine("[grey italic]No objects.[/]"); return 0; }

            var label = $" s3://{bucket}/";
            if (!string.IsNullOrEmpty(prefix)) label += prefix;
            AnsiConsole.MarkupLine($"[bold white on #0066CC]{label}[/]");
            foreach (var obj in objects)
            {
                if (obj.Key.EndsWith("/"))
                    AnsiConsole.MarkupLine($"  📁 [#00AAAA]{obj.Key}[/]");
                else
                    AnsiConsole.MarkupLine($"  📄 {obj.Key}{(obj.Size > 0 ? $"  [grey]({obj.Size} bytes)[/]" : "")}");
            }
            return 0;
        }

        private static string StripS3Prefix(string s) => s.StartsWith("s3://") ? s[5..] : s;
    }

    public class MbCommand : Command<BucketSettings>
    {
        protected override int Execute(CommandContext context, BucketSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateS3Client(settings.EndpointUrl);
            var bucket = settings.Bucket.StartsWith("s3://") ? settings.Bucket[5..] : settings.Bucket;
            S3Operations.CreateBucketAsync(client, bucket, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Bucket '[bold]{bucket}[/]' created");
            return 0;
        }
    }

    public class RbCommand : Command<BucketForceSettings>
    {
        protected override int Execute(CommandContext context, BucketForceSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateS3Client(settings.EndpointUrl);
            var bucket = settings.Bucket.StartsWith("s3://") ? settings.Bucket[5..] : settings.Bucket;

            if (settings.Force)
                S3Operations.EmptyBucketAsync(client, bucket, cancellationToken).GetAwaiter().GetResult();

            S3Operations.DeleteBucketAsync(client, bucket, cancellationToken).GetAwaiter().GetResult();
            AnsiConsole.MarkupLine($"[green bold]✓[/] Bucket '[bold]{bucket}[/]' removed");
            return 0;
        }
    }

    public class CpCommand : Command<CpSettings>
    {
        protected override int Execute(CommandContext context, CpSettings settings, CancellationToken cancellationToken)
        {
            var client = AwsClientFactory.CreateS3Client(settings.EndpointUrl);
            var src = settings.Source;
            var dst = settings.Destination;

            if (IsS3Path(src) && !IsS3Path(dst))
            {
                var (bucket, key) = ParseS3Path(src);
                if (string.IsNullOrEmpty(key)) { AnsiConsole.MarkupLine($"[red]Invalid s3 path: {src}[/]"); return 1; }
                var written = S3Operations.DownloadFileAsync(client, bucket, key, dst, cancellationToken).GetAwaiter().GetResult();
                AnsiConsole.MarkupLine($"[green bold]✓[/] Downloaded s3://{bucket}/{key} → {dst} ({written} bytes)");
            }
            else if (!IsS3Path(src) && IsS3Path(dst))
            {
                var (bucket, key) = ParseS3Path(dst);
                if (string.IsNullOrEmpty(key)) { AnsiConsole.MarkupLine($"[red]Invalid s3 path: {dst}[/]"); return 1; }
                S3Operations.UploadFileAsync(client, bucket, key, src, cancellationToken).GetAwaiter().GetResult();
                AnsiConsole.MarkupLine($"[green bold]✓[/] Uploaded {src} → s3://{bucket}/{key}");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]One argument must be an s3:// path and the other a local path[/]");
                return 1;
            }
            return 0;
        }

        private static bool IsS3Path(string s) => s.StartsWith("s3://");
        private static (string bucket, string key) ParseS3Path(string s)
        {
            var path = s.StartsWith("s3://") ? s[5..] : s;
            var parts = path.Split('/', 2);
            return parts.Length >= 2 ? (parts[0], parts[1]) : (parts[0], "");
        }
    }
}
