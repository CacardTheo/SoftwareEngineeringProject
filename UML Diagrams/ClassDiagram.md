classDiagram
    %% ===================== INTERFACES =====================
    class IBackupStrategy {
        <<interface>>
        +Backup(job, logService, settings, cryptoService, onFileCopied, businessGate, userPauseGate, ct) void
    }

    class INotifyPropertyChanged {
        <<interface>>
        +PropertyChanged : PropertyChangedEventHandler
    }

    class ICommand {
        <<interface>>
        +CanExecute(parameter) bool
        +Execute(parameter) void
        +CanExecuteChanged : EventHandler
    }

    class IDisposable {
        <<interface>>
        +Dispose() void
    }

    %% ===================== ENUMS =====================
    class BackupStatus {
        <<enumeration>>
        Inactive
        In_Progress
        Ended
        Error
    }

    class BackupType {
        <<enumeration>>
        Full
        Differential
    }

    class LogMode {
        <<enumeration>>
        Local
        Centralized
        Both
    }

    class LogFormat {
        <<enumeration>>
        Json
        Xml
    }

    %% ===================== MODELS =====================
    class BackupJob {
        +Name : string
        +SourceDir : string
        +TargetDir : string
        +Type : BackupType
    }

    class AppSettings {
        +Language : string
        +EncryptedExtensions : List~string~
        +EncryptionKey : string
        +BusinessSoftwareProcesses : List~string~
        +LogFormat : LogFormat
        +StateFormat : LogFormat
        +LogMode : LogMode
        +DockerLogServerUrl : string
        +PrioritizedExtensions : List~string~
        +LargeFileSizeThresholdKb : int
    }

    class StateEntry {
        +Name : string
        +State : BackupStatus
        +Progression : int
        +TotalFilesSize : long
        +TotalFilesToCopy : int
        +SourceFilePath : string
        +TargetFilePath : string
        +NbFilesLeftToDo : int
        +SizeRemaining : long
        +LastRun : DateTime
    }

    class BackupSyncContext {
        +LargeFileSizeThresholdBytes : long
        -_priorityFilesRemaining : int
        -_allPriorityDone : ManualResetEventSlim
        -_largeFileSemaphore : SemaphoreSlim
        +BackupSyncContext(largeFileSizeThresholdKb)
        +RegisterPriorityFiles(count) void
        +NotifyPriorityFileDone() void
        +WaitForAllPriorityFiles(ct) void
        +AcquireLargeFileSlot() void
        +ReleaseLargeFileSlot() void
        +Dispose() void
    }

    %% ===================== STRATEGIES =====================
    class BackupStrategyBase {
        <<abstract>>
        #_languageManager : LanguageManager
        #_context : BackupSyncContext
        +Backup(job, logService, settings, cryptoService, onFileCopied, businessGate, userPauseGate, ct) void*
        #CopyGroup(group, isPriorityGroup, ...) void
        #CopySingleFile(...)$ void
        #TryEncrypt(targetPath, extension, cryptoService, settings)$ long
    }

    class FullBackupStrategy {
        +FullBackupStrategy(context)
        +Backup(...) void
    }

    class DifferentialBackupStrategy {
        +DifferentialBackupStrategy(context)
        +Backup(...) void
    }

    %% ===================== VIEWMODELS =====================
    class ViewModelBase {
        <<abstract>>
        +LangMgr : LanguageManager
        +PropertyChanged : PropertyChangedEventHandler
        #OnPropertyChanged(propertyName) void
        #SetField(field, value, propertyName) bool
    }

    class MainViewModel {
        -_settingsManager : SettingsManager
        -_settings : AppSettings
        -_backupProcessor : BackupProcessor
        -_configManager : ConfigManager
        -_jobs : List~BackupJob~
        -_businessSoftwareMonitor : BusinessSoftwareMonitor
        +Jobs : ObservableCollection~BackupJobViewModel~
        +SelectedLanguage : string
        +RunAllCommand : Command
        +OpenSettingsCommand : ICommand
        +AddJobCommand : ICommand
        +RequestAddJob : Action~Action~BackupJob~~
        +RequestSettings : Action~AppSettings, Action~AppSettings~~
        +MainViewModel()
        +AddJob(job) bool
        +DeleteJob(index) bool
        +RunJob(input) bool
        +RunAllJobs() bool
        +RunJobByIndex(index, syncContext, userPauseGate, ct) bool
        +GetJobs() List~BackupJob~
        +ApplySettings(updated) void
        +Cleanup() void
    }

    class BackupJobViewModel {
        -_userPauseGate : ManualResetEventSlim
        -_cts : CancellationTokenSource
        +Job : BackupJob
        +Name : string
        +Status : BackupStatus
        +Progression : int
        +IsRunning : bool
        +IsPaused : bool
        +BlockedByBusinessSoftware : bool
        +CurrentFile : string
        +ErrorMessage : string
        +RunCommand : Command
        +DeleteCommand : Command
        +PauseCommand : Command
        +ResumeCommand : Command
        +StopCommand : Command
        +ApplyProgress(jobName, status, progression, currentFile, blocked, errorMessage) void
        +ResetForNewRun() void
        +Dispose() void
    }

    class AddJobViewModel {
        +Name : string
        +SourceDir : string
        +TargetDir : string
        +SelectedType : BackupType
        +ErrorMessage : string
        +Result : BackupJob
        +CreateCommand : ICommand
        +CancelCommand : ICommand
        +JobCreated : Action~BackupJob~
        +Cancelled : Action
        +TryCreate() void
    }

    class SettingsViewModel {
        +Language : string
        +LogFormat : LogFormat
        +StateFormat : LogFormat
        +BusinessProcesses : string
        +EncryptedExtensions : string
        +PrioritizedExtensions : string
        +LargeFileSizeThresholdKb : int
        +EncryptionKey : string
        +LogMode : LogMode
        +DockerLogServerUrl : string
        +LogModeOptions : List~LogModeDisplayOption~
        +SaveCommand : ICommand
        +CancelCommand : ICommand
        +Saved : Action~AppSettings~
        +Cancelled : Action
        +Save() void
    }

    class BackupProcessor {
        -_stateManager : StateManager
        -_businessSoftwareMonitor : BusinessSoftwareMonitor
        -_cryptoSoftService : CryptoSoftService
        -_settings : AppSettings
        -_businessSoftwareGate : ManualResetEventSlim
        +ProgressChanged : Action~string, BackupStatus, int, string, bool, string~
        +Execute(job, syncContext, userPauseGate, ct) bool
        +RaiseProgress(jobName, status, progression, currentFile, errorMessage) void
    }

    class LogModeDisplayOption {
        <<sealed>>
        +Mode : LogMode
        +Label : string
    }

    %% ===================== MANAGERS =====================
    class LanguageManager {
        -_instance : LanguageManager$
        -_translations : Dictionary~string, string~
        -_currentLanguage : string
        +Instance : LanguageManager$
        +this[key] : string
        +PropertyChanged : PropertyChangedEventHandler
        +GetInstance() LanguageManager$
        +SetLanguage(lang) void
        +GetAvailableLanguages() List~string~
        +GetText(key) string
    }

    class CryptoSoftService {
        -_instance : CryptoSoftService$
        -_globalMutex : Mutex
        +Instance : CryptoSoftService$
        +Encrypt(filePath, key) long
        +Dispose() void
    }

    class BusinessSoftwareMonitor {
        -_cts : CancellationTokenSource
        -_wasPreviouslyDetected : bool
        +OnBusinessSoftwareDetected : Action~string~
        +OnBusinessSoftwareClosed : Action
        +TryFindRunningBusinessSoftware(processNames, detectedProcess) bool
        +StartMonitoring(processNames) void
        +StopMonitoring() void
    }

    class ConfigManager {
        -_configFilePath : string
        +SaveJobs(jobs) void
        +LoadJobs() List~BackupJob~
    }

    class SettingsManager {
        -_settingsFilePath : string
        +SerializerOptions : JsonSerializerOptions$
        +Load() AppSettings
        +Save(settings) void
    }

    class StateManager {
        -_stateFolderPath : string
        -_format : LogFormat
        +SetFormat(format) void
        +SaveState(states) void
        +LoadStates() List~StateEntry~
        +UpdateJobState(updatedEntry) void
        +ClearState() void
    }

    class Command {
        -_execute : Action~object~
        -_canExecute : Func~object, bool~
        +CanExecuteChanged : EventHandler
        +CanExecute(parameter) bool
        +Execute(parameter) void
        +RaiseCanExecuteChanged() void
    }

    %% ===================== SERVICES =====================
    class BackupLogRouter {
        <<sealed>>
        -_settings : AppSettings
        -_localLogService : LogService
        +BackupLogRouter(settings)
        +Save(entry) void
    }

    class CentralLogSocketClient {
        <<static>>
        +TrySendFireAndForget(settings, entry)$ void
        +TrySendOnceAsync(host, port, entry, format)$ Task
    }

    class LogSocketEndpoint {
        <<static>>
        +TryParse(raw, host, port)$ bool
    }

    %% ===================== UTILS =====================
    class FileHelper {
        <<static>>
        +GetAppDataFolder()$ string
        +CopyFile(sourcePath, targetPath, onBytesWritten)$ void
    }

    %% ===================== VIEWS =====================
    class MainWindow {
        -_vm : MainViewModel
        +MainWindow()
    }

    class AddJobWindow {
        +JobCreated : Action~BackupJob~
        +Cancelled : Action
        +AddJobWindow()
    }

    class SettingsWindow {
        +Saved : Action~AppSettings~
        +Cancelled : Action
        +SettingsWindow(current)
    }

    class ConsoleView {
        -_viewModel : MainViewModel
        +ConsoleView()
        +Run(args) void
        +DisplayMenu() void
        +DisplayJobs() void
        +ConfigureSettings() void
    }

    %% ===================== INHERITANCE =====================
    INotifyPropertyChanged <|.. ViewModelBase
    INotifyPropertyChanged <|.. LanguageManager
    ICommand <|.. Command
    IDisposable <|.. BackupSyncContext
    IDisposable <|.. CryptoSoftService
    IDisposable <|.. BackupJobViewModel
    IBackupStrategy <|.. BackupStrategyBase
    BackupStrategyBase <|-- FullBackupStrategy
    BackupStrategyBase <|-- DifferentialBackupStrategy
    ViewModelBase <|-- MainViewModel
    ViewModelBase <|-- BackupJobViewModel
    ViewModelBase <|-- AddJobViewModel
    ViewModelBase <|-- SettingsViewModel

    %% ===================== COMPOSITION =====================
    MainViewModel *-- BackupProcessor
    MainViewModel *-- ConfigManager
    MainViewModel *-- SettingsManager
    MainViewModel *-- BusinessSoftwareMonitor
    MainViewModel "1" *-- "0..*" BackupJobViewModel
    MainViewModel --> AppSettings
    BackupProcessor *-- StateManager
    BackupProcessor --> BackupLogRouter
    BackupProcessor --> IBackupStrategy
    BackupProcessor --> BackupSyncContext
    BackupStrategyBase *-- BackupSyncContext
    BackupStrategyBase --> BackupLogRouter
    BackupStrategyBase --> FileHelper
    BackupLogRouter --> CentralLogSocketClient
    CentralLogSocketClient --> LogSocketEndpoint
    BackupJobViewModel --> BackupJob

    %% ===================== MODEL RELATIONSHIPS =====================
    BackupJob --> BackupType
    AppSettings --> LogMode
    AppSettings --> LogFormat
    StateEntry --> BackupStatus
    StateManager --> StateEntry

    %% ===================== SINGLETONS =====================
    MainViewModel --> LanguageManager
    MainViewModel --> CryptoSoftService
    BackupStrategyBase --> LanguageManager
    BackupProcessor --> CryptoSoftService

    %% ===================== VIEWS =====================
    MainWindow --> MainViewModel
    AddJobWindow --> AddJobViewModel
    SettingsWindow --> SettingsViewModel
    ConsoleView --> MainViewModel
    SettingsViewModel --> LogModeDisplayOption
