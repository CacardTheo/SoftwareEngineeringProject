using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;

await CentralLogServer.RunAsync().ConfigureAwait(false);

internal static class CentralLogServer
{
    public static async Task RunAsync()
    {
        string logsFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
        Directory.CreateDirectory(logsFolder);

        int port = 5132;
        string? portEnv = Environment.GetEnvironmentVariable("EASYSAVE_LOG_PORT");
        if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int parsed) && parsed > 0 && parsed <= 65535)
            port = parsed;

        var fileLock = new object();
        using var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();

        Console.WriteLine($"EasySave central log server listening on TCP port {port}. Logs folder: {logsFolder}");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            while (!cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client, logsFolder, fileLock), CancellationToken.None);
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task HandleClientAsync(TcpClient client, string logsFolder, object fileLock)
    {
        try
        {
            await using NetworkStream stream = client.GetStream();

            int length = await ReadInt32BigEndianAsync(stream).ConfigureAwait(false);
            if (length <= 0 || length > 10_000_000)
                return;

            byte[] body = new byte[length];
            await ReadExactlyAsync(stream, body, length).ConfigureAwait(false);

            string jsonText = Encoding.UTF8.GetString(body);
            JsonNode? parsed = JsonNode.Parse(jsonText);
            if (parsed is not JsonObject entryObj)
                return;

            string senderIp = ((IPEndPoint?)client.Client.RemoteEndPoint)?.Address.ToString() ?? "Unknown";
            entryObj["SenderIP"] = senderIp;
            entryObj["ReceivedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            string jsonPath = Path.Combine(logsFolder, $"{today}.json");
            string xmlPath = Path.Combine(logsFolder, $"{today}.xml");

            lock (fileLock)
            {
                var logs = new JsonArray();
                if (File.Exists(jsonPath))
                {
                    try
                    {
                        JsonNode? existing = JsonNode.Parse(File.ReadAllText(jsonPath));
                        if (existing is JsonArray arr)
                            logs = arr;
                        else if (existing is JsonObject one)
                        {
                            logs = new JsonArray();
                            logs.Add(one);
                        }
                    }
                    catch
                    {
                        logs = new JsonArray();
                    }
                }

                logs.Add(entryObj);

                var writeOptions = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(jsonPath, logs.ToJsonString(writeOptions));

                WriteLogsAsXml(logs, xmlPath);
            }
        }
        catch
        {
            // Ignore malformed or aborted connections.
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task<int> ReadInt32BigEndianAsync(NetworkStream stream)
    {
        byte[] buf = new byte[4];
        await ReadExactlyAsync(stream, buf, 4).ConfigureAwait(false);
        return BinaryPrimitives.ReadInt32BigEndian(buf);
    }

    private static async Task ReadExactlyAsync(NetworkStream stream, byte[] buffer, int length)
    {
        int offset = 0;
        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset)).ConfigureAwait(false);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }

    private static string JsonNodeToPlainText(JsonNode? node)
    {
        if (node is null)
            return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out string? s))
                return s ?? string.Empty;
            if (value.TryGetValue<long>(out long l))
                return l.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<int>(out int i))
                return i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<double>(out double d))
                return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.TryGetValue<bool>(out bool b))
                return b ? "true" : "false";
        }

        return node.ToJsonString();
    }

    private static void WriteLogsAsXml(JsonArray logs, string xmlPath)
    {
        var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
        using var writer = XmlWriter.Create(xmlPath, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement("Logs");

        foreach (JsonNode? node in logs)
        {
            if (node is not JsonObject obj)
                continue;

            writer.WriteStartElement("LogEntry");
            foreach (KeyValuePair<string, JsonNode?> prop in obj)
            {
                string name = XmlConvert.EncodeName(prop.Key);
                writer.WriteStartElement(name);
                writer.WriteString(JsonNodeToPlainText(prop.Value));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
    }
}
