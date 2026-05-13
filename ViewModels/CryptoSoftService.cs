using System.Diagnostics;
using CryptoSoftLib;

namespace EasySaveWpf.ViewModels;

public class CryptoSoftService : IDisposable
{
    private const string MutexName = "Global\\EasySave_CryptoSoft_SingleInstance";

    private static CryptoSoftService? _instance;
    private static readonly object _instanceLock = new();

    // Named system mutex acquired per-operation so concurrent processes queue up
    // rather than blocking app startup.
    private readonly Mutex _globalMutex = new(initiallyOwned: false, name: MutexName);

    private CryptoSoftService() { }

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

    public long Encrypt(string filePath, string key)
    {
        _globalMutex.WaitOne();
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            int result = CryptoProcessor.EncryptFileInPlace(filePath, key);
            sw.Stop();

            if (result < 0)
                return result;

            return sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
        finally
        {
            _globalMutex.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        _globalMutex.Dispose();
    }
}
