classDiagram
    %% Presentation Layer
    subgraph Presentation
        class ConsoleView {
            -BackupManager manager
            -LanguageManager langManager
            +Run(string[] args) void
            -DisplayMenu() void
            -HandleUserInput() void
            -DisplayJobs() void
            -DisplayProgress(StateEntry) void
        }
        class CommandParser {
            +Parse(string[] args) List~int~
            -ParseRange(string) List~int~
            -ParseSelection(string) List~int~
        }
        class LanguageManager {
            -string _currentLanguage
            -Dictionary~string, string~ _strings
            +GetString(string key) string
            +SetLanguage(string lang) void
            +GetAvailableLanguages() List~string~
        }
    end

    %% Business Logic Layer
    subgraph BusinessLogic
        class BackupManager {
            -List~BackupJob~ jobs
            -IBackupStrategy _strategy
            -StateManager _stateManager
            -ConfigManager _configManager
            -Logger _logger
            +CreateJob(BackupJob) void
            +DeleteJob(int index) void
            +ExecuteJob(int index) void
            +ExecuteAllJobs() void
            +ExecuteJobs(List~int~ indices) void
            +GetJobs() List~BackupJob~
            -SelectStrategy(BackupType) IBackupStrategy
        }
    end

    %% Data Access Layer
    subgraph DataAccess
        class ConfigManager {
            -string _configFilePath
            +SaveJobs(List~BackupJob~) void
            +LoadJobs() List~BackupJob~
        }
        class StateManager {
            -string _stateFilePath
            +UpdateState(StateEntry) void
            +LoadState() List~StateEntry~
            +ClearState() void
        }
    end

    %% EasyLogDLL
    subgraph EasyLogDLL
        class Logger {
            -string _logDirectory
            +Log(LogEntry entry) void
            -GetDailyFilePath() string
            -SerializeEntry(LogEntry) string
        }
    end

    %% Strategy Pattern
    subgraph Strategy
        class IBackupStrategy {
            <<interface>>
            +Execute(BackupJob, Action~LogEntry~, Action~StateEntry~) void
        }
        class FullBackupStrategy {
            +Execute(BackupJob, Action~LogEntry~, Action~StateEntry~) void
            -CopyAllFiles(string src, string dest) void
        }
        class DifferentialBackupStrategy {
            +Execute(BackupJob, Action~LogEntry~, Action~StateEntry~) void
            -GetModifiedFiles(string src, DateTime since) List~string~
        }
    end

    %% Models
    subgraph Models
        class BackupJob {
            +string Name
            +string SourceDirectory
            +string TargetDirectory
            +BackupType Type
        }
        class LogEntry {
            +DateTime Timestamp
            +string BackupName
            +string SourcePath
            +string DestinationPath
            +long FileSize
            +long TransferTimeMs
        }
        class StateEntry {
            +string JobName
            +DateTime LastActionTimestamp
            +BackupStatus Status
            +int TotalFiles
            +long TotalSize
            +int Progress
            +int RemainingFiles
            +long RemainingSize
            +string CurrentSourceFile
            +string CurrentDestFile
        }
    end

    %% Enums
    subgraph Enums
        class BackupType {
            <<enumeration>>
            Full
            Differential
        }
        class BackupStatus {
            <<enumeration>>
            Active
            Inactive
            End
        }
    end

    %% Relationships
    ConsoleView ..> BackupManager : depends on
    ConsoleView --> CommandParser : uses
    ConsoleView --> LanguageManager : uses

    BackupManager "1" --> "*" BackupJob : manages
    BackupManager --> ConfigManager : uses
    BackupManager --> StateManager : uses
    BackupManager --> Logger : uses
    BackupManager --> IBackupStrategy : uses

    ConfigManager ..> BackupJob : serializes
    StateManager ..> StateEntry : writes
    Logger ..> LogEntry : creates

    IBackupStrategy <|.. FullBackupStrategy : implements
    IBackupStrategy <|.. DifferentialBackupStrategy : implements

    BackupJob --> BackupType : has
    StateEntry --> BackupStatus : has