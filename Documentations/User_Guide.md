
### 1. Where to put your Settings
The application looks for your backup settings in a specific, secure folder. Before starting, you need to copy your settings file.

**Step:**
1. Press the **Windows Key + R** on your keyboard.
2. Type `%AppData%` and press **Enter**.
3. Open the **EasySave** folder.
4. Place your `backup_jobs.json` file inside that folder.

*If you don't do this, the application will show an "Invalid ID" because it won't see your backup list.*

---

### 2. Choosing Folders for Backup
For the best results, avoid using "System" folders (like `C:\Windows\Temp`). 
* **Why?** Windows protects these folders for your security, which often causes an "Access Denied" error.
* **Pro Tip:** Create a dedicated folder for your data (e.g., `C:\MyWork` or `D:\Backups`). This ensures the app can copy your files without interruptions.

---

### 3. Finding Your Logs
EasySave keeps a record of every file it copies. You can find these logs at any time in:
`%AppData%\EasySave\Logs\`

Each file is named by the date (e.g., `2026-04-22.json`), making it easy to track your history.

---
---

### 4. Priority File Extensions

In **Settings**, you can define a list of file extensions that must always be backed up first (e.g. `.key`, `.cfg`).

When a backup runs, files with those extensions are copied before any other file.
If multiple jobs run at the same time, no job will start copying non-priority files until **every** priority file across **all** running jobs has been fully copied.

One extension per line, include the dot (e.g. `.key`).

---

### 5. Large File Threshold

In **Settings**, the **Large File Threshold (KB)** field limits how many large files can be transferred simultaneously.

- When set to a value greater than `0`, at most **one** file larger than that size can be copied at a time across all running jobs.
- Other jobs may still copy smaller files freely in parallel — only large files wait.
- Set to `0` (the default) to disable the limit entirely.

**Example:** threshold = `10240` (10 MB). If Job A is copying a 500 MB archive, Job B must wait before it can start copying its own large files. But both jobs can still copy small files simultaneously.

---

**Need Help?**
If you see an "Access Denied" message, try running the application as an **Administrator** or choosing a source folder that isn't owned by Windows.
