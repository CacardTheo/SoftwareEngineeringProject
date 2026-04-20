sequenceDiagram
    actor User
    participant CV as ConsoleView
    participant CP as CommandParser
    participant BM as BackupManager
    participant CM as ConfigManager
    participant SM as StateManager
    participant BS as IBackupStrategy
    participant L as Logger (EasyLog.dll)

    User->>CV: Run("EasySave.exe 1")
    CV->>CP: Parse(args)
    CP-->>CV: List<Int> {1}
    CV->>BM: ExecuteJob(1)
    
    BM->>CM: LoadJobs()
    CM-->>BM: List<BackupJob>
    
    BM->>BM: SelectStrategy(job.Type)
    Note right of BM: Returns FullBackupStrategy<br/>or DifferentialBackupStrategy
    
    BM->>SM: UpdateState(entry: Active)
    BM->>BS: Execute(job, onLog, onState)
    
    rect rgb(240, 240, 240)
        Note over BS, L: loop [For each file in source directory]
        BS->>BS: Copy file from source to target
        BS->>L: Log(LogEntry)
        L->>L: Write to daily JSON file
        BS->>SM: UpdateState(progress++)
    end

    BS-->>BM: Execution complete
    BM->>SM: UpdateState(entry: End)
    BM-->>CV: Job completed
    CV-->>User: Display "Backup complete"