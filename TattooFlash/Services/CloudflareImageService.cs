using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TattooFlash.Configuration;
using TattooFlash.Interfaces;

namespace TattooFlash.Services
{
    public class CloudflareImageService : IImageService
    {
        private readonly R2Configuration _r2config;
        private readonly IAmazonS3 _client;

        public CloudflareImageService(IOptions<R2Configuration> r2ConfigOptions, IAmazonS3 client)
        {
            _r2config = r2ConfigOptions.Value;
            _client = client;
        }

        public async Task DeleteImageAsync(string imageName, CancellationToken cancellationToken)
        {
            var request = new DeleteObjectRequest { BucketName = _r2config.BucketName, Key = $"{_r2config.Prefix}/{imageName}"};

            await _client.DeleteObjectAsync(request, cancellationToken);
        }

        public string GetImageUrl(string imageName)
        {
            return $"{_r2config.PublicBaseUrl}/{_r2config.Prefix}/{imageName}";
        }

        public async Task<string> UploadImage(Stream imageData, string contentType, string extension, CancellationToken cancellationToken)
        {
            var filename = $"{Guid.NewGuid()}{extension}";
            var objectPath = $"{_r2config.Prefix}/{filename}";

            var request = new PutObjectRequest
            {
                InputStream = imageData,
                Key = objectPath,
                ContentType = contentType,
                AutoCloseStream = false,
                BucketName = _r2config.BucketName,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };

            var response = await _client.PutObjectAsync(request, cancellationToken);

            return filename;
        }
    }
}
