// CryptoSoft — XOR file encryptor/decryptor
// Usage: CryptoSoft.exe "<filePath>" "<key>"
// Exit codes: 0 = success, -1 = bad args, -2 = file not found, -3 = error

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CryptoSoft.exe <filePath> <key>");
    return -1;
}

string filePath = args[0];
string key = args[1];

if (!File.Exists(filePath))
{
    Console.Error.WriteLine($"File not found: {filePath}");
    return -2;
}

if (string.IsNullOrEmpty(key))
{
    Console.Error.WriteLine("Encryption key cannot be empty.");
    return -1;
}

try
{
    byte[] fileBytes = File.ReadAllBytes(filePath);
    byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(key);

    for (int i = 0; i < fileBytes.Length; i++)
    {
        fileBytes[i] ^= keyBytes[i % keyBytes.Length];
    }

    File.WriteAllBytes(filePath, fileBytes);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Encryption error: {ex.Message}");
    return -3;
}
