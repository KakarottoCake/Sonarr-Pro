using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;
using Sonarr.Api.V5.LibraryTools;
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
    private readonly IManageCommandQueue _commands;

    public SeriesCompressionController(ISeriesService seriesService, IManageCommandQueue commands)
    {
        _seriesService = seriesService;
        _commands = commands;
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
    public async Task<ContentResult> StartCompression(int id, [FromBody] SeriesCompressionRequest request, CancellationToken cancellationToken)
    {
        _seriesService.GetSeries(id);
        await LibraryOperationGate.Gate.WaitAsync(cancellationToken);
        try
        {
            if (_commands.All().Any(c => c.Status is CommandStatus.Queued or CommandStatus.Started && ((c.Body is ActivateRetainedVersionCommand activation && activation.SeriesId == id) || (c.Body is RestoreRecycledFileCommand restore && restore.SeriesId == id))))
            {
                return new ContentResult { StatusCode = 409, ContentType = "application/json", Content = "{\"message\":\"Wait for the file restore or version change to finish before compressing this show.\"}" };
            }

            return await Forward(HttpMethod.Post, $"/series/{id}", JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)), cancellationToken);
        }
        finally
        {
            LibraryOperationGate.Gate.Release();
        }
    }

    [HttpDelete("{id:int}/compression")]
    public Task<ContentResult> CancelCompression(int id, CancellationToken cancellationToken)
    {
        _seriesService.GetSeries(id);
        return Forward(HttpMethod.Delete, $"/series/{id}", null, cancellationToken);
    }

    public static async Task<bool> HasActiveJob(int seriesId, CancellationToken cancellationToken)
    {
        if (!global::System.IO.File.Exists(SocketPath))
        {
            return false;
        }

        var response = await Forward(HttpMethod.Get, $"/series/{seriesId}", null, cancellationToken);
        if (response.StatusCode != 200 || response.Content == null)
        {
            throw new IOException("Unable to check compression. Try again when the worker is available.");
        }

        using var document = JsonDocument.Parse(response.Content);
        return document.RootElement.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object &&
            job.TryGetProperty("status", out var status) && status.GetString() is "queued" or "running" or "cancelling";
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
