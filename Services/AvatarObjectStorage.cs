using System.Text.RegularExpressions;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace WebApplication1.Services;

public sealed class AvatarObjectStorage : IDisposable
{
    private static readonly Regex AvatarFileNamePattern = new("^[a-f0-9]{32}\\.(jpg|png|webp|gif)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private const string AvatarIdPrefix = "supabase/avatars/";
    private readonly string? bucket;
    private readonly string? publicBaseUrl;
    private readonly IAmazonS3? client;

    public bool IsConfigured => client is not null;

    public AvatarObjectStorage(IConfiguration configuration)
    {
        var section = configuration.GetSection("Storage:Avatars:S3");
        var endpoint = section["Endpoint"];
        bucket = section["Bucket"];
        var accessKey = section["AccessKey"];
        var secretKey = section["SecretKey"];
        var region = section["Region"];
        publicBaseUrl = section["PublicBaseUrl"]?.TrimEnd('/');
        var values = new[] { endpoint, bucket, accessKey, secretKey, region, publicBaseUrl };
        if (values.All(string.IsNullOrWhiteSpace))
        {
            return;
        }

        if (values.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Configure Storage:Avatars:S3:Endpoint, Bucket, AccessKey, SecretKey, Region, and PublicBaseUrl together to enable durable avatar storage.");
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var serviceUri) || serviceUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Storage:Avatars:S3:Endpoint must be an HTTPS URL.");
        }

        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var publicUri) || publicUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Storage:Avatars:S3:PublicBaseUrl must be an HTTPS URL.");
        }

        var s3Config = new AmazonS3Config
        {
            ServiceURL = serviceUri.ToString().TrimEnd('/'),
            ForcePathStyle = true,
            AuthenticationRegion = region
        };
        client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), s3Config);
    }

    public async Task<string> UploadAsync(string fileName, Stream content, long contentLength, string contentType, CancellationToken cancellationToken)
    {
        if (!AvatarFileNamePattern.IsMatch(fileName))
        {
            throw new ArgumentException("Invalid avatar file name.", nameof(fileName));
        }

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = fileName,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = contentType,
            AutoResetStreamPosition = false
        };
        request.Headers.ContentLength = contentLength;
        await GetClient().PutObjectAsync(request, cancellationToken);
        return AvatarIdPrefix + fileName;
    }

    public Task DeleteAsync(string avatarImageId, CancellationToken cancellationToken)
    {
        var fileName = GetFileName(avatarImageId);
        return GetClient().DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = fileName }, cancellationToken);
    }

    public string GetDisplayUrl(string? avatarImageId)
    {
        if (string.IsNullOrWhiteSpace(avatarImageId))
        {
            return string.Empty;
        }

        if (avatarImageId.StartsWith(AvatarIdPrefix, StringComparison.Ordinal))
        {
            return $"{publicBaseUrl}/{GetFileName(avatarImageId)}";
        }

        return $"/images/{avatarImageId}";
    }

    public bool IsSupabaseAvatar(string? avatarImageId) =>
        avatarImageId?.StartsWith(AvatarIdPrefix, StringComparison.Ordinal) == true;

    private static string GetFileName(string avatarImageId)
    {
        if (!avatarImageId.StartsWith(AvatarIdPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid avatar storage identifier.", nameof(avatarImageId));
        }

        var fileName = avatarImageId[AvatarIdPrefix.Length..];
        if (!AvatarFileNamePattern.IsMatch(fileName))
        {
            throw new ArgumentException("Invalid avatar storage identifier.", nameof(avatarImageId));
        }

        return fileName;
    }

    private IAmazonS3 GetClient() => client
        ?? throw new InvalidOperationException("Avatar object storage is not configured. Set Storage:Avatars:S3 environment variables to use remote avatar storage.");

    public void Dispose() => client?.Dispose();
}
