using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace WebApplication1.Services;

public sealed class ObjectMediaStorage : IDisposable
{
    private readonly string? bucket;
    private readonly IAmazonS3? client;

    public bool IsConfigured => client is not null;

    public ObjectMediaStorage(IConfiguration configuration)
    {
        var endpoint = configuration["Storage:S3:Endpoint"];
        bucket = configuration["Storage:S3:Bucket"];
        var accessKey = configuration["Storage:S3:AccessKey"];
        var secretKey = configuration["Storage:S3:SecretKey"];
        var values = new[] { endpoint, bucket, accessKey, secretKey };
        if (values.All(string.IsNullOrWhiteSpace))
        {
            return;
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Configure Storage:S3:Endpoint, Bucket, AccessKey, and SecretKey together to enable durable object storage.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var serviceUri)
            || (serviceUri.Scheme != Uri.UriSchemeHttps && !configuration.GetValue<bool>("Storage:S3:AllowInsecureHttp")))
        {
            throw new InvalidOperationException("Storage:S3:Endpoint must be an HTTPS URL.");
        }

        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUri.ToString().TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = configuration["Storage:S3:Region"] ?? "auto"
        };
        client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config);
    }

    public Task UploadAsync(string key, Stream content, long contentLength, string contentType, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = contentType,
            AutoResetStreamPosition = false
        };
        request.Headers.ContentLength = contentLength;
        return GetClient().PutObjectAsync(request, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        GetClient().DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key }, cancellationToken);

    public Task<GetObjectMetadataResponse> GetMetadataAsync(string key, CancellationToken cancellationToken) =>
        GetClient().GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = bucket, Key = key }, cancellationToken);

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await GetMetadataAsync(key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task<GetObjectResponse> GetObjectAsync(string key, long start, long end, CancellationToken cancellationToken) =>
        GetClient().GetObjectAsync(new GetObjectRequest
        {
            BucketName = bucket,
            Key = key,
            ByteRange = new ByteRange(start, end)
        }, cancellationToken);

    private IAmazonS3 GetClient() => client
        ?? throw new InvalidOperationException("Object storage is not configured. Set Storage:S3 environment variables to use remote media storage.");

    public void Dispose() => client?.Dispose();
}
