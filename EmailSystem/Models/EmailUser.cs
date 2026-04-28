namespace EmailSystem.Models;

public class EmailUser
{
    public string Name     { get; set; } = string.Empty; // ex: arthur.dono.1
    public string FullName { get; set; } = string.Empty; // ex: Arthur Dono
    public string Role     { get; set; } = string.Empty; // ex: dono, admin, membro

    public string Address => $"onemapasofficeal.{Name}@onemapasofficeal.com";

    public static bool IsValidUsername(string name)
    {
        // Deve ser lowercase, apenas letras, números e pontos, sem ponto no início/fim
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.StartsWith('.') || name.EndsWith('.')) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9]+(\.[a-z0-9]+)*$");
    }

    public static string BuildAddress(string name) =>
        $"onemapasofficeal.{name}@onemapasofficeal.com";
}
