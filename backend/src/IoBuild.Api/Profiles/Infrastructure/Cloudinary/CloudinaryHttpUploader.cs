using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace IoBuild.Api.Profiles.Infrastructure.Cloudinary;

public interface ICloudinaryUploader
{
    Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default);
}

public sealed class CloudinaryHttpUploader(HttpClient client, IConfiguration configuration, TimeProvider? clock = null) : ICloudinaryUploader
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public async Task<string?> UploadAsync(string content, CancellationToken cancellationToken = default)
    {
        var cloudName = configuration["Cloudinary:CloudName"];
        var apiKey = configuration["Cloudinary:ApiKey"];
        var apiSecret = configuration["Cloudinary:ApiSecret"];
        var baseUrl = configuration["Cloudinary:UploadBaseUrl"] ?? "https://api.cloudinary.com";
        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret)
            || !Uri.TryCreate(baseUrl.TrimEnd('/') + $"/v1_1/{Uri.EscapeDataString(cloudName)}/auto/upload", UriKind.Absolute, out var uri)) return null;

        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"timestamp={timestamp}{apiSecret}"))).ToLowerInvariant();
        using var body = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        body.Add(file, "file", "upload.bin");
        body.Add(new StringContent(apiKey), "api_key");
        body.Add(new StringContent(timestamp), "timestamp");
        body.Add(new StringContent(signature), "signature");

        try
        {
            using var response = await client.PostAsync(uri, body, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            return document.RootElement.TryGetProperty("secure_url", out var reference) ? reference.GetString() : null;
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
    }
}
