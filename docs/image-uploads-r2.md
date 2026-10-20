# Design image uploads with Cloudflare R2

Implementation reference for a future upload feature in TattooFlash. This document does not configure R2 or add upload functionality to the application. Code examples are illustrative and should be checked against the package versions selected during implementation.

## Recommended initial flow

For the Interactive Server Blazor app, start with uploads through the application server:

```text
Select image in Blazor
    → Server validates and processes image
    → Server uploads processed image to R2
    → Database stores object key alongside design
    → Browser displays image through public image domain
```

R2 credentials stay on the server. Put storage operations behind a service so Razor components do not depend on R2 details.

## Cloudflare configuration

1. Create a Standard storage bucket, for example `tattooflash-designs`.
2. Generate R2 S3 API credentials with object read/write access scoped to that bucket.
3. Record the account ID, access key ID, and secret access key.
4. Connect a public image domain such as `images.yourdomain.com` for published design images.

The public domain provides image reads; uploads still require authorization. Use `r2.dev` URLs for development rather than production. A custom domain enables Cloudflare caching. Keep private originals or unpublished assets in a separate private bucket if needed.

At the time this reference was written, the Standard storage free tier included 10 GB-month of storage, one million Class A operations, and ten million Class B operations monthly, with free internet egress. Above-limit usage is billable. Verify current pricing before implementation.

