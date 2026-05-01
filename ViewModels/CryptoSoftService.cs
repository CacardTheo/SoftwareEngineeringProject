using System.Diagnostics;
using CryptoSoftLib;

namespace EasySaveWpf.ViewModels;

public class CryptoSoftService
{
    public long Encrypt(string filePath, string key)
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
