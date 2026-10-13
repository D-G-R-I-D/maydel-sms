using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Maydel.Application.Abstractions;
using Maydel.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Maydel.Application.Tests.Storage;

/// <summary>Both storage providers behave the same: save → open → delete, server-generated keys, no path tricks.</summary>
public abstract class FileStorageContractTests
{
    protected abstract IFileStorage Storage { get; }

    [Fact]
    public async Task Saves_opens_and_deletes()
    {
        var key = await Storage.SaveAsync(new MemoryStream("hello"u8.ToArray()), "students", "jpg", "image/jpeg");

        Assert.Matches("^students/[0-9a-f]{32}\\.jpg$", key);
        await using (var stream = await Storage.OpenAsync(key))
        {
            Assert.NotNull(stream);
            Assert.Equal("hello", await new StreamReader(stream).ReadToEndAsync());
        }

        await Storage.DeleteAsync(key);
        Assert.Null(await Storage.OpenAsync(key));
        await Storage.DeleteAsync(key); // deleting again is fine
    }

    [Fact]
    public async Task Folder_and_extension_input_cannot_shape_the_key()
    {
        var key = await Storage.SaveAsync(new MemoryStream([1]), "../../etc", ".j/p..g", "image/jpeg");
        Assert.Matches("^etc/[0-9a-f]{32}\\.jpg$", key);
        await Storage.DeleteAsync(key);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("students/../../x.jpg")]
    [InlineData("/etc/passwd")]
    [InlineData("students\\x.jpg")]
    public async Task Rejects_keys_it_did_not_generate(string key)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Storage.OpenAsync(key));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Storage.DeleteAsync(key));
    }

    [Fact]
    public async Task Missing_files_open_as_null() =>
        Assert.Null(await Storage.OpenAsync($"students/{Guid.NewGuid():N}.jpg"));
}

public sealed class LocalFileStorageTests : FileStorageContractTests
{
    protected override IFileStorage Storage { get; } = new LocalFileStorage(Options.Create(new StorageOptions
    {
        RootPath = Path.Combine(Path.GetTempPath(), "maydel-storage-" + Guid.NewGuid().ToString("N")[..8]),
    }));
}

/// <summary>
/// Runs the same contract against Adobe S3Mock, a real S3 API server (path-style, SigV4), standing in for Cloudflare R2,
/// Railway buckets or AWS S3. (MinIO's public images are no longer available on Docker Hub.)
/// </summary>
public sealed class S3FileStorageTests : FileStorageContractTests, IAsyncLifetime
{
    private const string Bucket = "maydel-test";
    private readonly IContainer _s3 = new ContainerBuilder("adobe/s3mock:4.7.0")
        .WithEnvironment("COM_ADOBE_TESTING_S3MOCK_STORE_INITIAL_BUCKETS", Bucket)
        .WithPortBinding(9090, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(9090).ForPath("/")))
        .Build();
    private S3FileStorage? _storage;

    protected override IFileStorage Storage => _storage!;

    private string Endpoint => $"http://{_s3.Hostname}:{_s3.GetMappedPublicPort(9090)}";

    public async Task InitializeAsync()
    {
        await _s3.StartAsync();
        _storage = new S3FileStorage(Options.Create(new StorageOptions
        {
            Provider = "S3",
            S3 = new S3StorageOptions
            {
                ServiceUrl = Endpoint, Bucket = Bucket, Region = "us-east-1",
                AccessKeyId = "test", SecretAccessKey = "test", ForcePathStyle = true,
            },
        }));
    }

    public async Task DisposeAsync()
    {
        _storage?.Dispose();
        await _s3.DisposeAsync();
    }

    [Fact]
    public async Task Objects_are_stored_with_their_content_type()
    {
        var key = await Storage.SaveAsync(new MemoryStream(Encoding.UTF8.GetBytes("x")), "staff", "jpg", "image/jpeg");
        using var client = new AmazonS3Client(new BasicAWSCredentials("test", "test"),
            new AmazonS3Config { ServiceURL = Endpoint, ForcePathStyle = true, AuthenticationRegion = "us-east-1" });
        var meta = await client.GetObjectMetadataAsync(Bucket, key);
        Assert.Equal("image/jpeg", meta.Headers.ContentType);
    }
}
