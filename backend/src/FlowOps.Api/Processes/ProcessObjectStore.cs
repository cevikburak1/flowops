using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace FlowOps.Api;

internal sealed class ProcessObjectStore : IDisposable
{
    public const int InlineLimit = 1024 * 1024;
    public const int FileLimit = 10 * InlineLimit;
    public const int MaxEncodedLength = 13_981_016;
    public const int MaxRequestLength = 15_000_000;

    private readonly AmazonS3Client? client;
    private readonly string? bucket;
    public bool Enabled => client is not null;

    public ProcessObjectStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var settings = configuration.GetSection("Processes:Storage");
        if (settings["Endpoint"] is not { Length: > 0 } endpoint) return;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http")) ||
            settings["Bucket"] is not { Length: > 0 } bucketName ||
            settings["AccessKey"] is not { Length: > 0 } access ||
            settings["SecretKey"] is not { Length: > 0 } secret)
            throw new InvalidOperationException("Configure the process storage endpoint, bucket, access key, and secret key.");
        bucket = bucketName;
        client = new AmazonS3Client(new BasicAWSCredentials(access, secret), new AmazonS3Config
        {
            ServiceURL = uri.AbsoluteUri, ForcePathStyle = true, AuthenticationRegion = settings["Region"] ?? "us-east-1",
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            MaxErrorRetry = 1
        });
    }

    public async Task PutAsync(string key, byte[] content, CancellationToken token)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        await client!.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket, Key = key, InputStream = stream, ContentType = "application/octet-stream",
            UseChunkEncoding = false
        }, timeout.Token);
    }

    public Task<GetObjectResponse> GetAsync(string key, CancellationToken token) => client!.GetObjectAsync(bucket, key, token);

    public void Dispose() => client?.Dispose();
}
