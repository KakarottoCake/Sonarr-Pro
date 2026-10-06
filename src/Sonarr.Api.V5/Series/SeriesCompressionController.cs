using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.Series;

// The optional host worker owns encoders and persistent jobs. Sonarr never runs
// shell commands, accepts filesystem paths, or needs access to the Docker socket.
[V5ApiController("series")]
public class SeriesCompressionController : Controller
{
    private static readonly string SocketPath = Environment.GetEnvironmentVariable("SONARR_COMPRESSION_SOCKET") ?? "/compression/worker.sock";
    private static readonly HttpClient WorkerClient = CreateClient();
    private readonly ISeriesService _seriesService;

    public SeriesCompressionController(ISeriesService seriesService)
    {
        _seriesService = seriesService;
    }

    [HttpGet("compression/capabilities")]
    public Task<ContentResult> GetCapabilities(CancellationToken cancellationToken)
    {
        return Forward(HttpMethod.Get, "/capabilities", null, cancellationToken);
    }

    [HttpGet("{id:int}/compression")]
    public Task<ContentResult> GetCompression(int id, CancellationToken cancellationToken)
    {
        _seriesService.GetSeries(id);
        return Forward(HttpMethod.Get, $"/series/{id}", null, cancellationToken);
    }

    [HttpPost("{id:int}/compression")]
    public Task<ContentResult> StartCompression(int id, [FromBody] SeriesCompressionRequest request, CancellationToken cancellationToken)
    {
        _seriesService.GetSeries(id);
        return Forward(HttpMethod.Post, $"/series/{id}", JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken);
    }

    [HttpDelete("{id:int}/compression")]
    public Task<ContentResult> CancelCompression(int id, CancellationToken cancellationToken)
    {
        _seriesService.GetSeries(id);
        return Forward(HttpMethod.Delete, $"/series/{id}", null, cancellationToken);
    }

    private static HttpClient CreateClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = async (_, token) =>
            {
                var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                try
                {
                    await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };

        return new HttpClient(handler) { BaseAddress = new Uri("http://compression"), Timeout = TimeSpan.FromSeconds(30) };
    }

    private static async Task<ContentResult> Forward(HttpMethod method, string path, string? body, CancellationToken cancellationToken)
    {
        if (!global::System.IO.File.Exists(SocketPath))
        {
            return Unavailable(method);
        }

        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using var response = await WorkerClient.SendAsync(request, cancellationToken);
            return new ContentResult
            {
                Content = await response.Content.ReadAsStringAsync(cancellationToken),
                ContentType = "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException)
        {
            return Unavailable(method);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Unavailable(method);
        }
    }

    private static ContentResult Unavailable(HttpMethod method)
    {
        return new ContentResult
        {
            Content = "{\"available\":false,\"job\":null,\"modes\":[],\"message\":\"Compression worker is not connected.\"}",
            ContentType = "application/json",
            StatusCode = method == HttpMethod.Get ? 200 : 503
        };
    }
}

public class SeriesCompressionRequest
{
    public string Mode { get; set; } = string.Empty;
    public int MinSizeMb { get; set; }
}
