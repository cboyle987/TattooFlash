using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Moq;
using TattooFlash.Web.Configuration;
using TattooFlash.Web.Services;

namespace TattooFlash.UnitTest.Services;

public class CloudflareImageServiceTests
{
    private readonly Mock<IAmazonS3> _client = new(MockBehavior.Strict);
    private CloudflareImageService CreateService() => new(Options.Create(new R2Configuration
    {
        BucketName = "test-bucket",
        Prefix = "designs",
        PublicBaseUrl = "https://images.example.test"
    }), _client.Object);

    [Fact]
    public void GetImageUrl_ReturnsConfiguredPublicPath()
    {
        // Arrange
        var sut = CreateService();


        // Act
        var result = sut.GetImageUrl("image.webp");


        // Assert
        Assert.Equal("https://images.example.test/designs/image.webp", result);
        _client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UploadImage_SendsConfiguredRequestAndReturnsGeneratedFilename()
    {
        // Arrange
        using var source = new MemoryStream([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        PutObjectRequest? captured = null;
        _client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), cancellation.Token))
            .Callback<PutObjectRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new PutObjectResponse());
        var sut = CreateService();


        // Act
        var filename = await sut.UploadImage(source, "image/webp", ".webp", cancellation.Token);


        // Assert
        Assert.EndsWith(".webp", filename);
        Assert.True(Guid.TryParse(Path.GetFileNameWithoutExtension(filename), out _));
        Assert.NotNull(captured);
        Assert.Equal("test-bucket", captured.BucketName);
        Assert.Equal($"designs/{filename}", captured.Key);
        Assert.Equal("image/webp", captured.ContentType);
        Assert.Same(source, captured.InputStream);
        Assert.False(captured.AutoCloseStream);
        Assert.True(captured.DisablePayloadSigning);
        Assert.True(captured.DisableDefaultChecksumValidation);
        Assert.True(source.CanRead);
        _client.Verify(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task DeleteImageAsync_UsesBucketPrefixFilenameAndCancellationToken()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        _client.Setup(x => x.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r =>
            r.BucketName == "test-bucket" && r.Key == "designs/image.webp"), cancellation.Token))
            .ReturnsAsync(new DeleteObjectResponse());
        var sut = CreateService();


        // Act
        await sut.DeleteImageAsync("image.webp", cancellation.Token);


        // Assert
        _client.Verify(x => x.DeleteObjectAsync(It.Is<DeleteObjectRequest>(r =>
            r.BucketName == "test-bucket" && r.Key == "designs/image.webp"), cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageOperation_WhenClientFails_PropagatesException(bool upload)
    {
        // Arrange
        using var source = new MemoryStream([1]);
        var failure = new AmazonS3Exception("Storage unavailable");
        if (upload)
            _client.Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), CancellationToken.None)).ThrowsAsync(failure);
        else
            _client.Setup(x => x.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), CancellationToken.None)).ThrowsAsync(failure);
        var sut = CreateService();


        // Act
        var exception = await Assert.ThrowsAsync<AmazonS3Exception>(async () =>
        {
            if (upload) await sut.UploadImage(source, "image/webp", ".webp", CancellationToken.None);
            else await sut.DeleteImageAsync("image.webp", CancellationToken.None);
        });


        // Assert
        Assert.Same(failure, exception);
        Assert.True(source.CanRead);
    }
}