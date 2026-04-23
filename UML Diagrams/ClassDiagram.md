classDiagram
    %% --- PRESENTATION ---
    class ConsoleView {
        -MainViewModel viewModel
        +Run(string[]: args) void
        -DisplayMenu() void
        -HandleUserInput() string
        -DisplayJobs() void
        -RunMethodResult(bool success, string successMessage, string failureMessage) void
    }

    class MainViewModel {
        -BackupProcessor _backupProcessor
        -LanguageManager _langManager
        -List<BackupJob> _jobs
        +GetText(string key) string
        +ChangeLanguage(string lang) void
        +CreateJob(string name, string source, string target, string type) bool
        +DeleteJob(string input) bool
        +RunJob(int id) bool
        +GetJobs() List<BackupJobs>
        -ParseIndices(string input, int maxcount) List<int>
    }

    %% --- MODELS (Data Structures) ---
    class BackupJob {
        +string Name
        +string SourceDir
        +string TargetDir
        +BackupType Type
    }

    class LogEntry {
        +string Timestamp
        +string? BackupName
        +string? SourceFilePath
        +string? TargetFilePath
        +long FileSize
        +long FileTransferTimeMs
    }

    class StateEntry {
        +string Name
        +BackupStatus State
        +int Progression
        +long TotalFilesSize
        +int TotalFilesToCopy
        +string SourceFilePath
        +string TargetFilePath
        +int NbFilesLeftToDo
        +long SizeRemaining
        +DateTime LastRun
    }

    %% --- LOGIC & STRATEGY ---
    class BackupProcessor {
        -IBackupStrategy strategy
        -StateManager stateManager
        -LogService _logService
        +Execute(BackupJob job) void
    }

    class IBackupStrategy {
        <<interface>>
        +Backup(BackupJob jobm LogService logService, Action<string, string, long> onFileCopied) void
    }

    class FullBackupStrategy {
        -LanguageManager _languageManager
        +Backup(BackupJob jobm LogService logService, Action<string, string, long> onFileCopied) void
    }

    class DifferentialBackupStrategy {
        -LanguageManager _languageManager
        +Backup(BackupJob jobm LogService logService, Action<string, string, long> onFileCopied) void
    }

    %% --- SERVICES (Handlers) ---
    class LogService {
        -string _logFolder
        +Save(LogEntry entry) void
    }

    class StateManager {
        -string _stateFilesPath
        -JsonSerializerOptions _jsonOptions
        +SaveState(List<StateEntry> states) void
        +LoadStates() List<StateEntry> 
        +UpdateJobState(StateEntry state) void
        +ClearState() void
    }

    class LanguageManager {
        <<Singleton>>
        -static LanguageManager _instance
        -Dictionary<string, string> _translations
        -string _currentLanguage
        +static GetInstance() LanguageManager
        +SetLanguage(string lang) void
        -LoadTranslations() void
        +GetText(string key) string
    }

    class BackupType {
        <<Enumeration>>
        Full,
        Differential
    }

    class BackupStatus {
        <<Enumeration>>
        Inactive,
        In_Progress,
        Ended,
        Error
    }

%% --- RELATIONS ---
    ConsoleView "1" --* "1" MainViewModel : interacts with
    MainViewModel "1" --* "1" BackupProcessor : controls
    MainViewModel "1" --* "1" LanguageManager : uses
    MainViewModel "1" --o "*" BackupJob : manages
    
    IBackupStrategy <|.. FullBackupStrategy : implements
    IBackupStrategy <|.. DifferentialBackupStrategy : implements
    BackupProcessor "1" --* "1" IBackupStrategy : uses
    BackupProcessor "1" --* "1" LogService : uses
    BackupProcessor "1" --* "1" StateManager : updates
    
    BackupProcessor ..> BackupJob : processes
    LogService ..> LogEntry : creates
    StateManager ..> StateEntry : persists
    BackupJob ..> BackupType : typed by
    StateEntry ..> BackupStatus : reflects