classDiagram
    %% --- PRESENTATION LAYER ---
    subgraph Presentation
        class ConsoleView {
            -BackupManager manager
            -LanguageManager langManager
            -CommandParser parser
            +Run(string[] args) void
            -DisplayMenu() void
            -HandleUserInput() void
            -DisplayProgress(StateEntry) void
            +Update(LogEntry, StateEntry) void %% Implémentation Observer
        }
        class CommandParser {
            +Parse(string[] args) List~int~
            -ParseRange(string) List~int~
        }
        class LanguageManager {
            -string _currentLanguage
            -Dictionary~string, string~ _strings
            +GetString(string key) string
            +SetLanguage(string lang) void
        }
    end

    %% --- BUSINESS LOGIC LAYER (Sujet de l'Observer) ---
    subgraph BusinessLogic
        class BackupManager {
            -List~BackupJob~ jobs
            -List~IObserver~ _observers
            -StateManager _stateManager
            -ConfigManager _configManager
            -Logger _logger
            +Attach(IObserver) void
            +Detach(IObserver) void
            +ExecuteJobs(List~int~ indices) void
            -Notify(LogEntry, StateEntry) void
        }
        
        class IObserver {
            <<interface>>
            +Update(LogEntry, StateEntry) void
        }
    end

    %% --- DATA ACCESS & LOGS (Observers) ---
    subgraph DataAccess
        class ConfigManager {
            +SaveJobs(List~BackupJob~) void
            +LoadJobs() List~BackupJob~
        }
        class StateManager {
            -string _stateFilePath
            +UpdateState(StateEntry) void
        }
    end

    subgraph EasyLogDLL
        class Logger {
            -string _logDirectory
            +Log(LogEntry entry) void
            +Update(LogEntry, StateEntry) void %% Devient Observer
        }
    end

    %% --- STRATEGY PATTERN & FACTORY ---
    subgraph Strategy
        class IBackupStrategy {
            <<interface>>
            +Execute(BackupJob, Action~LogEntry, StateEntry~) void
        }
        class FullBackupStrategy {
            +Execute(BackupJob, Action~LogEntry, StateEntry~) void
            -CopyAllFiles(BackupJob, Action~LogEntry, StateEntry~) void
        }
        class DifferentialBackupStrategy {
            +Execute(BackupJob, Action~LogEntry, StateEntry~) void
            -GetModifiedFiles(BackupJob, Action~LogEntry, StateEntry~) void
        }
        class StrategyFactory {
            +static CreateStrategy(BackupType) IBackupStrategy
        }
    end

    %% --- MODELS ---
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
            +int Progress
            +string CurrentSourceFile
            +string CurrentDestFile
        }
    end

    %% --- RELATIONS ---
    
    %% Héritage et Réalisation
    IBackupStrategy <|.. FullBackupStrategy
    IBackupStrategy <|.. DifferentialBackupStrategy
    IObserver <|.. Logger
    IObserver <|.. StateManager
    IObserver <|.. ConsoleView

    %% Associations (Flèches pleines = Instances d'objet)
    ConsoleView "1" --> "1" BackupManager : gère
    BackupManager "1" --> "0..5" BackupJob : possède
    BackupManager "1" o-- "*" IObserver : liste d'observateurs
    
    %% Dépendances (Flèches pointillées = Utilisation ponctuelle)
    ConsoleView ..> CommandParser : utilise
    ConsoleView ..> LanguageManager : utilise
    BackupManager ..> StrategyFactory : demande la création
    StrategyFactory ..> IBackupStrategy : crée
    
    %% Relations vers les modèles
    ConfigManager ..> BackupJob : sérialise
    StateManager ..> StateEntry : écrit
    Logger ..> LogEntry : crée