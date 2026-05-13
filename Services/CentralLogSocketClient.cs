using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using EasyLog;
using EasySaveWpf;

namespace EasySaveWpf.Services;

internal static class CentralLogSocketClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Sends one log entry to the central server without blocking the backup thread for long.
    /// </summary>
    public static void TrySendFireAndForget(AppSettings settings, LogEntry entry)
    {
        if (!LogSocketEndpoint.TryParse(settings.DockerLogServerUrl, out string host, out int port))
            return;

        string format = settings.LogFormat == LogFormat.Xml ? "xml" : "json";
        _ = Task.Run(() => TrySendOnceAsync(host, port, entry, format));
    }

    private static async Task TrySendOnceAsync(string host, int port, LogEntry entry, string format)
    {
        try
        {
            JsonObject payload = JsonNode.Parse(JsonSerializer.Serialize(entry, JsonOptions))!.AsObject();
            payload["machineName"] = Environment.MachineName;
            payload["userName"] = Environment.UserName;
            payload["logFormat"] = format;

            byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);

            await using NetworkStream stream = client.GetStream();
            byte[] lenBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)body.Length);
            await stream.WriteAsync(lenBytes.AsMemory(0, 4), cts.Token).ConfigureAwait(false);
            await stream.WriteAsync(body, cts.Token).ConfigureAwait(false);
            await stream.FlushAsync(cts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Central logging must never break backups.
        }
    }
}