- [R2 pricing](https://developers.cloudflare.com/r2/pricing/)
- [Public buckets and custom domains](https://developers.cloudflare.com/r2/buckets/public-buckets/)
- [R2 S3 API credentials](https://developers.cloudflare.com/r2/api/tokens/)

## Server configuration and storage service

R2 supports the S3 API. Use `AWSSDK.S3`, checking compatibility against Cloudflare's current documentation. Cloudflare's referenced .NET example targets SDK version 3.

Non-secret settings can go in `appsettings.json`:

```json
{
  "R2": {
    "AccountId": "your-account-id",
    "BucketName": "tattooflash-designs",
    "PublicBaseUrl": "https://images.yourdomain.com"
  }
}
```

Store `R2:AccessKeyId` and `R2:SecretAccessKey` in .NET user secrets locally and the hosting provider's secret configuration in production. Do not commit credentials.

Example registration in `Program.cs`:

```csharp
using Amazon.Runtime;
using Amazon.S3;

builder.Services.AddSingleton<IAmazonS3>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();

    return new AmazonS3Client(
        new BasicAWSCredentials(
            configuration["R2:AccessKeyId"]!,
            configuration["R2:SecretAccessKey"]!),
        new AmazonS3Config
        {
            ServiceURL =
                $"https://{configuration["R2:AccountId"]}.r2.cloudflarestorage.com",
            AuthenticationRegion = "auto",
            ForcePathStyle = true
        });
});

builder.Services.AddScoped<DesignImageStorage>();
builder.Services.AddScoped<DesignImageProcessor>();
```

Validate required configuration at startup in the implementation rather than relying solely on the null-forgiving operators shown above.

Example storage service, receiving an already validated image:

```csharp
using Amazon.S3;
using Amazon.S3.Model;

public sealed class DesignImageStorage(
    IAmazonS3 s3,
    IConfiguration configuration)
{
    public async Task<string> UploadAsync(
        Stream validatedImage,
        string contentType,
        string extension,
        CancellationToken cancellationToken = default)
    {
        // These values come from the image processor,
        // not the browser's filename or declared MIME type.
        var key = $"designs/{Guid.NewGuid():N}{extension}";

        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = configuration["R2:BucketName"]!,
            Key = key,
            InputStream = validatedImage,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        }, cancellationToken);

        return key;
    }
}
```

Cloudflare's current .NET guidance specifies the two disabling flags to avoid unsupported streaming signature behaviour. Recheck this advice when selecting the SDK version.

- [Official R2 .NET SDK examples](https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/)

## Server-side image validation

Treat validation as a pipeline: inspect, enforce limits, decode, orient, resize, and re-encode. Filename extensions and browser-provided `ContentType` values are not proof of the actual file format. A file picker `accept` attribute helps users select files but does not replace server validation.

### Package options

| Package | Considerations |
| --- | --- |
| `SixLabors.ImageSharp` | Managed C# API for inspection, decoding, resizing, orientation, and encoding. Review current Six Labors licensing. |
| `SkiaSharp` | Native Skia-based image processing. Account for native dependencies in the hosting environment. |

ImageSharp is a convenient starting point for this pipeline. Check its license for the actual project and business circumstances rather than assuming all uses are unrestricted.

- [ImageSharp documentation](https://docs.sixlabors.com/articles/imagesharp/index.html)
- [Six Labors licensing and pricing](https://sixlabors.com/pricing/)
- [ImageSharp security considerations](https://docs.sixlabors.com/articles/imagesharp/security.html)
- [SkiaSharp codec API](https://learn.microsoft.com/en-us/dotnet/api/skiasharp.skcodec)

### Suggested initial policies

| Policy | Starting value |
| --- | --- |
| Maximum incoming file size | 10 MB |
| Accepted formats | JPEG, PNG, WebP |
| Maximum source side length | 8,000 pixels |
| Maximum source pixel count | 25 megapixels |
| Published dimensions | Fit within 1,800 × 1,800 without upscaling |
| Animation | Publish only the first frame as a static image |
| Output | Lossless WebP, preserving transparency |
| Metadata | Apply orientation, then omit metadata from published output |

These limits are adjustable product choices. Pixel limits matter because a small compressed upload may require substantial decoded memory. Identification is a preflight check; a full decode must also succeed. Restrict concurrent processing and keep the image library patched.

### Example ImageSharp processor

This example uses an explicitly size-limited browser stream and an in-memory buffer so it can inspect and decode the same file. It is suitable as a starting point for small, infrequent admin uploads. Verify the APIs against the ImageSharp version selected for implementation.

```csharp
using Microsoft.AspNetCore.Components.Forms;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

public sealed record ProcessedDesignImage(
    byte[] Bytes,
    string ContentType,
    string Extension,
    int Width,
    int Height);

public sealed class DesignImageProcessor
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private const int MaxDimension = 8_000;
    private const long MaxPixels = 25_000_000;
    private const int OutputDimension = 1_800;

    public async Task<ProcessedDesignImage> ProcessAsync(
        IBrowserFile file,
        CancellationToken cancellationToken = default)
    {
        await using var browserStream = file.OpenReadStream(
            MaxUploadBytes,
            cancellationToken);

        using var buffer = new MemoryStream();

        await browserStream.CopyToAsync(
            buffer, cancellationToken);

        try
        {
            var options = new DecoderOptions
            {
                MaxFrames = 1
            };

            buffer.Position = 0;

            var info = await Image.IdentifyAsync(
                options, buffer, cancellationToken);

            if (info is null)
            {
                throw new ImageValidationException(
                    "The file could not be recognised as an image.");
            }

            var format = info.Metadata.DecodedImageFormat?.Name;

            if (format is not ("JPEG" or "PNG" or "WEBP"))
            {
                throw new ImageValidationException(
                    "Please select a JPEG, PNG, or WebP image.");
            }

            if (info.Width <= 0 ||
                info.Height <= 0 ||
                info.Width > MaxDimension ||
                info.Height > MaxDimension ||
                (long)info.Width * info.Height > MaxPixels)
            {
                throw new ImageValidationException(
                    "The image dimensions are too large.");
            }

            buffer.Position = 0;

            using var image = await Image.LoadAsync(
                options, buffer, cancellationToken);

            image.Mutate(context => context.AutoOrient());

            if (image.Width > OutputDimension ||
                image.Height > OutputDimension)
            {
                image.Mutate(context => context.Resize(
                    new ResizeOptions
                    {
                        Size = new Size(
                            OutputDimension, OutputDimension),
                        Mode = ResizeMode.Max
                    }));
            }

            using var output = new MemoryStream();

            await image.SaveAsync(
                output,
                new WebpEncoder
                {
                    FileFormat = WebpFileFormatType.Lossless,
                    SkipMetadata = true
                },
                cancellationToken);

            return new ProcessedDesignImage(
                output.ToArray(),
                "image/webp",
                ".webp",
                image.Width,
                image.Height);
        }
        catch (UnknownImageFormatException)
        {
            throw new ImageValidationException(
                "The file is not a supported image.");
        }
        catch (InvalidImageContentException)
        {
            throw new ImageValidationException(
                "The image is damaged or could not be read.");
        }
    }
}

public sealed class ImageValidationException(string message)
    : Exception(message);
```

Orientation must be applied before metadata is discarded, otherwise phone images can appear sideways. Re-encoding produces a new published image rather than retaining the original uploaded file. This is not a guarantee against all decoder vulnerabilities; maintained dependencies and resource limits remain necessary.

The example buffers both input and encoded output and also allocates decoded pixels. The incoming 10 MB limit is not a cap on total processing memory or output size. For larger workloads, use temporary files outside `wwwroot`, constrain simultaneous work, and enforce a separate output-size policy if needed.

## Connecting the Blazor form to processing and storage

Use a MudBlazor file upload component with `IBrowserFile`. Keep the selected file pending until Save, then validate the form, process the image, and upload it. Disable repeated submission and show upload progress or a busy indicator.

```csharp
var processed = await imageProcessor.ProcessAsync(
    selectedFile, cancellationToken);

using var stream = new MemoryStream(
    processed.Bytes, writable: false);

var imageKey = await imageStorage.UploadAsync(
    stream,
    processed.ContentType,
    processed.Extension,
    cancellationToken);

// Persist imageKey with the design.
```

Catch `ImageValidationException` in the form and display its message through a `MudAlert` or snackbar. Handle oversized-stream `IOException` errors as upload failures without assuming every I/O failure means the size limit was exceeded. Log unexpected failures on the server; do not display internal exception details. Preserve cancellation behaviour.

Authorize design creation or editing on the server before allowing upload work.

- [Blazor file upload guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads/)

## Database reference and public URL

Store an object key on the design rather than the image bytes or a hardcoded domain:

```csharp
public required string ImageKey { get; set; }
```

Example key:

```text
designs/b4c2d51c67484aa885ab731d07864308.webp
```

Build the display URL in a service or view model:

```csharp
var imageUrl = $"{publicBaseUrl.TrimEnd('/')}/{design.ImageKey}";
```

Existing `MudImage` and `MudCardMedia` components can continue consuming `ImageUrl`. Keep the bucket's S3 API endpoint separate from the public image domain.

## Save failures, replacement, and cleanup

R2 and the database do not share a transaction:

1. Authorize the user and validate the form.
2. Process and upload the image under a generated key.
3. Save the design and image key to the database.
4. If the database save fails, attempt to remove the uploaded object and record cleanup failures for retry.

When replacing an image, upload it under a new key, save that key, then remove the old object. New keys avoid stale cached content. Once published objects are cached, deleting the R2 object alone may not immediately remove cached copies; account for cache purge requirements where necessary.

For a larger implementation, consider a pending-upload record and a scheduled orphan cleanup process. Generate a smaller thumbnail alongside the published image if card loading performance warrants it.

## Alternative: browser-to-R2 uploads

For larger or more frequent uploads, the server can issue a short-lived presigned PUT URL and the browser can upload directly to R2. This avoids passing all bytes through the Blazor server, but adds browser upload code, bucket CORS configuration, and a completion flow that verifies uploaded objects before attaching them to designs.

Direct uploads must still undergo trusted validation and processing before publication, typically through a private staging bucket or prefix. Presigned URLs use the R2 S3 API domain, not the public custom domain.

- [R2 presigned URLs](https://developers.cloudflare.com/r2/api/s3/presigned-urls/)
- [R2 CORS configuration](https://developers.cloudflare.com/r2/buckets/cors/)

Start with the server-side pipeline for a small number of admin uploads, keeping processing and storage behind separate services so the upload transport can change later.
