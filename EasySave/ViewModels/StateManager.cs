using SoftwareEngineeringProject;
using System.Globalization;
using System.Text.Json;

public class StateManager
{

    private readonly string _stateFilesPath;

    private readonly JsonSerializerOptions _jsonOptions;

    public StateManager()
    {
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string easySaveFolder = Path.Combine(appDataFolder, "EasySave");

        if ( !Directory.Exists(easySaveFolder))
        {
            Directory.CreateDirectory(easySaveFolder);
        }

        _stateFilesPath = Path.Combine(easySaveFolder, "state.json");
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public void SaveState(List<StateEntry> states)
    {
        string jsonString = JsonSerializer.Serialize(states, _jsonOptions);
        File.WriteAllText(_stateFilesPath, jsonString);
    }

    public List<StateEntry> LoadStates()
    {
        if (!File.Exists(_stateFilesPath))
        {
            return new List<StateEntry>();
        }

        string jsonString = File.ReadAllText(_stateFilesPath);

        List<StateEntry> states = JsonSerializer.Deserialize<List<StateEntry>>(jsonString) ?? new List<StateEntry>(); 
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