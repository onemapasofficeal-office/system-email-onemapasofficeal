using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EmailSystem.Models;
#pragma warning disable CA1416

namespace EmailSystem.Services;

/// <summary>
/// Persiste a configuração SMTP de forma segura (DPAPI + AES + XOR).
/// </summary>
public static class ConfigStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EmailSystem", "smtp.dat");

    private static byte[] MachineKey()
    {
        string seed = Environment.MachineName + Environment.UserName + "EmailSys_2026";
        return SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    }

    public static void Save(SmtpConfig config)
    {
        string json  = JsonSerializer.Serialize(config);
        byte[] raw   = Encoding.UTF8.GetBytes(json);
        byte[] xored = Xor(raw, MachineKey());
        byte[] aesed = AesEncrypt(xored);
        byte[] dpapi = ProtectedData.Protect(aesed, MachineKey(), DataProtectionScope.CurrentUser);

        string dir = Path.GetDirectoryName(StorePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(StorePath, dpapi);
    }

    public static SmtpConfig? Load()
    {
        if (!File.Exists(StorePath)) return null;
        try
        {
            byte[] dpapi = File.ReadAllBytes(StorePath);
            byte[] aesed = ProtectedData.Unprotect(dpapi, MachineKey(), DataProtectionScope.CurrentUser);
            byte[] xored = AesDecrypt(aesed);
            byte[] raw   = Xor(xored, MachineKey());
            string json  = Encoding.UTF8.GetString(raw);
            return JsonSerializer.Deserialize<SmtpConfig>(json);
        }
        catch { return null; }
    }

    public static bool Exists() => File.Exists(StorePath);

    private static byte[] AesEncrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256; aes.GenerateKey(); aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        byte[] enc_data = enc.TransformFinalBlock(data, 0, data.Length);
        byte[] result   = new byte[aes.Key.Length + aes.IV.Length + enc_data.Length];
        Buffer.BlockCopy(aes.Key,  0, result, 0,                              aes.Key.Length);
        Buffer.BlockCopy(aes.IV,   0, result, aes.Key.Length,                 aes.IV.Length);
        Buffer.BlockCopy(enc_data, 0, result, aes.Key.Length + aes.IV.Length, enc_data.Length);
        return result;
    }

    private static byte[] AesDecrypt(byte[] data)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256; aes.Key = data[..32]; aes.IV = data[32..48];
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 48, data.Length - 48);
    }

    private static byte[] Xor(byte[] data, byte[] key)
    {
        byte[] r = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) r[i] = (byte)(data[i] ^ key[i % key.Length]);
        return r;
    }
}
