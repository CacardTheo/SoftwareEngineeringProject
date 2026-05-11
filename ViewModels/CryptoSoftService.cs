using System.Diagnostics;
using CryptoSoftLib;

namespace EasySaveWpf.ViewModels;

public class CryptoSoftService
{
    private static CryptoSoftService? _instance;
    private static readonly object _instanceLock = new object();

    public static CryptoSoftService Instance
    {
        get
        {
            if (_instance is null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new CryptoSoftService();
                }
            }
            return _instance;
        }
    }

    // Private constructor: no external instantiation
    private CryptoSoftService() { }

    private readonly object _encryptLock = new object();

    public long Encrypt(string filePath, string key)
    {
        lock (_encryptLock)
        {
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
        }
    }
}
