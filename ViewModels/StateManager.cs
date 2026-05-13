using EasyLog;
using EasySaveWpf;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

public class StateManager
{

    private readonly string _stateFolderPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _stateLock = new();
    private LogFormat _format = LogFormat.Json;

    public StateManager()
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string easySaveFolder = Path.Combine(appDataFolder, "EasySave");

        if ( !Directory.Exists(easySaveFolder))
        {
            Directory.CreateDirectory(easySaveFolder);
        }

        _stateFolderPath = easySaveFolder;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter()}
        };
    }

    public void SetFormat(LogFormat format)
    {
        _format = format;
    }

    public void SaveState(List<StateEntry> states)
    {
        if (_format == LogFormat.Xml)
        {
            string xmlPath = Path.Combine(_stateFolderPath, "state.xml");
            var serializer = new XmlSerializer(typeof(List<StateEntry>));
            using FileStream stream = File.Create(xmlPath);
            serializer.Serialize(stream, states);
            return;
        }

        string jsonPath = Path.Combine(_stateFolderPath, "state.json");
        string jsonString = JsonSerializer.Serialize(states, _jsonOptions);
        File.WriteAllText(jsonPath, jsonString);
    }

    public List<StateEntry> LoadStates()
    {
        if (_format == LogFormat.Xml)
        {
            string xmlPath = Path.Combine(_stateFolderPath, "state.xml");
            if (!File.Exists(xmlPath))
            {
                return new List<StateEntry>();
            }

            try
            {
                var serializer = new XmlSerializer(typeof(List<StateEntry>));
                using FileStream stream = File.OpenRead(xmlPath);
                return (List<StateEntry>?)serializer.Deserialize(stream) ?? new List<StateEntry>();
            }
            catch
            {
                return new List<StateEntry>();
            }
        }

        string jsonPath = Path.Combine(_stateFolderPath, "state.json");
        if (!File.Exists(jsonPath))
            return new List<StateEntry>();

        try
        {
            string jsonString = File.ReadAllText(jsonPath);
            return JsonSerializer.Deserialize<List<StateEntry>>(jsonString, _jsonOptions) ?? new List<StateEntry>();
        }
        catch
        {
            return new List<StateEntry>();
        }
    }

    public void UpdateJobState(StateEntry updatedEntry)
    {
        lock (_stateLock)
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
                allStates.Add(updatedEntry);

            SaveState(allStates);
        }
    }

    public void ClearState()
    {
        lock (_stateLock)
        {
            SaveState(new List<StateEntry>());
        }
    }
}