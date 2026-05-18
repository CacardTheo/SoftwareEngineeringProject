# EasySave — Documentation technique (Livrable 3)

Ce document décrit l'architecture et l'implémentation complète de l'application, fichier par fichier, bloc par bloc. Il couvre chaque décision de conception, chaque primitive de thread et chaque flux de données du code actuel. Il signale également le code mort identifié.

---

## Table des matières

1. [Vue d'ensemble de l'architecture](#1-vue-densemble-de-larchitecture)
2. [Point d'entrée](#2-point-dentrée)
3. [Modèles de données](#3-modèles-de-données)
4. [Couche de persistance](#4-couche-de-persistance)
5. [Système de langues](#5-système-de-langues)
6. [Surveillance des logiciels métier](#6-surveillance-des-logiciels-métier)
7. [Chiffrement (CryptoSoftService)](#7-chiffrement-cryptosoftservice)
8. [Pipeline d'exécution des sauvegardes](#8-pipeline-dexécution-des-sauvegardes)
9. [Stratégies de sauvegarde](#9-stratégies-de-sauvegarde)
10. [Synchronisation inter-jobs (BackupSyncContext)](#10-synchronisation-inter-jobs-backupsynccontext)
11. [Routage des logs et logging centralisé](#11-routage-des-logs-et-logging-centralisé)
12. [EasySaveLogServer](#12-easysavelogserver)
13. [Couche ViewModels](#13-couche-viewmodels)
14. [Couche Views](#14-couche-views)
15. [Modèle de threading](#15-modèle-de-threading)
16. [Sécurité des threads — primitives et emplacements](#16-sécurité-des-threads--primitives-et-emplacements)
17. [Code mort identifié](#17-code-mort-identifié)

---

## 1. Vue d'ensemble de l'architecture

EasySave suit le pattern **MVVM** (Model – View – ViewModel), standard pour les applications Avalonia UI.

```
┌────────────────────────────────────────────────────────────┐
│  Views  (XAML + code-behind)                               │
│  MainWindow / AddJobWindow / SettingsWindow                │
│  → Connaissent uniquement leur ViewModel                   │
└───────────────────┬────────────────────────────────────────┘
                    │ Liaison DataContext
┌───────────────────▼────────────────────────────────────────┐
│  ViewModels                                                │
│  MainViewModel / BackupJobViewModel                        │
│  AddJobViewModel / SettingsViewModel                       │
│  → Exposent propriétés et commandes que les Views lient    │
│  → Orchestrent les services, ne touchent jamais l'UI       │
└───────┬──────────────────┬─────────────────────────────────┘
        │                  │
┌───────▼───────┐  ┌───────▼────────────────────────────────┐
│  Persistance  │  │  Pipeline de sauvegarde                │
│  ConfigManager│  │  BackupProcessor                       │
│  SettingsManager  │  ↳ FullBackupStrategy                 │
│  StateManager │  │  ↳ DifferentialBackupStrategy          │
│  LanguageManager  │  ↳ BackupStrategyBase (logique partagée)
│  BusinessSoftwareMonitor  BackupSyncContext (sync threads) │
│  CryptoSoftService        FileHelper (I/O bas niveau)      │
└───────────────┘  └────────────────────────────────────────┘
                              │
                   ┌──────────▼──────────────────────────────┐
                   │  Services                               │
                   │  BackupLogRouter                        │
                   │  CentralLogSocketClient                 │
                   │  LogSocketEndpoint                      │
                   └─────────────────────────────────────────┘
```

Les Views ne connaissent que leur ViewModel. Les ViewModels connaissent les services et le pipeline. Rien ne remonte dans l'autre sens.

---

## 2. Point d'entrée

**`Program.cs`**

```csharp
[STAThread]
public static void Main(string[] args)
{
    if (args.Length > 0)
    {
        RunCli(args[0]);
        return;
    }
    BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
}

private static void RunCli(string input)
{
    var model = new MainViewModel();
    bool success = model.RunJob(input);
    Environment.Exit(success ? 0 : 1);
}
```

- `[STAThread]` est requis par Avalonia (et WPF en général) pour que le thread principal soit en Single-Threaded Apartment, nécessaire pour les appels COM et l'interface graphique.
- Si `args` est non vide, on entre en mode CLI : on crée un `MainViewModel`, on appelle `RunJob(input)` avec la chaîne passée en argument (ex: `"1-3"` ou `"2"`), puis on quitte avec le code 0 ou 1 selon le succès.
- Si aucun argument, on lance l'application Avalonia normalement.
- `BuildAvaloniaApp()` configure Avalonia avec la détection automatique de plateforme et le logging vers la trace.

---

## 3. Modèles de données

### `Models/Enums/BackupStatus.cs`

```
Inactive     → job jamais exécuté, ou annulé, ou bloqué
In_Progress  → copie en cours
Ended        → dernière exécution terminée avec succès
Error        → dernière exécution a rencontré une exception fatale
```

### `Models/Enums/BackupType.cs`

```
Full          → copie tous les fichiers du répertoire source sans condition
Differential  → copie uniquement les fichiers absents ou plus récents à destination
```

### `Models/Data/BackupJob.cs`

Unité de travail persistée. Quatre propriétés nullable :

| Propriété | Type | Description |
|---|---|---|
| `Name` | `string?` | Identifiant unique, clé primaire dans le fichier de config et le state |
| `SourceDir` | `string?` | Chemin source — fichier ou répertoire |
| `TargetDir` | `string?` | Répertoire de destination |
| `Type` | `BackupType` | `Full` ou `Differential` |

`Name` sert de clé primaire dans `backup_jobs.json` et `state.json` — chaque recherche de job est une comparaison de chaîne sur ce champ.

### `Models/Data/AppSettings.cs`

Tous les paramètres configurables par l'utilisateur, persistés dans `settings.json` :

```csharp
public string Language { get; set; } = "en";
public List<string> EncryptedExtensions { get; set; } = new();
public string EncryptionKey { get; set; } = string.Empty;
public List<string> BusinessSoftwareProcesses { get; set; } = new();
public LogFormat LogFormat { get; set; } = LogFormat.Json;
public LogFormat StateFormat { get; set; } = LogFormat.Json;
public LogMode LogMode { get; set; } = LogMode.Local;
public string DockerLogServerUrl { get; set; } = "127.0.0.1:5132";
public List<string> PrioritizedExtensions { get; set; } = new();
public int LargeFileSizeThresholdKb { get; set; } = 0;
```

- `LargeFileSizeThresholdKb = 0` signifie que la limitation des gros fichiers est désactivée.
- `DockerLogServerUrl` accepte le format `host:port` ou une URL `http(s)://host:port/...` (voir `LogSocketEndpoint`).
- Les listes vides par défaut évitent les null checks partout dans le code.

### `Models/Data/StateEntry.cs`

Snapshot d'un job à un instant donné, écrit dans `state.json`/`state.xml` après chaque événement significatif :

| Champ | Description |
|---|---|
| `Name` | Nom du job (clé de recherche) |
| `State` | `BackupStatus` courant |
| `TotalFilesToCopy` | Nombre total de fichiers |
| `NbFilesLeftToDo` | Fichiers restants |
| `TotalFilesSize` | Taille totale en octets |
| `SizeRemaining` | Octets restants |
| `Progression` | Pourcentage 0–100 |
| `SourceFilePath` | Fichier source actuellement copié |
| `TargetFilePath` | Fichier destination actuellement copié |
| `LastRun` | Horodatage de la dernière mise à jour |

---

## 4. Couche de persistance

Tous les fichiers sont stockés dans `%APPDATA%\EasySave\`. Le chemin est résolu une seule fois via `FileHelper.GetAppDataFolder()`.

### `Utils/FileHelper.cs`

Deux responsabilités statiques :

**1. Résolution et cache du dossier AppData**

```csharp
private static string? _appDataFolder;

public static string GetAppDataFolder()
{
    if (_appDataFolder != null) return _appDataFolder;
    string folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EasySave");
    Directory.CreateDirectory(folder);  // idempotent
    _appDataFolder = folder;
    return folder;
}
```

- Le résultat est mis en cache dans un champ statique après le premier appel. `Directory.CreateDirectory` est idempotent (ne lance pas d'exception si le dossier existe déjà).
- `ConfigManager`, `SettingsManager` et `StateManager` appellent tous cette méthode dans leur constructeur.

**2. Copie de fichier par chunks**

```csharp
public static void CopyFile(string sourcePath, string targetPath,
    Action<string, string, long>? onBytesWritten = null)
{
    const int bufferSize = 81920; // 80 Ko par chunk
    using FileStream source = new(sourcePath, FileMode.Open, FileAccess.Read,
        FileShare.Read, bufferSize, FileOptions.SequentialScan);
    using FileStream target = new(targetPath, FileMode.Create, FileAccess.Write,
        FileShare.None, bufferSize, FileOptions.None);
    byte[] buffer = new byte[bufferSize];
    int bytesRead;
    while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
    {
        target.Write(buffer, 0, bytesRead);
        onBytesWritten?.Invoke(sourcePath, targetPath, bytesRead);
    }
}
```

- `FileShare.Read` sur la source : d'autres processus peuvent lire le fichier pendant la copie.
- `FileShare.None` sur la cible : empêche les écritures concurrentes vers la même destination.
- `FileOptions.SequentialScan` : indique à l'OS de pré-charger agressivement les données en RAM puisqu'on lit linéairement — améliore le débit.
- Buffer de 80 Ko : assez grand pour minimiser le nombre de syscalls, assez petit pour que `onBytesWritten` soit appelé fréquemment et maintienne des barres de progression fluides.
- `onBytesWritten` est optionnel (`?.Invoke`). Si `null`, la copie se fait sans notification.

---

### `ViewModels/ConfigManager.cs`

Sauvegarde et charge la liste de `BackupJob` dans `backup_jobs.json` via `System.Text.Json` avec `WriteIndented = true`.

```csharp
public void SaveJobs(List<BackupJob> jobs)
{
    var options = new JsonSerializerOptions { WriteIndented = true };
    string json = JsonSerializer.Serialize(jobs, options);
    File.WriteAllText(_configFilePath, json);
    Console.WriteLine($"{_languageManager.GetText("config_jobs_saved")}{_configFilePath}");
}

public List<BackupJob> LoadJobs()
{
    if (!File.Exists(_configFilePath)) return [];
    string json = File.ReadAllText(_configFilePath);
    return JsonSerializer.Deserialize<List<BackupJob>>(json) ?? [];
}
```

- Toutes les erreurs sont catchées et loguées en console — un échec de lecture retourne une liste vide pour que l'app démarre quand même.
- Le `Console.WriteLine` de `SaveJobs` n'a aucun effet visible en mode GUI (la console est cachée). C'est un vestige du mode CLI.

---

### `ViewModels/SettingsManager.cs`

Sauvegarde et charge `AppSettings` dans `settings.json`.

```csharp
private static JsonSerializerOptions SerializerOptions { get; } = new()
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};

public AppSettings Load()
{
    if (!File.Exists(_settingsFilePath)) return new AppSettings();
    string json = File.ReadAllText(_settingsFilePath);
    return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings();
}
```

- `JsonStringEnumConverter` sérialise les enums comme chaînes (`"Json"`, `"Xml"`) plutôt que comme entiers — le fichier JSON est lisible par un humain.
- Tout échec de chargement (fichier corrompu, fichier absent) retourne un `AppSettings` avec les valeurs par défaut. L'utilisateur ne voit jamais d'erreur non gérée au démarrage.

---

### `ViewModels/StateManager.cs`

Plus complexe que les deux autres car il est appelé depuis plusieurs threads concurrents lors d'un "Run All".

**Thread safety :** Toutes les méthodes publiques qui touchent le fichier sont protégées par `private readonly object _stateLock`.

```csharp
public void UpdateJobState(StateEntry updatedEntry)
{
    lock (_stateLock)
    {
        List<StateEntry> allStates = LoadStates();  // lecture fichier complet

        bool found = false;
        for (int i = 0; i < allStates.Count; i++)
        {
            if (allStates[i].Name == updatedEntry.Name)
            {
                allStates[i] = updatedEntry;
                found = true;
                break;
            }
        }

        if (!found) allStates.Add(updatedEntry);

        SaveState(allStates);  // écriture fichier complet
    }
}

public void ClearState()
{
    lock (_stateLock)
    {
        SaveState(new List<StateEntry>());
    }
}
```

- Pourquoi lecture-modification-écriture complète et non append ? Le fichier de state contient **tous** les jobs. Quand le job A termine, le fichier doit encore contenir le state courant des jobs B et C. Il faut donc charger la liste entière, remplacer l'entrée du job A, et réécrire la liste complète. Le lock garantit qu'aucun thread n'entrelace sa lecture et son écriture.
- `SaveState` et `LoadStates` sont `private` : ils ne doivent être appelés qu'à l'intérieur du lock.
- Supporte JSON et XML via `_format`, configurable via `SetFormat(LogFormat)`.

---

## 5. Système de langues

**`ViewModels/LanguageManager.cs`**

Singleton qui charge les clés de traduction depuis des fichiers JSON dans `Resources/Languages/`.

**Pattern singleton avec double vérification :**

```csharp
private static LanguageManager _instance;
private static readonly object _lock = new object();

public static LanguageManager GetInstance()
{
    if (_instance == null)
    {
        lock (_lock)
        {
            if (_instance == null)
                _instance = new LanguageManager();
        }
    }
    return _instance;
}
```

- Le `null` check extérieur évite d'acquérir le lock à chaque appel (cas courant). Le check intérieur dans le lock empêche deux threads ayant passé le check externe de créer chacun une instance.

**Indexeur :**

```csharp
public string this[string key] => GetText(key);
```

Permet la syntaxe `LangMgr["gui_done"]` dans les bindings XAML et le code ViewModel au lieu de `LangMgr.GetText("gui_done")`.

**Notification de changement de langue :**

```csharp
public void SetLanguage(string lang)
{
    if (_currentLanguage != lang)
    {
        _currentLanguage = lang;
        LoadTranslations();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
```

- `SetLanguage` fire `PropertyChanged` avec `"Item"` et `"Item[]"`. Les bindings Avalonia qui utilisent l'indexeur écoutent `"Item[]"` et se réévaluent automatiquement — toute l'interface se relibelle sans aucun appel explicite de refresh.

**`GetAvailableLanguages()` :**

```csharp
public List<string> GetAvailableLanguages()
{
    string dirPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Languages");
    if (Directory.Exists(dirPath))
    {
        foreach (string file in Directory.GetFiles(dirPath, "*.json"))
            languages.Add(Path.GetFileNameWithoutExtension(file));
    }
    // fallback vers chemin relatif si AppContext.BaseDirectory ne fonctionne pas
    // retourne ["en"] si rien trouvé
}
```

- Découverte dynamique des langues disponibles en scannant le dossier `Resources/Languages/`.
- Retourne au moins `["en"]` comme valeur de repli sûre.

**`GetTextForLanguage(lang, key)` :**

Lit une clé depuis un fichier de langue spécifique **sans changer** la langue courante. Utilisé par `SettingsViewModel.LanguageSelectorLabel` pour afficher chaque langue dans sa propre langue (ex: "English / Français / Русский").

**`ViewModelBase.cs` :**

```csharp
protected ViewModelBase()
{
    LanguageManager.Instance.PropertyChanged += (s, e) => OnPropertyChanged(string.Empty);
}
```

- Chaque ViewModel de base s'abonne aux changements de langue. `OnPropertyChanged(string.Empty)` notifie Avalonia que **toutes** les propriétés ont potentiellement changé, déclenchant un rebind complet de tous les labels dans la vue liée.

---

## 6. Surveillance des logiciels métier

**`ViewModels/BusinessSoftwareMonitor.cs`**

Surveille si un processus configuré est en cours d'exécution via `Process.GetProcessesByName()`.

**Boucle de surveillance :**

```csharp
public void StartMonitoring(IEnumerable<string> configuredProcessNames)
{
    _cts?.Cancel();
    _cts?.Dispose();
    _cts = new CancellationTokenSource();
    _wasPreviouslyDetected = false;

    Task.Run(() =>
    {
        while (!token.IsCancellationRequested)
        {
            bool isRunningNow = TryFindRunningBusinessSoftware(processList, out string detectedName);

            if (isRunningNow && !_wasPreviouslyDetected)
                OnBusinessSoftwareDetected?.Invoke(detectedName);
            else if (!isRunningNow && _wasPreviouslyDetected)
                OnBusinessSoftwareClosed?.Invoke();

            _wasPreviouslyDetected = isRunningNow;
            token.WaitHandle.WaitOne(1000);  // attente interruptible de 1s
        }
    }, token);
}
```

- `_cts?.Cancel()` au début de `StartMonitoring` arrête proprement une surveillance précédente avant d'en démarrer une nouvelle (utile quand les settings sont sauvegardés).
- `token.WaitHandle.WaitOne(1000)` est préférable à `Thread.Sleep(1000)` : le `WaitHandle` répond immédiatement si le token est annulé, alors que `Thread.Sleep` ignorerait le signal d'arrêt.
- La logique "edge trigger" (`_wasPreviouslyDetected`) : les événements ne sont envoyés qu'au moment de la **transition** (apparition ou disparition du processus), pas à chaque cycle.

**Normalisation du nom de processus :**

```csharp
private static string NormalizeProcessName(string processName)
{
    string name = processName.Trim();
    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        name = Path.GetFileNameWithoutExtension(name);
    return name;
}
```

`Process.GetProcessesByName()` ne veut pas l'extension `.exe`. La normalisation permet à l'utilisateur de saisir indifféremment `"word"` ou `"word.exe"`.

**Intégration dans `MainViewModel` :**

```csharp
_businessSoftwareMonitor.OnBusinessSoftwareDetected += (processName) =>
{
    _businessSoftwareGate.Reset(); // ferme le gate → met en pause tous les jobs
};

_businessSoftwareMonitor.OnBusinessSoftwareClosed += () =>
{
    _businessSoftwareGate.Set(); // ouvre le gate → reprend tous les jobs
};
```

Le gate est un `ManualResetEventSlim` partagé avec `BackupProcessor`. Quand il est fermé (Reset), tous les threads de copie se mettent en pause à la prochaine limite de fichier.

---

## 7. Chiffrement (CryptoSoftService)

**`ViewModels/CryptoSoftService.cs`**

Singleton thread-safe qui encapsule l'appel à `CryptoProcessor.EncryptFileInPlace` depuis la librairie `CryptoSoftLib`.

**Singleton :**

```csharp
private static CryptoSoftService? _instance;
private static readonly object _instanceLock = new();

public static CryptoSoftService Instance
{
    get
    {
        if (_instance is null)
            lock (_instanceLock)
                _instance ??= new CryptoSoftService();
        return _instance;
    }
}
```

**Mutex système nommé :**

```csharp
private const string MutexName = "Global\\EasySave_CryptoSoft_SingleInstance";
private readonly Mutex _globalMutex = new(initiallyOwned: false, name: MutexName);

public long Encrypt(string filePath, string key)
{
    _globalMutex.WaitOne();
    try
    {
        Stopwatch sw = Stopwatch.StartNew();
        int result = CryptoProcessor.EncryptFileInPlace(filePath, key);
        sw.Stop();
        if (result < 0) return result;
        return sw.ElapsedMilliseconds;
    }
    catch { return -1; }
    finally { _globalMutex.ReleaseMutex(); }
}
```

- Le mutex système **nommé** (`Global\...`) est visible entre processus différents sur la même machine. Si deux instances d'EasySave tournent en parallèle, elles se queueuent pour chiffrer plutôt que de corrompre les fichiers.
- `initiallyOwned: false` : le mutex n'est pas acquis à la création — il est acquis à la demande dans `WaitOne()`.
- La valeur de retour est le temps de chiffrement en millisecondes (positif = succès), ou un code d'erreur négatif (retourné directement par `CryptoProcessor`) ou `-1` pour une exception.
- Le catch avale toutes les exceptions : un échec de chiffrement ne doit jamais crasher la sauvegarde.

---

## 8. Pipeline d'exécution des sauvegardes

**`ViewModels/BackupProcessor.cs`**

L'orchestrateur central. Reçoit un `BackupJob`, un `BackupSyncContext` et deux gates, et gère tout le cycle de vie de la sauvegarde.

**Champs injectés :**

```csharp
private readonly StateManager _stateManager;
private readonly BusinessSoftwareMonitor _businessSoftwareMonitor; // ⚠ DEAD FIELD — jamais lu
private readonly CryptoSoftService _cryptoSoftService;
private readonly AppSettings _settings;
private readonly ManualResetEventSlim _businessSoftwareGate;
```

**Événement de progression :**

```csharp
public event Action<string, BackupStatus, int, string, bool, string>? ProgressChanged;
```

Paramètres dans l'ordre : `jobName`, `status`, `progression`, `currentFile`, `blocked`, `errorMessage`. Souscrit par `MainViewModel.OnJobProgressChanged`.

**Méthode `Execute` — cycle de vie complet :**

**Bloc 1 — Sélection de stratégie**

```csharp
IBackupStrategy strategy = job.Type switch
{
    BackupType.Full         => new FullBackupStrategy(syncContext),
    BackupType.Differential => new DifferentialBackupStrategy(syncContext),
    _ => throw new ArgumentException($"Unknown backup type: {job.Type}")
};
```

Chaque appel à `Execute` crée une **nouvelle instance** de stratégie. C'est intentionnel : chaque stratégie détient un `Lock _lock` privé qui sérialise les écritures de log des threads de fichiers concurrents au sein d'un job. Des instances séparées par job signifient que les locks sont indépendants entre jobs.

**Bloc 2 — Énumération des fichiers et calcul de la taille totale**

```csharp
if (File.Exists(job.SourceDir))
    files = new string[] { job.SourceDir };
else
    files = Directory.GetFiles(job.SourceDir ?? string.Empty, "*.*", SearchOption.AllDirectories);

long totalSize = 0;
foreach (string file in files) totalSize += new FileInfo(file).Length;
```

- Détecte si la source est un fichier unique ou un répertoire.
- `totalSize` est le dénominateur pour tous les calculs de pourcentage.
- En cas d'exception (répertoire inaccessible), `files` est mis à `Array.Empty<string>()` et le job se termine immédiatement.

**Bloc 3 — Mise à jour initiale du state**

```csharp
_stateManager.UpdateJobState(new StateEntry
{
    Name = job.Name ?? "Unnamed Job",
    State = BackupStatus.Inactive,
    TotalFilesToCopy = totalFiles,
    TotalFilesSize = totalSize,
    NbFilesLeftToDo = totalFiles,
    SizeRemaining = totalSize,
    Progression = 0,
    LastRun = DateTime.Now
});
RaiseProgress(job.Name, BackupStatus.In_Progress, 0);
```

Le state est écrit en `Inactive` au fichier avant le début réel de la copie (état "prêt à partir"). Puis l'UI est notifiée `In_Progress`.

**Bloc 4 — Callbacks locaux (fonctions locales)**

Deux fonctions locales sont définies dans le scope de `Execute` et passées à `strategy.Backup` :

**`OnBytesWritten`** — appelée après chaque chunk de 80 Ko :

```csharp
void OnBytesWritten(string sourceFile, string destFile, long bytes)
{
    long current = Interlocked.Add(ref bytesCopied, bytes);
    int progression = totalSize > 0 ? (int)(current * 100 / totalSize) : 0;
    if (progression == Volatile.Read(ref lastReportedProgression)) return;
    Volatile.Write(ref lastReportedProgression, progression);
    // mise à jour state + RaiseProgress
}
```

- `Interlocked.Add` : accumulation atomique des octets depuis plusieurs threads de fichiers concurrents.
- Guard `if (progression == lastReportedProgression) return` : évite les mises à jour UI redondantes quand le pourcentage n'a pas changé. `Volatile.Read/Write` assure la cohérence mémoire inter-threads sans lock.

**`OnFileCopied`** — appelée une fois par fichier complété :

```csharp
void OnFileCopied(string sourceFile, string destFile, long fileSize)
{
    int fc = Interlocked.Increment(ref filesCopied);
    long bc = Volatile.Read(ref bytesCopied);
    int progression = totalSize > 0 ? (int)(bc * 100 / totalSize) : 0;
    // mise à jour state + RaiseProgress
}
```

- `Interlocked.Increment` : compteur atomique de fichiers terminés.

**Bloc 5 — Gestion des exceptions**

```csharp
catch (OperationCanceledException)
{
    // Annulation utilisateur → state Inactive, return false
    _stateManager.UpdateJobState(new StateEntry { State = BackupStatus.Inactive, ... });
    RaiseProgress(job.Name, BackupStatus.Inactive, 0);
    return false;
}
catch (Exception ex)
{
    // Erreur fatale → state Error, log de l'erreur, re-throw
    _stateManager.UpdateJobState(new StateEntry { State = BackupStatus.Error, ... });
    logRouter.Save(new LogEntry { Event = $"Error:{ex.GetType().Name}:{ex.Message}", ... });
    RaiseProgress(job.Name, BackupStatus.Error, 0, errorMessage: ex.Message);
    throw;
}
```

- `OperationCanceledException` = annulation propre par l'utilisateur (CancellationToken). Le job revient à `Inactive`.
- Toute autre exception = erreur fatale. Le re-throw est délibéré : le caller (`RunCard`/`RunAll`) a un `catch (Exception) {}` vide pour éviter que le thread non géré termine le processus.

**`RaiseProgress` :**

```csharp
private void RaiseProgress(string? jobName, BackupStatus status, int progression,
    string currentFile = "", string errorMessage = "")
{
    bool blocked = !_businessSoftwareGate.IsSet;
    ProgressChanged?.Invoke(jobName ?? string.Empty, status, progression,
        currentFile, blocked, errorMessage);
}
```

Le champ `blocked` est calculé en lisant `_businessSoftwareGate.IsSet` au moment de l'émission, pas passé en paramètre — garantit que l'état courant du gate est toujours reflété dans l'événement.

---

## 9. Stratégies de sauvegarde

### `Models/Strategies/IBackupStrategy.cs`

Le contrat d'interface :

```csharp
void Backup(
    BackupJob job,
    BackupLogRouter logService,
    AppSettings settings,
    CryptoSoftService cryptoService,
    Action<string, string, long> onFileCopied,
    ManualResetEventSlim businessSoftwareGate,
    ManualResetEventSlim userPauseGate,
    CancellationToken cancellationToken,
    Action<string, string, long>? onBytesWritten = null);
```

- `businessSoftwareGate` et `userPauseGate` sont deux `ManualResetEventSlim` distincts : l'un géré par le monitor automatique, l'autre par l'utilisateur via les boutons Pause/Resume.
- `onBytesWritten` est optionnel : `null` si la progression par chunk n'est pas nécessaire.

---

### `Models/Strategies/BackupStrategyBase.cs`

Classe abstraite contenant toute la logique partagée. Les stratégies concrètes n'héritent que le nécessaire.

**`_lock` — `System.Threading.Lock` :**

```csharp
private readonly Lock _lock = new();
```

`System.Threading.Lock` est un type .NET 9+ optimisé pour l'exclusion mutuelle. Le JIT génère un code plus efficace qu'un lock `object` classique. Ce lock est **par instance de stratégie** → chaque job a son propre lock → les jobs parallèles ne se bloquent jamais mutuellement sur ce lock.

---

**`WaitForGates` — attente des deux gates :**

```csharp
private static void WaitForGates(ManualResetEventSlim businessSoftwareGate,
    ManualResetEventSlim userPauseGate, CancellationToken ct)
{
    while (!businessSoftwareGate.IsSet || !userPauseGate.IsSet)
    {
        ct.ThrowIfCancellationRequested();
        if (!businessSoftwareGate.IsSet) businessSoftwareGate.Wait(ct);
        if (!userPauseGate.IsSet) userPauseGate.Wait(ct);
    }
    ct.ThrowIfCancellationRequested();
}
```

- La boucle while est nécessaire car les deux gates peuvent être fermées simultanément et il faut attendre les deux.
- `ct.ThrowIfCancellationRequested()` avant et après chaque `Wait` : si l'utilisateur stop pendant la pause, le thread se débloque immédiatement.

---

**`CopyGroup` — copie parallèle d'un groupe de fichiers :**

```csharp
protected void CopyGroup(List<FileInfo> group, bool isPriorityGroup, ...)
{
    var threads = new List<Thread>();
    var threadLimiter = new SemaphoreSlim(Environment.ProcessorCount);

    foreach (FileInfo filePath in group)
    {
        cancellationToken.ThrowIfCancellationRequested();
        WaitForGates(businessSoftwareGate, userPauseGate, cancellationToken);
        threadLimiter.Wait(cancellationToken); // attend un slot libre

        FileInfo captured = filePath;
        var thread = new Thread(() => { /* ... */ });
        threads.Add(thread);
        thread.Start();
    }

    foreach (var t in threads) t.Join();
    cancellationToken.ThrowIfCancellationRequested();
}
```

- `threadLimiter = new SemaphoreSlim(Environment.ProcessorCount)` : au plus `N` threads (N = nombre de cœurs logiques) s'exécutent simultanément. Sans cette limite, créer des milliers de threads filerait en mémoire et dégraderait les performances.
- `WaitForGates` est appelé **avant** de créer le thread (dans le thread parent). Les gates sont aussi vérifiées **à l'intérieur** du thread fils (cas où le gate se ferme après la création mais avant le début de la copie).
- `foreach (var t in threads) t.Join()` : attend que **tous** les threads du groupe soient terminés avant de retourner.
- Le re-check du CancellationToken après `Join` : les threads fils avalent les `OperationCanceledException` en interne — le re-check propaget l'annulation au niveau supérieur.

**Intérieur d'un thread de fichier :**

```csharp
var thread = new Thread(() =>
{
    string? currentTargetFile = null;
    bool isLargeFile = false;
    try
    {
        if (cancellationToken.IsCancellationRequested) return; // [1]

        if (!isPriorityGroup)
            _context.WaitForAllPriorityFiles(cancellationToken); // [2]

        string targetPath = captured.FullName.Replace(job.SourceDir!, job.TargetDir!); // [3]

        if (!shouldCopy(captured, targetPath)) return; // [4]

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!); // [5]

        isLargeFile = _context.LargeFileSizeThresholdBytes > 0
                      && captured.Length > _context.LargeFileSizeThresholdBytes;
        if (isLargeFile) _context.AcquireLargeFileSlot(); // [6]
        try
        {
            void OnChunk(string src, string dest, long bytes)
            {
                currentTargetFile = dest;
                cancellationToken.ThrowIfCancellationRequested(); // [7]
                onBytesWritten?.Invoke(src, dest, bytes);
            }
            FileHelper.CopyFile(captured.FullName, targetPath, OnChunk);
        }
        catch (OperationCanceledException)
        {
            if (currentTargetFile != null && File.Exists(currentTargetFile))
                try { File.Delete(currentTargetFile); } catch { } // [8]
            return;
        }
        finally
        {
            if (isLargeFile) _context.ReleaseLargeFileSlot(); // [9]
        }

        long encryptionTime = TryEncrypt(targetPath, captured.Extension, ...); // [10]

        lock (_lock) // [11]
        {
            onFileCopied(captured.FullName, targetPath, captured.Length);
            logService.Save(new LogEntry { ... });
        }
    }
    catch (OperationCanceledException) { /* avalé */ }
    catch (Exception)
    {
        lock (_lock) { logService.Save(new LogEntry { Event = "CopyError", ... }); }
    }
    finally
    {
        if (isPriorityGroup) _context.NotifyPriorityFileDone(); // [12]
        threadLimiter.Release(); // [13]
    }
});
```

Étapes numérotées :

1. **[1] Vérification CancellationToken en entrée** : si l'annulation a eu lieu entre la création du thread et son démarrage, on sort immédiatement.
2. **[2] Barrière de priorité inter-jobs** : les threads non-prioritaires attendent que **tous** les fichiers prioritaires de **tous** les jobs soient terminés. Implémenté via `ManualResetEventSlim` dans `BackupSyncContext`.
3. **[3] Calcul du chemin de destination** : remplacement textuel du préfixe source par le préfixe destination pour reconstruire la structure de dossiers.
4. **[4] Prédicat `shouldCopy`** : pour Full = toujours vrai ; pour Differential = vérifie si le fichier est absent ou plus récent.
5. **[5] Création du répertoire destination** : `CreateDirectory` est idempotent, pas besoin de lock — plusieurs threads peuvent l'appeler en parallèle sur le même chemin sans problème.
6. **[6] Acquisition du slot de gros fichier** : si le fichier dépasse le seuil configuré et si le seuil est > 0, attend le sémaphore `_largeFileSemaphore` (1 slot max).
7. **[7] Vérification de l'annulation par chunk** : dans le callback `OnChunk`, le token est vérifié à chaque chunk de 80 Ko — permettant d'annuler en cours de copie d'un gros fichier sans attendre la fin.
8. **[8] Suppression du fichier partiel** : en cas d'annulation, le fichier destination partiellement écrit est supprimé pour ne pas laisser de données corrompues.
9. **[9] Libération du slot** : dans un `finally` — toujours libéré même si la copie a échoué ou été annulée. Sans ça, le sémaphore serait bloqué à jamais.
10. **[10] Chiffrement optionnel** : si l'extension du fichier est dans `EncryptedExtensions`, appel de `CryptoSoftService.Encrypt`. Retourne le temps de chiffrement en ms, ou 0 si non chiffré.
11. **[11] Section critique log + callback** : `onFileCopied` (qui met à jour le compteur et le state) et `logService.Save` (qui écrit dans le fichier) sont protégés par `_lock`. Sans ce lock, deux threads de fichier pourraient écrire simultanément dans le fichier de log et corrompre son contenu.
12. **[12] Notification priorité** : dans `finally` — même si la copie a levé une exception, le thread prioritaire notifie le contexte. Sans ça, les threads non-prioritaires attendraient indéfiniment dans `WaitForAllPriorityFiles`.
13. **[13] Libération du slot threadLimiter** : dans `finally` — libère une place pour qu'un nouveau thread puisse démarrer.

---

**`CopySingleFile` — quand la source est un fichier unique :**

```csharp
protected static void CopySingleFile(string sourcePath, string targetDir, ...,
    Func<FileInfo, string, bool>? shouldCopy = null)
{
    WaitForGates(businessSoftwareGate, userPauseGate, cancellationToken);
    cancellationToken.ThrowIfCancellationRequested();

    FileInfo fileInfo = new(sourcePath);
    Directory.CreateDirectory(targetDir);
    string targetPath = Path.Combine(targetDir, fileInfo.Name);

    if (shouldCopy != null && !shouldCopy(fileInfo, targetPath)) return;

    // copie avec OnChunk + suppression si annulé
    // puis TryEncrypt, onFileCopied, logService.Save
}
```

- Chemin simplifié : pas de thread supplémentaire, tout se passe sur le thread appelant.
- `shouldCopy` est optionnel : Full passe `null` (toujours copier), Differential passe un prédicat.
- En cas d'annulation, le fichier partiel est supprimé et l'`OperationCanceledException` est **re-throwé** (contrairement à `CopyGroup` où elle est avalée par le thread interne).

---

**`TryEncrypt` :**

```csharp
protected static long TryEncrypt(string targetPath, string extension,
    CryptoSoftService cryptoService, AppSettings settings)
{
    foreach (string ext in settings.EncryptedExtensions)
    {
        if (ext.Equals(extension, StringComparison.OrdinalIgnoreCase))
            return cryptoService.Encrypt(targetPath, settings.EncryptionKey);
    }
    return 0;
}
```

Parcourt la liste des extensions chiffrées. Si l'extension correspond (insensible à la casse), chiffre le fichier et retourne le temps en ms. Retourne 0 si pas de chiffrement.

---

### `Models/Strategies/FullBackupStrategy.cs`

```csharp
public override void Backup(...)
{
    // Validation : chemins non vides, source existante
    
    if (File.Exists(job.SourceDir))
    {
        CopySingleFile(job.SourceDir, job.TargetDir, ..., shouldCopy: null);
        return;
    }

    FileInfo[] files = sourceInfo.GetFiles("*.*", SearchOption.AllDirectories);
    var prioritized = files.Where(f =>
        settings.PrioritizedExtensions.Contains(f.Extension.ToLowerInvariant())).ToList();
    var regular = files.Except(prioritized).ToList();

    _context.RegisterPriorityFiles(prioritized.Count);

    CopyGroup(prioritized, isPriorityGroup: true,  ..., shouldCopy: (f, t) => true);
    CopyGroup(regular,     isPriorityGroup: false, ..., shouldCopy: (f, t) => true);
}
```

- `shouldCopy: (f, t) => true` : tous les fichiers sont toujours copiés.
- `_context.RegisterPriorityFiles(prioritized.Count)` : ferme la barrière inter-jobs si des fichiers prioritaires existent.
- Les deux `CopyGroup` sont **séquentiels** l'un après l'autre (le deuxième n'est appelé qu'après le retour du premier) mais les threads à l'intérieur de chaque groupe sont parallèles.

---

### `Models/Strategies/DifferentialBackupStrategy.cs`

```csharp
static bool ShouldCopy(FileInfo f, string t) =>
    !File.Exists(t) || f.LastWriteTime > File.GetLastWriteTime(t);
```

- `!File.Exists(t)` : copie si le fichier n'existe pas à destination.
- `f.LastWriteTime > File.GetLastWriteTime(t)` : copie si la source est plus récente.
- Même structure que Full, mais `shouldCopy: ShouldCopy` est passé aux deux `CopyGroup` et à `CopySingleFile`.
- Pour le cas fichier unique, une fonction locale séparée `IsNewer` est définie (identique à `ShouldCopy` mais nommée différemment pour la clarté).

---

## 10. Synchronisation inter-jobs (BackupSyncContext)

**`Models/BackupSyncContext.cs`**

Créé une fois par exécution (`RunCard` ou `RunAll`) et partagé par tous les threads de jobs de ce lot. Impose deux contraintes cross-job. Implémente `IDisposable`.

```csharp
public class BackupSyncContext : IDisposable
{
    private int _priorityFilesRemaining = 0;
    private readonly ManualResetEventSlim _allPriorityDone = new(initialState: true);
    private readonly SemaphoreSlim _largeFileSemaphore = new(1, 1);

    public long LargeFileSizeThresholdBytes { get; }

    public BackupSyncContext(int largeFileSizeThresholdKb)
    {
        LargeFileSizeThresholdBytes = (long)largeFileSizeThresholdKb * 1024;
    }
    ...
}
```

**Contrainte 1 : Barrière de priorité**

```csharp
public void RegisterPriorityFiles(int count)
{
    if (count <= 0) return;          // pas de fichiers prioritaires → le gate reste ouvert
    _allPriorityDone.Reset();        // ferme le gate
    Interlocked.Add(ref _priorityFilesRemaining, count);
}

public void NotifyPriorityFileDone()
{
    if (Interlocked.Decrement(ref _priorityFilesRemaining) == 0)
        _allPriorityDone.Set();      // ouvre le gate quand le dernier fichier prioritaire termine
}

public void WaitForAllPriorityFiles(CancellationToken ct = default)
{
    if (ct == default)
        _allPriorityDone.Wait();
    else
        _allPriorityDone.Wait(ct);
}
```

- Initialisé à `true` (ouvert). Quand des fichiers prioritaires sont enregistrés : Reset → fermé, compteur incrémenté.
- Quand un thread prioritaire se termine : compteur décrémenté. Quand il atteint 0 : Set → ouvert, tous les threads non-prioritaires en attente se débloquent.
- `WaitForAllPriorityFiles(ct)` : la variante avec CancellationToken permet de débloquer en cas d'annulation utilisateur.

**Pourquoi `ManualResetEventSlim` et pas autre chose :**

| Primitive | Comportement | Problème |
|---|---|---|
| `AutoResetEvent` | Libère **un** waiter, puis se referme | Avec 50 threads non-prioritaires en attente, chacun nécessiterait son propre signal. |
| `Monitor.PulseAll` | Broadcast, mais requiert un lock | Un PulseAll manqué (s'il fire avant un Wait) bloque le thread définitivement. |
| `ManualResetEventSlim` | Reste ouvert une fois Set — tous les waiters se débloquent | Sémantique "barrière" exacte. La variante Slim spin-wait brièvement avant un wait kernel, évitant le context-switch pour les attentes courtes. |

**Contrainte 2 : Sémaphore de gros fichier**

```csharp
private readonly SemaphoreSlim _largeFileSemaphore = new(1, 1);

public void AcquireLargeFileSlot() => _largeFileSemaphore.Wait();
public void ReleaseLargeFileSlot() => _largeFileSemaphore.Release();
```

- Au plus un transfert de gros fichier à la fois, tous jobs confondus.
- `SemaphoreSlim` plutôt que `Mutex` : un `Mutex` exige que le thread qui a acquis soit celui qui libère. Un `SemaphoreSlim` n'a pas d'affinité de thread — n'importe quel thread peut Release. Aussi, `SemaphoreSlim` est un objet managé sans handle kernel tant qu'aucun thread ne bloque, ce qui rend les acquisitions non-contendues plus rapides. Passer à `new(2, 2)` permettrait deux transferts simultanés sans aucun autre changement.

**Dispose :**

```csharp
public void Dispose()
{
    _allPriorityDone.Dispose();
    _largeFileSemaphore.Dispose();
}
```

Appelé dans le `finally` de `RunCard` et après le `Join` de `RunAll`.

---

## 11. Routage des logs et logging centralisé

### `Services/BackupLogRouter.cs`

Routage des `LogEntry` selon `AppSettings.LogMode` :

```csharp
public sealed class BackupLogRouter
{
    private readonly AppSettings _settings;
    private LogService? _localLogService;

    public BackupLogRouter(AppSettings settings) => _settings = settings;

    public void Save(LogEntry entry)
    {
        bool local = _settings.LogMode is LogMode.Local or LogMode.Both;
        bool central = _settings.LogMode is LogMode.Centralized or LogMode.Both;

        if (local)
        {
            _localLogService ??= new LogService(_settings.LogFormat, LogMode.Local, string.Empty);
            _localLogService.Save(entry);
        }

        if (central)
            CentralLogSocketClient.TrySendFireAndForget(_settings, entry);
    }
}
```

- `_localLogService` est initialisé en **lazy** (`??=`) au premier Save : évite de créer un `LogService` si le mode est Centralized uniquement.
- `LogService` vient de la DLL externe `EasyLog.dll` — gère les fichiers de log journaliers (`YYYY-MM-DD.json` ou `.xml`).
- Une instance de `BackupLogRouter` est créée par appel à `Execute` (dans `BackupProcessor`) — elle est donc liée aux settings de ce run.

---

### `Services/LogSocketEndpoint.cs`

Parse les endpoints TCP en acceptant plusieurs formats :

```csharp
public static bool TryParse(string? raw, out string host, out int port)
{
    host = "127.0.0.1";
    port = 5132;

    if (string.IsNullOrWhiteSpace(raw)) return true; // valeurs par défaut

    // Format URL http(s)://host:port (legacy)
    if (Uri.TryCreate(s, UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
    {
        host = uri.Host;
        port = uri.IsDefaultPort ? 5132 : uri.Port;
        return true;
    }

    // Format host:port (standard)
    int colon = s.LastIndexOf(':');
    if (colon > 0 && int.TryParse(s.AsSpan(colon + 1), out int p) && p > 0 && p <= 65535)
    {
        host = s[..colon].Trim();
        port = p;
        return true;
    }

    // Fallback : juste le host, port par défaut
    host = s;
    port = 5132;
    return host.Length > 0;
}
```

- `LastIndexOf(':')` : gère les adresses IPv6 du format `[::1]:5132` (le dernier `:` sépare port).
- Retourne `true` même si `raw` est vide/null (avec les valeurs par défaut) — l'appelant ne loggue pas vers le serveur central dans ce cas.
- Le format "legacy" URL (`http://...`) est conservé pour la compatibilité avec d'anciens fichiers de settings.

---

### `Services/CentralLogSocketClient.cs`

Client TCP fire-and-forget pour l'envoi des logs vers le serveur central :

```csharp
public static void TrySendFireAndForget(AppSettings settings, LogEntry entry)
{
    if (!LogSocketEndpoint.TryParse(settings.DockerLogServerUrl, out string host, out int port))
        return;

    string format = settings.LogFormat == LogFormat.Xml ? "xml" : "json";
    _ = Task.Run(() => TrySendOnceAsync(host, port, entry, format)); // fire-and-forget
}

private static async Task TrySendOnceAsync(string host, int port, LogEntry entry, string format)
{
    try
    {
        // Sérialisation JSON + ajout de métadonnées
        JsonObject payload = JsonNode.Parse(JsonSerializer.Serialize(entry, JsonOptions))!.AsObject();
        payload["machineName"] = Environment.MachineName;
        payload["userName"] = Environment.UserName;
        payload["logFormat"] = format;

        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);

        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);

        await using NetworkStream stream = client.GetStream();
        byte[] lenBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lenBytes, (uint)body.Length);
        await stream.WriteAsync(lenBytes.AsMemory(0, 4), cts.Token);
        await stream.WriteAsync(body, cts.Token);
        await stream.FlushAsync(cts.Token);
    }
    catch
    {
        // Le logging central ne doit jamais casser les sauvegardes.
    }
}
```

- `_ = Task.Run(...)` : le résultat de la Task est ignoré délibérément — fire-and-forget. Le thread de copie ne se bloque pas pour attendre la réponse réseau.
- `JsonOptions` avec `CamelCase` : les clés JSON sont en camelCase pour correspondre aux conventions du serveur.
- Les métadonnées ajoutées : `machineName`, `userName` (pour identifier l'origine), `logFormat` (pour que le serveur sache dans quel format écrire).
- **Protocole binaire** : 4 octets big-endian de longueur + body JSON. Le serveur lit d'abord la longueur, puis exactement ce nombre d'octets — évite les problèmes de fragmentation TCP.
- Timeout de 8 secondes : si le serveur est inaccessible, la Task se termine proprement après 8s sans bloquer.
- Catch vide : toute exception réseau est silencieusement ignorée. Le logging central ne doit **jamais** interrompre les sauvegardes.

---

## 12. EasySaveLogServer

**`EasySaveLogServer/Program.cs`**

Serveur TCP centralisé indépendant (projet séparé, `net8.0`) qui reçoit et stocke les logs de toutes les instances EasySave sur le réseau.

**Démarrage et configuration :**

```csharp
int port = 5132;
string? portEnv = Environment.GetEnvironmentVariable("EASYSAVE_LOG_PORT");
if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out int parsed) && parsed > 0 && parsed <= 65535)
    port = parsed;

using var listener = new TcpListener(IPAddress.Any, port);
listener.Start();
```

- Le port est configurable via variable d'environnement `EASYSAVE_LOG_PORT` — utile pour la containerisation (Docker).
- `IPAddress.Any` : écoute sur toutes les interfaces réseau.

**Boucle d'acceptation :**

```csharp
while (!cts.IsCancellationRequested)
{
    TcpClient client = await listener.AcceptTcpClientAsync(cts.Token);
    _ = Task.Run(() => HandleClientAsync(client, logsFolder, fileLock), CancellationToken.None);
}
```

- Chaque connexion client est gérée dans une Task séparée — le serveur peut accepter de nouveaux clients pendant le traitement des précédents.
- `CancellationToken.None` pour `HandleClientAsync` : on veut finir de traiter les clients déjà connectés même si le serveur arrête d'accepter.
- Ctrl+C est intercepté et déclenche `cts.Cancel()` pour un arrêt propre.

**Traitement d'un client (`HandleClientAsync`) :**

```csharp
int length = await ReadInt32BigEndianAsync(stream);        // [1] lit 4 octets de longueur
if (length <= 0 || length > 10_000_000) return;           // [2] garde-fou (10 Mo max)

byte[] body = new byte[length];
await ReadExactlyAsync(stream, body, length);               // [3] lit exactement N octets

JsonNode? parsed = JsonNode.Parse(jsonText);               // [4] parse le JSON

entryObj["SenderIP"] = senderIp;                           // [5] ajoute métadonnées serveur
entryObj["ReceivedAt"] = DateTime.UtcNow.ToString("...");

bool useXml = entryObj["logFormat"]?.GetValue<string>()    // [6] détermine le format
              ?.Equals("xml", ...) == true;
entryObj.Remove("logFormat");                              // [7] retire le champ technique

lock (fileLock)                                            // [8] section critique fichier
{
    // lecture du fichier existant + append + réécriture
}
```

1. **Lecture de la longueur** : 4 octets big-endian → int. `ReadInt32BigEndian` vs `ReadUInt32BigEndian` : le client envoie `WriteUInt32BigEndian`, le serveur lit avec `ReadInt32BigEndian`. Pour des payloads < 2 Go, les valeurs sont identiques (bit de signe non utilisé).
2. **Garde-fou** : refuse les messages de taille invalide ou > 10 Mo pour éviter les attaques par épuisement mémoire.
3. **`ReadExactlyAsync`** : TCP peut fragmenter les données. Cette méthode boucle jusqu'à avoir lu exactement `length` octets.
4. **Parse JSON** : transforme le body en `JsonObject` manipulable.
5. **Ajout de métadonnées serveur** : l'IP de l'expéditeur et l'horodatage de réception (UTC) sont ajoutés par le serveur — le client ne peut pas falsifier ces valeurs.
6. **Détermination du format** : le champ `logFormat` envoyé par le client indique si le serveur doit écrire en JSON ou XML.
7. **Suppression du champ technique** : `logFormat` est un méta-champ de protocole, pas une donnée de log. Il est retiré avant l'écriture.
8. **Section critique** : `lock (fileLock)` protège la lecture-modification-écriture du fichier de log partagé entre tous les handlers de clients concurrents.

**`WriteLogsAsXml`** : convertit un `JsonArray` en XML structuré via `XmlWriter`. `XmlConvert.EncodeName` encode les noms de propriétés pour les rendre valides en tant qu'éléments XML.

---

## 13. Couche ViewModels

### `ViewModels/ViewModelBase.cs`

Classe de base de tous les ViewModels. Implémente `INotifyPropertyChanged`.

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public LanguageManager LangMgr => LanguageManager.Instance;

    protected ViewModelBase()
    {
        LanguageManager.Instance.PropertyChanged += (s, e) => OnPropertyChanged(string.Empty);
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
```

- `LangMgr` comme propriété publique : tous les ViewModels héritants accèdent aux traductions via `LangMgr["clé"]` sans passer par un singleton explicite.
- `SetField<T>` : guard — ne fire `PropertyChanged` que si la valeur change réellement. Évite les redraws UI inutiles. `[CallerMemberName]` est un attribut compile-time : le compilateur remplace automatiquement le paramètre par le nom de la propriété appelante.
- `OnPropertyChanged(string.Empty)` sur changement de langue : notifie Avalonia que **toutes** les propriétés ont potentiellement changé → rebind complet.

---

### `ViewModels/Commands.cs`

Implémentation de `ICommand` :

```csharp
public class Command : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    // Constructeur sans paramètre (plus courant)
    public Command(Action execute, Func<bool>? canExecute = null)
    {
        _execute = _ => execute();
        _canExecute = canExecute == null ? null : _ => canExecute();
    }

    // Constructeur avec paramètre (pour les commandes liées à un élément de liste)
    public Command(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
```

- `RaiseCanExecuteChanged` : force Avalonia à réévaluer `CanExecute` → active/désactive les boutons liés.
- Si `_canExecute == null`, `CanExecute` retourne toujours `true` (commande toujours disponible).

---

### `ViewModels/BackupJobViewModel.cs`

Wrapper observable autour d'un `BackupJob`. C'est la "carte" affichée dans l'UI.

**Propriétés et notifications en cascade :**

```csharp
public BackupStatus Status
{
    get => _status;
    set
    {
        SetField(ref _status, value);
        OnPropertyChanged(nameof(StatusText));   // recalcule le texte
        OnPropertyChanged(nameof(StatusColor));  // recalcule la couleur
        OnPropertyChanged(nameof(HasBeenRun));
        OnPropertyChanged(nameof(IsInProgress));
        OnPropertyChanged(nameof(HasError));
    }
}

public bool IsRunning
{
    get => _isRunning;
    set
    {
        SetField(ref _isRunning, value);
        RunCommand.RaiseCanExecuteChanged();    // active/désactive le bouton Run
        DeleteCommand.RaiseCanExecuteChanged();
        PauseCommand.RaiseCanExecuteChanged();
        ResumeCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsNotRunning));
        OnPropertyChanged(nameof(IsRunningAndPaused));
        OnPropertyChanged(nameof(IsRunningAndNotPaused));
    }
}
```

Chaque setter notifie toutes les propriétés calculées qui en dépendent, évitant d'écrire plusieurs `OnPropertyChanged` à chaque mise à jour manuelle.

**Propriétés calculées :**

```csharp
public bool IsNotRunning => !IsRunning;
public bool IsRunningAndPaused => IsRunning && IsPaused;
public bool IsRunningAndNotPaused => IsRunning && !IsPaused;
public bool HasBeenRun => Status != BackupStatus.Inactive;
public bool IsInProgress => Status == BackupStatus.In_Progress;
public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFile);
public bool HasError => Status == BackupStatus.Error && !string.IsNullOrEmpty(_errorMessage);
```

Ces propriétés sont directement liées à la visibilité de contrôles UI (ex: le bouton Pause n'est visible que si `IsRunningAndNotPaused` est `true`).

**Couleurs et texte de statut :**

```csharp
public string StatusColor => Status switch
{
    BackupStatus.In_Progress => "#4A90D9",  // bleu
    BackupStatus.Ended       => "#4CAF50",  // vert
    BackupStatus.Error       => "#F44336",  // rouge
    BackupStatus.Inactive when BlockedByBusinessSoftware => "#FF9800",  // orange
    _                        => "#9E9E9E"   // gris
};

public string StatusText => Status switch
{
    BackupStatus.In_Progress => Progression + "%",
    BackupStatus.Ended       => LangMgr["gui_done"],
    BackupStatus.Error       => LangMgr["gui_error"],
    BackupStatus.Inactive when BlockedByBusinessSoftware => LangMgr["gui_blocked"],
    _                        => LangMgr["gui_idle"]
};
```

**Mécanismes de pause et stop :**

```csharp
private ManualResetEventSlim _userPauseGate = new ManualResetEventSlim(true);
private CancellationTokenSource _cts = new CancellationTokenSource();

PauseCommand = new Command(
    () => { _userPauseGate.Reset(); IsPaused = true; },
    () => IsRunning && !IsPaused);

ResumeCommand = new Command(
    () => { _userPauseGate.Set(); IsPaused = false; },
    () => IsRunning && IsPaused);

StopCommand = new Command(
    () => _cts.Cancel(),
    () => IsRunning);
```

- `_userPauseGate.Reset()` ferme le gate → les threads de copie s'arrêtent à la prochaine limite de fichier.
- `_cts.Cancel()` signale le `CancellationToken` → les threads de copie se terminent au prochain chunk.

**`ResetForNewRun()` :**

```csharp
public void ResetForNewRun()
{
    _cts.Dispose();
    _cts = new CancellationTokenSource();  // nouveau token propre
    _userPauseGate.Set();                  // s'assure que le gate est ouvert
    IsPaused = false;
}
```

Appelé avant chaque run pour repartir d'un état propre. Un `CancellationTokenSource` annulé ne peut pas être réarmé — il faut en créer un nouveau.

**`ApplyProgress()` — appelé depuis le thread UI :**

```csharp
public void ApplyProgress(string jobName, BackupStatus status, int progression,
    string currentFile, bool blocked, string errorMessage = "")
{
    Status = status;
    Progression = Math.Max(0, progression);
    BlockedByBusinessSoftware = blocked;
    if (!string.IsNullOrEmpty(currentFile)) CurrentFile = currentFile;
    ErrorMessage = status == BackupStatus.Error ? errorMessage : string.Empty;
}
```

- `ErrorMessage` est effacé quand le statut n'est pas `Error` : évite qu'une ancienne erreur persiste après un re-run réussi.
- `Math.Max(0, progression)` : garde-fou contre les progressions négatives.

---

### `ViewModels/MainViewModel.cs`

ViewModel principal de l'application.

**Initialisation :**

```csharp
public MainViewModel()
{
    _settingsManager = new SettingsManager();
    _settings = _settingsManager.Load();

    LanguageManager.Instance.SetLanguage(_settings.Language);

    StateManager stateManager = new StateManager();
    stateManager.SetFormat(_settings.StateFormat);

    _businessSoftwareMonitor = new BusinessSoftwareMonitor();
    _businessSoftwareMonitor.OnBusinessSoftwareDetected += _ => _businessSoftwareGate.Reset();
    _businessSoftwareMonitor.OnBusinessSoftwareClosed  += () => _businessSoftwareGate.Set();
    _businessSoftwareMonitor.StartMonitoring(_settings.BusinessSoftwareProcesses);

    _backupProcessor = new BackupProcessor(stateManager, _businessSoftwareMonitor,
        CryptoSoftService.Instance, _settings, _businessSoftwareGate);

    _configManager = new ConfigManager();
    _jobs = _configManager.LoadJobs();

    _backupProcessor.ProgressChanged += OnJobProgressChanged;

    FillJobCards();

    RunAllCommand = new Command(RunAll, () => !_isRunningAll);
    AddJobCommand = new Command(ShowAddJobDialog);
    OpenSettingsCommand = new Command(ShowSettingsDialog);
}
```

Ordre important : la langue est chargée avant tout autre ViewModel (les erreurs éventuelles de chargement des jobs seront affichées dans la bonne langue).

**Callbacks pour les fenêtres de dialogue :**

```csharp
public Action<Action<BackupJob?>>? RequestAddJob { get; set; }
public Action<AppSettings, Action<AppSettings?>>? RequestSettings { get; set; }
```

Le ViewModel n'importe aucune classe View. Il expose des callbacks (callback qui reçoit un callback) — la View injecte la logique d'ouverture de fenêtre. C'est le pattern MVVM standard pour les dialogues.

**`RunCard` — job unique en arrière-plan :**

```csharp
private void RunCard(BackupJobViewModel card)
{
    card.ResetForNewRun();
    card.IsRunning = true;
    card.Status = BackupStatus.In_Progress;
    card.Progression = 0;

    int index = _jobs.IndexOf(card.Job);
    var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);

    Thread thread = new Thread(() =>
    {
        try { RunJobByIndex(index, syncContext, card.UserPauseGate, card.CancellationToken); }
        catch (Exception) { }
        finally
        {
            syncContext.Dispose();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                card.IsRunning = false;
                card.IsPaused = false;
            });
        }
    });
    thread.IsBackground = true;
    thread.Start();
}
```

- `thread.IsBackground = true` : ce thread ne bloque pas la fermeture de l'application si une copie est en cours quand l'utilisateur ferme la fenêtre.
- Le `catch (Exception) {}` vide est intentionnel : `BackupProcessor.Execute` re-throw les exceptions fatales ; sans ce catch, le thread non géré terminerait le processus.
- `Dispatcher.UIThread.InvokeAsync` pour mettre à jour `IsRunning` : cette propriété est liée à l'UI, sa modification doit se faire sur le thread UI.

**`RunAll` — tous les jobs en parallèle :**

```csharp
private void RunAll()
{
    _isRunningAll = true;
    RunAllCommand.RaiseCanExecuteChanged(); // désactive le bouton "Run All"

    var cards = Jobs.ToList();
    foreach (var card in cards) { card.ResetForNewRun(); card.IsRunning = true; ... }

    Thread coordinator = new Thread(() =>
    {
        var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
        var jobThreads = new List<Thread>();

        foreach (var card in cards)
        {
            BackupJobViewModel captured = card; // capture locale
            Thread jobThread = new Thread(() =>
            {
                try { RunJobByIndex(index, syncContext, captured.UserPauseGate, captured.CancellationToken); }
                catch (Exception) { }
                finally
                {
                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        captured.IsRunning = false;
                        captured.IsPaused = false;
                    });
                }
            });
            jobThread.IsBackground = true;
            jobThreads.Add(jobThread);
        }

        foreach (var t in jobThreads) t.Start();
        foreach (var t in jobThreads) t.Join();
        syncContext.Dispose();

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _isRunningAll = false;
            RunAllCommand.RaiseCanExecuteChanged(); // réactive le bouton
        });
    });
    coordinator.IsBackground = true;
    coordinator.Start();
}
```

- Un `BackupSyncContext` unique est partagé entre tous les threads de jobs — c'est ce qui permet la barrière de priorité et le sémaphore de gros fichier cross-job.
- `BackupJobViewModel captured = card` dans la boucle : copie locale pour que la closure ne capture pas la variable de boucle qui change à chaque itération.

**`OnJobProgressChanged` — pont thread de copie → thread UI :**

```csharp
private void OnJobProgressChanged(string jobName, BackupStatus status, int progression,
    string currentFile, bool blocked, string errorMessage)
{
    Dispatcher.UIThread.InvokeAsync(() =>
    {
        UpdateCardProgress(jobName, status, progression, currentFile, blocked, errorMessage);
    });
}
```

- `InvokeAsync` (non bloquant) : le thread de copie met l'action en queue sur le thread UI et continue immédiatement. Sans ça, avec des centaines de chunks par seconde depuis plusieurs jobs parallèles, chaque thread de copie serait bloqué le temps que l'UI traite la mise à jour.

**`RunJob(string input)` — exécution CLI :**

```csharp
public bool RunJob(string input)
{
    if (_jobs.Count == 0) return false;

    List<int> indicesToRun = ParseIndices(input, _jobs.Count);

    using var syncContext = new BackupSyncContext(_settings.LargeFileSizeThresholdKb);
    bool allSucceeded = true;
    foreach (int index in indicesToRun)
    {
        using var pauseGate = new ManualResetEventSlim(true);
        bool succeeded = _backupProcessor.Execute(_jobs[index], syncContext, pauseGate, CancellationToken.None);
        allSucceeded &= succeeded;
        if (!succeeded) break;
    }
    return allSucceeded;
}
```

- Exécution **séquentielle** (foreach, pas de threads) : arrêt au premier échec. Approprié pour le mode CLI scripté.
- `CancellationToken.None` : pas d'annulation en CLI.

**`ParseIndices` — parsing des indices :**

```csharp
private static List<int> ParseIndices(string input, int maxCount)
{
    if (input.Contains('-'))
    {
        // "1-3" → [0, 1, 2] (0-based)
        string[] parts = input.Split('-');
        if (int.TryParse(parts[0], out int start) && int.TryParse(parts[1], out int end))
            for (int i = start; i <= end; i++)
                if (i > 0 && i <= maxCount) indices.Add(i - 1);
    }
    else if (input.Contains(';'))
    {
        // "1;4;5" → [0, 3, 4]
        foreach (string part in input.Split(';'))
            if (int.TryParse(part, out int id) && id > 0 && id <= maxCount)
                indices.Add(id - 1);
    }
    else
    {
        // "2" → [1]
        if (int.TryParse(input, out int id) && id > 0 && id <= maxCount)
            indices.Add(id - 1);
    }
    indices.Sort();
    return indices;
}
```

Les indices affichés sont 1-basés (comme vu par l'utilisateur), convertis en 0-basés en interne.

**`ToUniqueList` — déduplication :**

```csharp
private static List<string> ToUniqueList(IEnumerable<string> values, bool lowercase = false)
{
    var result = new List<string>();
    foreach (var value in values)
    {
        if (string.IsNullOrWhiteSpace(value)) continue;
        string clean = lowercase ? value.Trim().ToLowerInvariant() : value.Trim();
        if (!result.Contains(clean, StringComparer.OrdinalIgnoreCase))
            result.Add(clean);
    }
    return result;
}
```

`lowercase: true` pour les extensions (`.TXT` et `.txt` sont le même format). `lowercase: false` pour les noms de processus (la normalisation est faite ailleurs dans `BusinessSoftwareMonitor`).

**`ApplySettings` :**

```csharp
public void ApplySettings(AppSettings updated)
{
    _settings.LogFormat = updated.LogFormat;
    // ... copie champ par champ (pas de remplacement de référence)
    LanguageManager.Instance.SetLanguage(updated.Language);
    _settings.Language = updated.Language;
    SaveSettings();
    _businessSoftwareMonitor.StopMonitoring();
    _businessSoftwareMonitor.StartMonitoring(_settings.BusinessSoftwareProcesses);
}
```

Copie champ par champ plutôt que remplacer l'objet `_settings` : `BackupProcessor` détient une référence à cet objet. Un remplacement de référence ne serait pas vu par `BackupProcessor`.

**`Cleanup` :**

```csharp
public void Cleanup()
{
    _businessSoftwareMonitor.StopMonitoring();
    _businessSoftwareGate.Dispose();
    foreach (BackupJobViewModel card in Jobs) card.Dispose();
    CryptoSoftService.Instance.Dispose();
}
```

Appelé par `MainWindow` à la fermeture. Arrête le monitoring, libère le gate, dispose les cards (qui disposent leurs `CancellationTokenSource` et `ManualResetEventSlim`), et libère le mutex système de `CryptoSoftService`.

---

### `ViewModels/AddJobViewModel.cs`

```csharp
public class AddJobViewModel : ViewModelBase
{
    private readonly Command _createCommand;

    public string Name { get => _name; set { if (SetField(ref _name, value)) _createCommand.RaiseCanExecuteChanged(); } }
    public string SourceDir { get => _sourceDir; set { if (SetField(ref _sourceDir, value)) _createCommand.RaiseCanExecuteChanged(); } }
    public string TargetDir { get => _targetDir; set { if (SetField(ref _targetDir, value)) _createCommand.RaiseCanExecuteChanged(); } }

    private bool CanCreate() =>
        !string.IsNullOrWhiteSpace(Name) &&
        !string.IsNullOrWhiteSpace(SourceDir) &&
        !string.IsNullOrWhiteSpace(TargetDir);

    private void TryCreate()
    {
        if (!CanCreate()) return;
        Result = new BackupJob { Name = Name.Trim(), SourceDir = SourceDir.Trim(), ... };
        JobCreated?.Invoke(Result);
    }

    public event Action<BackupJob>? JobCreated;
    public event Action? Cancelled;
}
```

- Chaque setter de propriété appelle `_createCommand.RaiseCanExecuteChanged()` : le bouton "Create" se ré-évalue immédiatement quand l'utilisateur tape dans un champ.
- `CanCreate()` est appelé deux fois (comme `canExecute` pour la commande, et comme guard dans `TryCreate`) — double vérification défensive.

---

### `ViewModels/SettingsViewModel.cs`

```csharp
public class SettingsViewModel : ViewModelBase
{
    // Champs multilignes pour les listes
    public string BusinessProcesses { get; set; }  // une ligne = un processus
    public string EncryptedExtensions { get; set; }
    public string PrioritizedExtensions { get; set; }

    // LogMode via objet d'affichage
    public List<LogModeDisplayOption> LogModeOptions { get; }
    public LogModeDisplayOption SelectedLogModeOption
    {
        get => LogModeOptions.First(o => o.Mode == LogMode);
        set { if (value != null && value.Mode != LogMode) LogMode = value.Mode; }
    }

    // Label multilingue pour le sélecteur de langue
    public string LanguageSelectorLabel
    {
        get
        {
            // "English / Français / Русский"
            return string.Join(" / ",
                LangMgr.GetAvailableLanguages()
                        .Select(l => LangMgr.GetTextForLanguage(l, "gui_language_name")));
        }
    }
}
```

- `ParseLines(string multiline)` : split sur `\r`, `\n`, trim, déduplication insensible à la casse.
- `LanguageSelectorLabel` : chaque langue est affichée dans sa propre langue (utilise `GetTextForLanguage` sans changer la langue courante).
- `LogModeDisplayOption` : petit objet `sealed` qui associe un `LogMode` enum à un label traduit, permettant à un ComboBox de lier l'enum sans conversion manuelle.

---

### `ViewModels/LogModeDisplayOption.cs`

```csharp
public sealed class LogModeDisplayOption
{
    public LogMode Mode { get; }
    public string Label { get; }

    public LogModeDisplayOption(LogMode mode, string label)
    {
        Mode = mode;
        Label = label;
    }
}
```

Objet de valeur immuable utilisé uniquement par `SettingsViewModel`. `sealed` car aucune extension n'est prévue.

---

## 14. Couche Views

### `Views/MainWindow.xaml.cs`

```csharp
public MainWindow()
{
    var vm = new MainViewModel();

    vm.RequestAddJob = callback =>
    {
        var dialog = new AddJobWindow();
        dialog.JobCreated  += job => callback(job);
        dialog.Cancelled   += ()  => callback(null);
        dialog.Show(this);
    };

    vm.RequestSettings = (current, callback) =>
    {
        var dialog = new SettingsWindow(current);
        dialog.Saved     += settings => callback(settings);
        dialog.Cancelled += ()       => callback(null);
        dialog.Show(this);
    };

    DataContext = vm;
    InitializeComponent();

    Closing += (_, _) => vm.Cleanup();
}
```

- Le ViewModel n'importe aucune classe View. Il expose `RequestAddJob` et `RequestSettings` comme `Action<...>` que la View remplit avec la logique d'ouverture de fenêtre. Inversion de dépendance MVVM pure.
- `DataContext = vm` avant `InitializeComponent()` pour que les bindings XAML trouvent immédiatement leurs données.
- `Closing += vm.Cleanup()` : garantit que le monitoring est arrêté et les ressources libérées à la fermeture.

---

### `Views/AddJobWindow.xaml.cs` et `Views/SettingsWindow.xaml.cs`

Même pattern pour les deux :

```csharp
public AddJobWindow()
{
    var vm = new AddJobViewModel();
    DataContext = vm;
    InitializeComponent();

    vm.JobCreated += job => { JobCreated?.Invoke(job); Close(); };
    vm.Cancelled  += () => { Cancelled?.Invoke(); Close(); };
}

public event Action<BackupJob>? JobCreated;
public event Action? Cancelled;
```

1. Crée son ViewModel dans le constructeur.
2. Souscrit aux événements du ViewModel.
3. Relaie vers ses propres événements publics, puis ferme la fenêtre.

`MainWindow` souscrit aux événements publics de ces fenêtres via les callbacks `RequestAddJob`/`RequestSettings`.

---

### `Views/ConsoleView.cs`

Interface console alternative avec menu numéroté (1–9). Crée son propre `MainViewModel` et appelle directement ses méthodes.

> ⚠️ **Code mort** : `ConsoleView` n'est **jamais instanciée** par `Program.cs`. La méthode `RunCli` de `Program.cs` appelle directement `model.RunJob(input)` et n'utilise pas `ConsoleView`. Voir section [17. Code mort](#17-code-mort-identifié).

---

## 15. Modèle de threading

L'application utilise quatre catégories de threads :

```
Thread UI (Avalonia dispatcher)
└── [clic bouton] RunAll() ou RunCard()
    └── Thread coordinateur (IsBackground=true)
        └── RunAll() :
            ├── Thread job 1 (IsBackground=true) → BackupProcessor.Execute()
            │   └── CopyGroup()
            │       ├── Thread fichier 1-1 → CopyFile() + Encrypt()
            │       ├── Thread fichier 1-2 → CopyFile() + Encrypt()
            │       └── ... (limité à ProcessorCount threads simultanés)
            ├── Thread job 2 (IsBackground=true) → BackupProcessor.Execute()
            │   └── CopyGroup()
            │       ├── Thread fichier 2-1 → ...
            │       └── ...
            └── ... (tous les jobs en parallèle)
```

### Thread UI

Toutes les liaisons, notifications de changement de propriété et mises à jour de contrôles doivent se passer ici. `Dispatcher.UIThread.InvokeAsync(action)` met du travail en queue depuis n'importe quel thread.

### Thread coordinateur (un par `RunCard` / `RunAll`)

Démarré par `RunCard` ou `RunAll`. Contient le `try/catch/finally` externe qui remet l'UI à jour quand l'exécution se termine. `IsBackground = true` : ne bloque pas la fermeture de l'application.

### Threads de job (un par job dans `RunAll`)

Démarrés à l'intérieur du thread coordinateur. Chacun appelle `RunJobByIndex` → `BackupProcessor.Execute`. Tous sont joinés (`t.Join()`) avant que le coordinateur ne se termine.

### Threads de fichier (un par fichier dans `CopyGroup`)

Démarrés à l'intérieur du thread de job. Chacun copie un fichier. Tous sont joinés (`t.Join()`) avant que `CopyGroup` ne retourne. Limités à `ProcessorCount` simultanés par `SemaphoreSlim(Environment.ProcessorCount)`.

---

## 16. Sécurité des threads — primitives et emplacements

| Emplacement | Primitive | Ce qu'elle protège |
|---|---|---|
| `StateManager._stateLock` | `object` + `lock` | Lecture-modification-écriture sur le fichier state — empêche deux threads de job d'écrire simultanément et de corrompre les données |
| `BackupStrategyBase._lock` | `System.Threading.Lock` | `onFileCopied` et `logService.Save` — empêche les écritures de log concurrentes des threads de fichiers au sein d'un job |
| `BackupSyncContext._priorityFilesRemaining` | `Interlocked.Add` / `Interlocked.Decrement` | Compteur atomique — instruction CPU atomique suffisante, pas besoin de lock |
| `BackupSyncContext._allPriorityDone` | `ManualResetEventSlim` | Barrière broadcast cross-job — libère tous les threads non-prioritaires quand le dernier fichier prioritaire termine |
| `BackupSyncContext._largeFileSemaphore` | `SemaphoreSlim(1,1)` | Limite les transferts de gros fichiers à un seul simultané, tous jobs confondus |
| `BackupProcessor.OnBytesWritten.bytesCopied` | `Interlocked.Add` | Cumul atomique des octets depuis des threads de fichier concurrents |
| `BackupProcessor.OnBytesWritten.lastReportedProgression` | `Volatile.Read` / `Volatile.Write` | Flag de déduplication des mises à jour de progression — Volatile garantit la cohérence mémoire entre CPUs sans lock |
| `LanguageManager._instance` | `object` + double-checked lock | Création du singleton — empêche deux threads de créer chacun une instance au démarrage |
| `CryptoSoftService._instance` | `object` + double-checked lock (`??=`) | Création du singleton de CryptoSoftService |
| `CryptoSoftService._globalMutex` | `Mutex` nommé système | Limite à un seul processus simultané l'accès à `CryptoProcessor` — visible entre plusieurs instances de l'application |
| `EasySaveLogServer.fileLock` | `object` + `lock` | Lecture-modification-écriture du fichier de log centralisé — protège les handlers de clients concurrents |
| `CopyGroup.threadLimiter` | `SemaphoreSlim(ProcessorCount)` | Limite le nombre de threads de fichiers actifs simultanément pour éviter l'explosion du pool de threads |

---

## 17. Code mort identifié

### `Views/ConsoleView.cs` — classe entière inutilisée

`ConsoleView` définit une interface console complète avec menu, mais **n'est jamais instanciée** nulle part dans l'application. `Program.cs` appelle directement `model.RunJob(input)` sans passer par `ConsoleView` :

```csharp
// Program.cs — ce qui existe vraiment
private static void RunCli(string input)
{
    var model = new MainViewModel();
    bool success = model.RunJob(input);
    Environment.Exit(success ? 0 : 1);
}

// ConsoleView — jamais appelée
public class ConsoleView
{
    public void Run(string[] args) { while (true) { ... } } // jamais exécuté
}
```

En conséquence, les méthodes de `MainViewModel` qui ne sont appelées **que** par `ConsoleView` sont également du code mort :

- `MainViewModel.CreateJob(string name, string source, string target, string type)` — seule `ConsoleView` appelle cette surcharge à paramètres string. L'UI utilise `AddJob(BackupJob)`.
- `MainViewModel.RunAllJobs()` — seule `ConsoleView` (case "7") appelle cette méthode. `RunAll()` est la méthode utilisée par l'UI.
- `MainViewModel.GetJobs()` — seule `ConsoleView` appelle cette méthode pour afficher la liste.

---

### `BackupProcessor._businessSoftwareMonitor` — champ injecté mais jamais lu

```csharp
public class BackupProcessor
{
    private readonly BusinessSoftwareMonitor _businessSoftwareMonitor; // ⚠ jamais utilisé

    public BackupProcessor(..., BusinessSoftwareMonitor businessSoftwareMonitor, ...)
    {
        _businessSoftwareMonitor = businessSoftwareMonitor; // stocké
        // mais _businessSoftwareMonitor n'est jamais référencé dans Execute()
    }
}
```

La logique de blocage par logiciel métier est entièrement gérée via `_businessSoftwareGate` (un `ManualResetEventSlim`). `BackupProcessor` n'a pas besoin de connaître le monitor directement. Le champ et le paramètre constructeur peuvent être supprimés.

---

### `AddJobViewModel.ErrorMessage` et `AddJobViewModel.Result` — propriétés jamais lues extérieurement

```csharp
public string? ErrorMessage { get; private set; }  // setter privé jamais appelé
public BackupJob? Result { get; private set; }     // set dans TryCreate, mais jamais lu
```

- `ErrorMessage` : le setter privé n'est jamais appelé dans le code → la propriété est toujours `null`.
- `Result` : est bien assigné dans `TryCreate`, mais le résultat est livré via l'événement `JobCreated` — personne ne lit `Result` depuis l'extérieur.

---

### `MainViewModel.AvailableLanguages` — liste codée en dur, incomplète

```csharp
public List<string> AvailableLanguages { get; } = new() { "en", "fr" }; // manque "ru"
```

`LanguageManager.GetAvailableLanguages()` découvre dynamiquement toutes les langues disponibles (en, fr, ru). Cette propriété de `MainViewModel` est codée en dur avec seulement deux langues. Si elle est liée à un sélecteur de langue dans `MainWindow.xaml`, le Russe n'y apparaîtra pas. `SettingsViewModel` utilise correctement `LangMgr.GetAvailableLanguages()`.

---

### `MainViewModel.UpdateBusinessSoftwareProcesses()`, `UpdateEncryptedExtensions()`, `SetEncryptionKey()` — méthodes orphelines

```csharp
public void UpdateBusinessSoftwareProcesses(IEnumerable<string> processNames) { ... }
public void UpdateEncryptedExtensions(IEnumerable<string> extensions) { ... }
public void SetEncryptionKey(string key) { ... }
```

Ces trois méthodes ne sont appelées ni par le code GUI (qui utilise `ApplySettings(AppSettings)` pour tout sauvegarder d'un coup), ni par `ConsoleView` (qui, elle aussi, utilise `ApplySettings`). Elles sont probablement des vestiges d'une ancienne API.
