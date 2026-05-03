
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

### 4. Choosing File Formats (NEW in V1.1)

**Option 8 - Log Format:**
Choose how your backup logs are stored:
- **JSON** - Easy to read, widely supported
- **XML** - Structured format, better for parsing

**Option 9 - State Format:**
Choose how job states are tracked:
- **JSON** - Compact and readable
- **XML** - Hierarchical structure

Your format choice is saved automatically and used for all future backups.

---
**Need Help?**
If you see an "Access Denied" message, try running the application as an **Administrator** or choosing a source folder that isn't owned by Windows.
