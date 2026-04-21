sequenceDiagram
    autonumber
    actor U as User
    participant CV as ConsoleView
    participant VM as MainViewModel
    participant BP as BackupProcessor
    participant BS as IBackupStrategy
    participant SM as StateManager
    participant EL as EasyLog

    U->>CV: Input Job ID
    CV->>VM: RunJob(id)
    
    activate VM
    VM->>BP: Execute(job)
    activate BP
    
    %% State: Started
    BP->>SM: Update(stateEntry: "Started")
    
    BP->>BS: Backup(job)
    activate BS
    
    %% State: In Progress (Simulation)
    loop During execution
        BS-->>BP: Progress details
        BP->>SM: Update(stateEntry: "In Progress")
    end
    
    BS-->>BP: Execution finished
    deactivate BS
    
    %% State: Finished & Logging
    par Finalization
        BP->>SM: Update(stateEntry: "Finished")
        BP->>EL: Save(logEntry)
    end
    
    BP-->>VM: Task Completed
    deactivate BP
    
    VM-->>CV: Notify View
    deactivate VM
    
    CV-->>U: Display Success Message