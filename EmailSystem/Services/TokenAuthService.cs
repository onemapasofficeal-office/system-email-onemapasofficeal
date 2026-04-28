using System.Security.Cryptography;
using System.Text;

namespace EmailSystem.Services;

/// <summary>
/// Lê e valida o token temporário gerado pelo GitHubTokenGen.
/// O arquivo token.ini.nula.onemapasofficeal.github.roloxstution contém:
///   linha 1: payload criptografado
///   linha 2: ordem das camadas (10 chars)
/// </summary>
public static class TokenAuthService
{
    private const string AppSalt = "GitHubTokenGen_v1_2026_salt!@#$%";

    public static string? ReadAndDecrypt(string tokenFilePath)
    {
        if (!File.Exists(tokenFilePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Arquivo de token não encontrado: {tokenFilePath}");
            Console.ResetColor();
            return null;
        }

        try
        {
            string[] lines = File.ReadAllLines(tokenFilePath);
            if (lines.Length < 2) throw new InvalidOperationException("Formato de token inválido.");

            string payload  = lines[0].Trim();
            string orderStr = lines[1].Trim();

            // Reconstrói o código completo no formato esperado pelo TokenCipher
            string fullCode = $"{orderStr}|{payload}";

            return Decrypt(fullCode);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Erro ao ler token: {ex.Message}");
            Console.ResetColor();
            return null;
        }
    }

    private static string Decrypt(string code)
    {
        int sep = code.IndexOf('|');
        if (sep != 10) throw new InvalidOperationException("Cabeçalho de ordem ausente.");

        string orderStr = code[..10];
        string b64      = code[11..];
        byte[] data     = FromBase64Url(b64);

        char[] order = orderStr.ToCharArray();
        for (int i = order.Length - 1; i >= 0; i--)
            data = ApplyLayer(order[i], data, encrypt: false);

        string text = Encoding.UTF8.GetString(data);
        int s2 = text.IndexOf('|');
        if (s2 < 0) throw new InvalidOperationException("Payload inválido.");

        long   expiresAt = long.Parse(text[..s2]);
        string token     = text[(s2 + 1)..];

        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowUnix > expiresAt)
            throw new InvalidOperationException($"Token expirado há {nowUnix - expiresAt}s.");

        return token;
    }

    // ── Camadas (espelho do TokenCipher) ─────────────────────────────────────

    private static byte[] ApplyLayer(char layer, byte[] data, bool encrypt) => layer switch
    {
        'A' => encrypt ? AesEnc(data, Derive("layer_aes_A")) : AesDec(data, Derive("layer_aes_A")),
        'B' => encrypt ? AesEnc(data, Derive("layer_aes_B")) : AesDec(data, Derive("layer_aes_B")),
        'C' => encrypt ? AesEnc(data, Derive("layer_aes_C")) : AesDec(data, Derive("layer_aes_C")),
        'D' => encrypt ? AesEnc(data, Derive("layer_aes_D")) : AesDec(data, Derive("layer_aes_D")),
        'X' => Xor(data, Derive("layer_xor_X")),
        'Y' => Xor(data, DeriveFromData(data, "layer_xor_Y")),
        'Z' => Xor(data, Derive("layer_xor_Z")),
        'R' => Reverse(data),
        'H' => encrypt ? PrependHmac(data) : StripHmac(data),
        'S' => encrypt ? ShuffleBytes(data) : UnshuffleBytes(data),
        _   => throw new InvalidOperationException($"Camada desconhecida: '{layer}'")
    };

    private static byte[] Derive(string seed)
        => SHA256.HashData(Encoding.UTF8.GetBytes(seed + AppSalt));

    private static byte[] DeriveFromData(byte[] data, string seed)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(seed + AppSalt));
        return hmac.ComputeHash(data)[..32];
    }

    private static byte[] AesEnc(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key; aes.Mode = CipherMode.CBC; aes.GenerateIV();
        using var enc = aes.CreateEncryptor();
        byte[] cipher = enc.TransformFinalBlock(data, 0, data.Length);
        byte[] r = new byte[16 + cipher.Length];
        Buffer.BlockCopy(aes.IV, 0, r, 0, 16);
        Buffer.BlockCopy(cipher, 0, r, 16, cipher.Length);
        return r;
    }

    private static byte[] AesDec(byte[] data, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = data[..16]; aes.Mode = CipherMode.CBC;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 16, data.Length - 16);
    }

    private static byte[] Xor(byte[] data, byte[] key)
    {
        byte[] r = new byte[data.Length];
        for (int i = 0; i < data.Length; i++) r[i] = (byte)(data[i] ^ key[i % key.Length]);
        return r;
    }

    private static byte[] Reverse(byte[] data) { byte[] r = (byte[])data.Clone(); Array.Reverse(r); return r; }

    private static byte[] PrependHmac(byte[] data)
    {
        byte[] h = ComputeHmac(data, Derive("layer_hmac_H"));
        byte[] r = new byte[h.Length + data.Length];
        Buffer.BlockCopy(h, 0, r, 0, h.Length);
        Buffer.BlockCopy(data, 0, r, h.Length, data.Length);
        return r;
    }

    private static byte[] StripHmac(byte[] data)
    {
        if (data.Length < 33) throw new InvalidOperationException("HMAC ausente.");
        byte[] hmac = data[..32]; byte[] payload = data[32..];
        if (!CryptographicOperations.FixedTimeEquals(hmac, ComputeHmac(payload, Derive("layer_hmac_H"))))
            throw new InvalidOperationException("Token adulterado.");
        return payload;
    }

    private static byte[] ShuffleBytes(byte[] data)
    {
        byte[] r = (byte[])data.Clone(); byte[] key = Derive("layer_shuffle_S");
        for (int i = r.Length - 1; i > 0; i--)
        { int j = (int)((key[i % key.Length] * 256 + key[(i + 1) % key.Length]) % (i + 1)); (r[i], r[j]) = (r[j], r[i]); }
        return r;
    }

    private static byte[] UnshuffleBytes(byte[] data)
    {
        byte[] r = (byte[])data.Clone(); byte[] key = Derive("layer_shuffle_S");
        int[] idx = new int[r.Length];
        for (int i = r.Length - 1; i > 0; i--)
            idx[i] = (int)((key[i % key.Length] * 256 + key[(i + 1) % key.Length]) % (i + 1));
        for (int i = 1; i < r.Length; i++) { int j = idx[i]; (r[i], r[j]) = (r[j], r[i]); }
        return r;
    }

    private static byte[] ComputeHmac(byte[] data, byte[] key)
    { using var h = new HMACSHA256(key); return h.ComputeHash(data); }

    private static string ToBase64Url(byte[] data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').Replace("=", "~");

    private static byte[] FromBase64Url(string s)
        => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').Replace("~", "="));
}
