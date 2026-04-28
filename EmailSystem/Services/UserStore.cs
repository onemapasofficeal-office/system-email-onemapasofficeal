using System.Text.Json;
using EmailSystem.Models;

namespace EmailSystem.Services;

/// <summary>
/// Gerencia a lista de usuários do domínio onemapasofficeal.com.
/// Persiste em JSON local em AppData.
/// </summary>
public static class UserStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EmailSystem", "users.json");

    public static List<EmailUser> LoadAll()
    {
        if (!File.Exists(StorePath)) return new List<EmailUser>();
        try
        {
            string json = File.ReadAllText(StorePath);
            return JsonSerializer.Deserialize<List<EmailUser>>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void SaveAll(List<EmailUser> users)
    {
        string dir = Path.GetDirectoryName(StorePath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Add(EmailUser user)
    {
        var users = LoadAll();
        if (users.Any(u => u.Name == user.Name))
            throw new InvalidOperationException($"Usuário '{user.Name}' já existe.");
        users.Add(user);
        SaveAll(users);
    }

    public static void Remove(string name)
    {
        var users = LoadAll();
        int removed = users.RemoveAll(u => u.Name == name);
        if (removed == 0) throw new InvalidOperationException($"Usuário '{name}' não encontrado.");
        SaveAll(users);
    }

    public static EmailUser? Find(string name) =>
        LoadAll().FirstOrDefault(u => u.Name == name);
}
