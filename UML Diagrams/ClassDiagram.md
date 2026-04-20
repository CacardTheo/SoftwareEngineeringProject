---
config:
  layout: dagre
---
classDiagram
direction TB

%% ===================== UI LAYER =====================
class ViewInterface

class ConsoleView {
    -ILanguage _currentLang
    +DisplayMenu()
    +Update(FileTransferUpdate update)
    +ChangeLanguage(string code)
}

class ILanguage {
    +GetText(string key) string
}

class FrLanguage
class EnLanguage

class LanguageFactory {
    +CreateLanguage(string code) ILanguage
}

ViewInterface --> JobManager
ConsoleView --> JobManager
ConsoleView --> ILanguage
ConsoleView --> LanguageFactory
ILanguage <|-- FrLanguage
ILanguage <|-- EnLanguage

%% ===================== CORE LAYER =====================
class JobManager {
    -List~BackupJob~ _jobs
    -StateManager _stateManager
    +AddJob(BackupJob job)
    +ExecuteSelection(string input)
}

class CommandParser {
    +Parse(string input) List~int~
}

class BackupJob {
    +string Name
    +string SourceDir
    +string TargetDir
    +BackupType Type
    +Execute()
}

JobManager --> BackupJob
JobManager --> CommandParser
JobManager --> StateManager

%% ===================== STRATEGY LAYER =====================
class IBackupStrategy {
    +Execute(string src, string dest, FileService fs, Action~FileTransferUpdate~ cb)
}

class FullBackupStrategy {
}

class DifferentialBackupStrategy {
}

class FileService {
    +GetAllFiles(string path)
    +CopyFile(string src, string dest)
}

BackupJob --> IBackupStrategy
IBackupStrategy <|-- FullBackupStrategy
IBackupStrategy <|-- DifferentialBackupStrategy
IBackupStrategy --> FileService

%% ===================== OBSERVER LAYER =====================
class IObserver {
    +Update(FileTransferUpdate update)
}

class FileTransferUpdate
class JobState

BackupJob --> FileTransferUpdate
FileTransferUpdate --> JobState

BackupJob --> IObserver
IObserver <|-- ConsoleView
IObserver <|-- Logger
IObserver <|-- StateManager

%% ===================== STATE LAYER =====================
class StateManager {
    +SaveState(List~JobState~ states)
}

class IStateWriter
class JsonStateWriter

StateManager --> IStateWriter
IStateWriter <|-- JsonStateWriter

%% ===================== LOGGING LAYER =====================
class Logger {
    +LogAction(LogEntry entry)
}

class ILogWriter
class JsonLogWriter
class LogEntry
class ConfigManager

Logger --> ILogWriter
ILogWriter <|-- JsonLogWriter
Logger --> ConfigManager