using System.Text.Json;
using System.Xml.Serialization;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/api/logs", async (HttpRequest request) =>
{
    var body = await new StreamReader(request.Body).ReadToEndAsync();

    var logsFolder = Path.Combine(AppContext.BaseDirectory, "Logs");
    Directory.CreateDirectory(logsFolder);

    var jsonPath = Path.Combine(logsFolder, $"{DateTime.Now:yyyy-MM-dd}.json");
    var xmlPath = Path.Combine(logsFolder, $"{DateTime.Now:yyyy-MM-dd}.xml");

    var logEntry = JsonSerializer.Deserialize<object>(body);

    //JSON
    List<object> logs = new();

    if (File.Exists(jsonPath))
    {
        var existing = await File.ReadAllTextAsync(jsonPath);
        logs = JsonSerializer.Deserialize<List<object>>(existing) ?? new();
    }

    logs.Add(logEntry);

    await File.WriteAllTextAsync(jsonPath,
        JsonSerializer.Serialize(logs, new JsonSerializerOptions { WriteIndented = true })
    );

    //XML
    var serializer = new XmlSerializer(typeof(List<object>));
    using var writer = new StreamWriter(xmlPath);
    serializer.Serialize(writer, logs);

    return Results.Ok();
});

app.Run();