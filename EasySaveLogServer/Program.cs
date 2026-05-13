using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var logsFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(logsFolder);

object fileLock = new object();

app.MapGet("/", () => "EasyLog Central Server is running");

// Receive logs from EasySave clients
app.MapPost("/api/logs", async (HttpRequest request) =>
{
    try
    {
        // Read the JSON body sent by EasySave
        string body = await new StreamReader(request.Body).ReadToEndAsync();

        // Get the sender's IP address 
        string senderIP = request.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "Unknown";

        // Parse the log entry sent by EasySave
        JsonDocument originalEntry = JsonDocument.Parse(body);

        var enrichedEntry = new Dictionary<string, object>
        {
            ["SenderIP"] = senderIP,
            ["ReceivedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // Copy all fields from the original log entry
        foreach (JsonProperty prop in originalEntry.RootElement.EnumerateObject())
        {
            enrichedEntry[prop.Name] = prop.Value.Clone();
        }

        // Daily file path
        string today = DateTime.Now.ToString("yyyy-MM-dd");
        string jsonPath = Path.Combine(logsFolder, $"{today}.json");
        string xmlPath = Path.Combine(logsFolder, $"{today}.xml");

        // Thread
        lock (fileLock)
        {
            // JSON
            List<Dictionary<string, object>> jsonLogs = new();

            if (File.Exists(jsonPath))
            {
                try
                {
                    string existing = File.ReadAllText(jsonPath);
                    jsonLogs = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                        existing) ?? new();
                }
                catch
                {
                    jsonLogs = new();
                }
            }

            jsonLogs.Add(enrichedEntry);

            File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonLogs,
                new JsonSerializerOptions { WriteIndented = true }));

            // XML 
            using (StreamWriter writer = new StreamWriter(xmlPath))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                writer.WriteLine("<Logs>");

                foreach (var log in jsonLogs)
                {
                    writer.WriteLine("  <LogEntry>");
                    foreach (var kvp in log)
                    {
                        // Clean the value for XML
                        string value = kvp.Value?.ToString() ?? "";
                        writer.WriteLine($"    <{kvp.Key}>{value}</{kvp.Key}>");
                    }
                    writer.WriteLine("  </LogEntry>");
                }

                writer.WriteLine("</Logs>");
            }
        }

        return Results.Ok(new { Status = "Log received", From = senderIP });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { Error = ex.Message });
    }
});

app.Run();