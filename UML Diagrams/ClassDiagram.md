classDiagram
    %% --- PRESENTATION ---
    class ConsoleView {
        -MainViewModel viewModel
        +ShowMenu() void
        +ReadInput() string
    }

    class MainViewModel {
        -BackupProcessor processor
        -LanguageManager langManager
        +RunJob(int id) void
        +ChangeLanguage(string lang) void
    }

    %% --- MODELS (Data Structures) ---
    class BackupJob {
        +string Name
        +string SourceDir
        +string TargetDir
        +string Type
    }

    class LogEntry {
        +DateTime Timestamp
        +string BackupName
        +string SourcePath
        +string DestinationPath
        +long FileSize
        +int TransferTime
    }

    class StateEntry {
        +string JobName
        +string Status
        +int Progress
        +long RemainingSize
        +string CurrentSourceFile
    }

    %% --- LOGIC & STRATEGY ---
    class BackupProcessor {
        -IBackupStrategy strategy
        -EasyLog logger
        -StateManager stateManager
        +Execute(BackupJob job) void
    }

    class IBackupStrategy {
        <<interface>>
        +Backup(BackupJob job) void
    }

    class FullBackup {
        +Backup(BackupJob job) void
    }

    class DifferentialBackup {
        +Backup(BackupJob job) void
    }

    %% --- SERVICES (Handlers) ---
    class EasyLog {
        +Save(LogEntry entry) void
    }

    class StateManager {
        +Update(StateEntry state) void
    }

    class LanguageManager {
        <<Singleton>>
        -static LanguageManager _instance
        -string currentLanguage
        +static GetInstance() LanguageManager
        +SetLanguage(string lang) void
        +GetText(string key) string
    }

    %% --- RELATIONS ---
    
    ConsoleView --> MainViewModel : interacts with
    MainViewModel --> BackupProcessor : controls
    MainViewModel --> LanguageManager : uses
    
    IBackupStrategy <|.. FullBackup : implements
    IBackupStrategy <|.. DifferentialBackup : implements
    BackupProcessor --> IBackupStrategy : uses
    BackupProcessor --> EasyLog : calls
    BackupProcessor --> StateManager : calls
    
    BackupProcessor ..> BackupJob : reads
    EasyLog ..> LogEntry : writes
    StateManager ..> StateEntry : manages