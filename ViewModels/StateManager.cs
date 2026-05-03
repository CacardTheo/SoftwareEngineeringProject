using SoftwareEngineeringProject;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

public class StateManager
{

    private readonly string _stateFilesPath;
    private string _format;

    private readonly JsonSerializerOptions _jsonOptions;

    public StateManager(string format = "JSON")
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string easySaveFolder = Path.Combine(appDataFolder, "EasySave");

        if ( !Directory.Exists(easySaveFolder))
        {
            Directory.CreateDirectory(easySaveFolder);
        }

        _format = format.Equals("XML", StringComparison.OrdinalIgnoreCase) ? "XML" : "JSON";
        string fileName = _format.Equals("XML") ? "state.xml" : "state.json";
        _stateFilesPath = Path.Combine(easySaveFolder, fileName);
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter()}
        };
    }

    public void SaveState(List<StateEntry> states)
    {
        if (_format.Equals("XML"))
        {
            SaveStateAsXml(states);
        }
        else
        {
            SaveStateAsJson(states);
        }
    }

    private void SaveStateAsJson(List<StateEntry> states)
    {
        string jsonString = JsonSerializer.Serialize(states, _jsonOptions);
        File.WriteAllText(_stateFilesPath, jsonString);
    }

    private void SaveStateAsXml(List<StateEntry> states)
    {
        using var sw = new System.IO.StringWriter();
        var settings = new System.Xml.XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8
        };
        using (var xw = System.Xml.XmlWriter.Create(sw, settings))
        {
            xw.WriteStartDocument();
            xw.WriteStartElement("States");
            foreach (var state in states)
            {
                xw.WriteStartElement("State");
                xw.WriteElementString("Name", state.Name);
                xw.WriteElementString("Status", state.State.ToString());
                xw.WriteElementString("LastRun", state.LastRun.ToString("O"));
                xw.WriteElementString("SourceFilePath", state.SourceFilePath);
                xw.WriteElementString("TargetFilePath", state.TargetFilePath);
                xw.WriteElementString("TotalFilesToCopy", state.TotalFilesToCopy.ToString());
                xw.WriteElementString("TotalFilesSize", state.TotalFilesSize.ToString());
                xw.WriteElementString("NbFilesLeftToDo", state.NbFilesLeftToDo.ToString());
                xw.WriteElementString("Progression", state.Progression.ToString());
                xw.WriteEndElement();
            }
            xw.WriteEndElement();
            xw.WriteEndDocument();
        }
        File.WriteAllText(_stateFilesPath, sw.ToString());
    }

    public List<StateEntry> LoadStates()
    {
        if (!File.Exists(_stateFilesPath))
        {
            return new List<StateEntry>();
        }

        if (_format.Equals("XML"))
        {
            return LoadStatesFromXml();
        }
        else
        {
            return LoadStatesFromJson();
        }
    }

    private List<StateEntry> LoadStatesFromJson()
    {
        string jsonString = File.ReadAllText(_stateFilesPath);
        List<StateEntry> states = JsonSerializer.Deserialize<List<StateEntry>>(jsonString, _jsonOptions) ?? new List<StateEntry>();
        return states;
    }

    private List<StateEntry> LoadStatesFromXml()
    {
        var states = new List<StateEntry>();
        try
        {
            var doc = XDocument.Load(_stateFilesPath);
            foreach (var stateElem in doc.Root?.Elements("State") ?? Enumerable.Empty<XElement>())
            {
                var state = new StateEntry
                {
                    Name = stateElem.Element("Name")?.Value ?? "Unknown",
                    SourceFilePath = stateElem.Element("SourceFilePath")?.Value ?? "",
                    TargetFilePath = stateElem.Element("TargetFilePath")?.Value ?? "",
                    State = Enum.TryParse<BackupStatus>(stateElem.Element("Status")?.Value ?? "Inactive", out var status) ? status : BackupStatus.Inactive,
                    TotalFilesToCopy = int.TryParse(stateElem.Element("TotalFilesToCopy")?.Value, out var totalFiles) ? totalFiles : 0,
                    TotalFilesSize = long.TryParse(stateElem.Element("TotalFilesSize")?.Value, out var totalSize) ? totalSize : 0,
                    NbFilesLeftToDo = int.TryParse(stateElem.Element("NbFilesLeftToDo")?.Value, out var nbLeft) ? nbLeft : 0,
                    Progression = int.TryParse(stateElem.Element("Progression")?.Value, out var prog) ? prog : 0,
                    LastRun = DateTime.TryParse(stateElem.Element("LastRun")?.Value, out var lastRun) ? lastRun : DateTime.Now
                };
                states.Add(state);
            }
        }
        catch
        {
            return new List<StateEntry>();
        }
        return states;
    }

    public void UpdateJobState(StateEntry updatedEntry)
    {
        List<StateEntry> allStates = LoadStates();

        bool found = false;
        for (int i = 0; i < allStates.Count; i++)
        {
            if (allStates[i].Name == updatedEntry.Name)
            {
                allStates[i] = updatedEntry;
                found = true;
                break;
            }
        }

        if (!found)
        {
            allStates.Add(updatedEntry);
        }
        SaveState(allStates);   
    }

    public void ClearState()
    {
        SaveState(new List<StateEntry>());
    }
}