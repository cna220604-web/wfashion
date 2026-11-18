using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

public sealed class MediaController(
    ObjectMediaStorage objectStorage,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<MediaController> logger) : Controller
{
    private static readonly Regex VideoFileNamePattern = new("^[a-f0-9]{32}\\.(mp4|webm)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [HttpGet("/videos/fashion/{fileName}")]
    public async Task<IActionResult> FashionVideo(string fileName, CancellationToken cancellationToken)
    {
        if (!VideoFileNamePattern.IsMatch(fileName))
        {
            return NotFound();
        }

        if (!objectStorage.IsConfigured)
        {
            var storageRoot = configuration["Storage:PersistentPath"] ?? environment.WebRootPath;
            var localPath = Path.Combine(storageRoot, "videos", "fashion", fileName);
            return System.IO.File.Exists(localPath)
                ? PhysicalFile(localPath, GetVideoContentType(fileName), enableRangeProcessing: true)
                : NotFound();
        }

        if (!string.IsNullOrWhiteSpace(objectStorage.PublicBaseUrl))
        {
            return Redirect($"{objectStorage.PublicBaseUrl}/videos/fashion/{Uri.EscapeDataString(fileName)}");
        }

        var key = $"videos/fashion/{fileName}";
        GetObjectMetadataResponse metadata;
        try
        {
            metadata = await objectStorage.GetMetadataAsync(key, cancellationToken);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            var storageRoot = configuration["Storage:PersistentPath"] ?? environment.WebRootPath;
            var localPath = Path.Combine(storageRoot, "videos", "fashion", fileName);
            return System.IO.File.Exists(localPath)
                ? PhysicalFile(localPath, GetVideoContentType(fileName), enableRangeProcessing: true)
                : NotFound();
        }
        catch (AmazonS3Exception exception)
        {
            logger.LogError(exception, "Unable to read fashion video metadata for {VideoFileName}", fileName);
            throw;
        }

        var totalLength = metadata.ContentLength;
        var start = 0L;
        var end = totalLength - 1;
        var isPartial = false;
        if (Request.Headers.TryGetValue("Range", out var rangeHeader))
        {
            if (!RangeHeaderValue.TryParse(rangeHeader.ToString(), out var parsedRange)
                || parsedRange.Unit != "bytes"
                || parsedRange.Ranges.Count != 1)
            {
                return RangeNotSatisfiable(totalLength);
            }

            var requestedRange = parsedRange.Ranges.Single();
            if (requestedRange.From is long requestedStart)
            {
                start = requestedStart;
                end = requestedRange.To ?? end;
            }
            else if (requestedRange.To is long suffixLength && suffixLength > 0)
            {
                start = Math.Max(0, totalLength - suffixLength);
            }
            else
            {
                return RangeNotSatisfiable(totalLength);
            }

            if (start >= totalLength || start > end)
            {
                return RangeNotSatisfiable(totalLength);
            }

            end = Math.Min(end, totalLength - 1);
            isPartial = true;
        }

        Response.StatusCode = isPartial ? StatusCodes.Status206PartialContent : StatusCodes.Status200OK;
        Response.ContentType = string.IsNullOrWhiteSpace(metadata.Headers.ContentType)
            ? GetVideoContentType(fileName)
            : metadata.Headers.ContentType;
        Response.Headers.AcceptRanges = "bytes";
        Response.Headers.CacheControl = "public, max-age=3600";
        Response.ContentLength = end - start + 1;
        if (isPartial)
        {
            Response.Headers.ContentRange = $"bytes {start}-{end}/{totalLength}";
        }

        using var video = await objectStorage.GetObjectAsync(key, start, end, cancellationToken);
        await video.ResponseStream.CopyToAsync(Response.Body, cancellationToken);
        return new EmptyResult();
    }

    private IActionResult RangeNotSatisfiable(long totalLength)
    {
        Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
        Response.Headers.ContentRange = $"bytes */{totalLength}";
        return new EmptyResult();
    }

    private static string GetVideoContentType(string fileName) =>
        fileName.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4";
}
