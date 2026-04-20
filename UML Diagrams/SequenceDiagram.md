sequenceDiagram

actor User
participant ConsoleView
participant JobManager
participant CommandParser
participant BackupJob
participant IBackupStrategy
participant FileService
participant IObserver
participant Logger
participant StateManager

%% ================= ENTRY =================
User ->> ConsoleView: Run EasySave.exe 
ConsoleView ->> JobManager: ExecuteSelection(input)

JobManager ->> CommandParser: Parse(input)
CommandParser -->> JobManager: List<JobIds>

%% ================= JOB EXECUTION =================
loop For each JobId
    JobManager ->> BackupJob: Execute()

    BackupJob ->> IBackupStrategy: Execute(src, dest, FileService, callback)

    loop For each file in directory
        IBackupStrategy ->> FileService: CopyFile(src, dest)
        FileService -->> IBackupStrategy: file copied

        %% REAL-TIME UPDATE FLOW
        IBackupStrategy ->> BackupJob: FileTransferUpdate(progress)

        BackupJob ->> IObserver: Notify(FileTransferUpdate)

        par Observer real-time updates
            IObserver ->> Logger: Update(log entry)
            IObserver ->> StateManager: Update(job state)
            IObserver ->> ConsoleView: Update(progress UI)
        end
    end
end

%% ================= END =================
JobManager -->> ConsoleView: Execution finished