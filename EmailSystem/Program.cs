using EmailSystem.Models;
using EmailSystem.Services;

namespace EmailSystem;

class Program
{
    private static readonly string DefaultTokenFile = Path.Combine(
        AppContext.BaseDirectory,
        "token.ini.nula.onemapasofficeal.github.roloxstution");

    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        PrintBanner();

        // ── 1. Autenticação ───────────────────────────────────────────────────
        string? pat = TryLoadToken(args);
        if (pat == null)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠ Token temporário não encontrado.");
            Console.WriteLine("  Digite o PAT do GitHub manualmente (ou Enter para sair):");
            Console.ResetColor();
            Console.Write("  PAT: ");
            pat = ReadPassword();
            if (string.IsNullOrWhiteSpace(pat)) return;
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("  ✓ Autenticado.");
        Console.ResetColor();
        Console.WriteLine();

        // ── 2. Configuração SMTP ──────────────────────────────────────────────
        SmtpConfig? smtp = ConfigStore.Load();
        if (smtp == null)
        {
            Console.WriteLine("  Nenhuma configuração SMTP encontrada. Vamos configurar agora.");
            smtp = PromptSmtpConfig();
            ConfigStore.Save(smtp);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✓ Configuração salva.");
            Console.ResetColor();
            Console.WriteLine();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  SMTP: {smtp.Host}:{smtp.Port}  ({smtp.Username})");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ── 3. Menu principal ─────────────────────────────────────────────────
        bool running = true;
        while (running)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ┌──────────────────────────────────────────┐");
            Console.WriteLine("  │       SISTEMA DE E-MAIL  •  onemapasofficeal.com      │");
            Console.WriteLine("  ├──────────────────────────────────────────┤");
            Console.WriteLine("  │  1. Enviar e-mail                        │");
            Console.WriteLine("  │  2. Listar usuários                      │");
            Console.WriteLine("  │  3. Adicionar usuário                    │");
            Console.WriteLine("  │  4. Remover usuário                      │");
            Console.WriteLine("  │  5. Reconfigurar SMTP                    │");
            Console.WriteLine("  │  6. Sair                                 │");
            Console.WriteLine("  └──────────────────────────────────────────┘");
            Console.ResetColor();
            Console.Write("  Opção: ");
            string? opt = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (opt)
            {
                case "1": await SendEmailFlow(smtp); break;
                case "2": ListUsers();               break;
                case "3": AddUserFlow();              break;
                case "4": RemoveUserFlow();           break;
                case "5":
                    smtp = PromptSmtpConfig();
                    ConfigStore.Save(smtp);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  ✓ Configuração atualizada.");
                    Console.ResetColor();
                    break;
                case "6": running = false; break;
                default:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  Opção inválida.");
                    Console.ResetColor();
                    break;
            }
            Console.WriteLine();
        }
    }

    // ── Busca token em múltiplos locais ──────────────────────────────────────

    static string? TryLoadToken(string[] args)
    {
        // Locais onde o arquivo de token pode estar
        var candidates = new List<string>();

        if (args.Length > 0)
            candidates.Add(args[0]);

        // Pasta do exe
        candidates.Add(DefaultTokenFile);

        // Pasta do GitHubTokenGen (irmã do EmailSystem)
        string baseDir = AppContext.BaseDirectory;
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "..", "..", "token", "GitHubTokenGen", "bin", "Debug", "net8.0-windows", "token.ini.nula.onemapasofficeal.github.roloxstution"));
        candidates.Add(Path.Combine(baseDir, "..", "..", "..", "..", "token", "GitHubTokenGen", "bin", "Debug", "net8.0-windows", "token.ini.nula.onemapasofficeal.github.roloxstution"));

        // Desktop e pasta atual
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "token.ini.nula.onemapasofficeal.github.roloxstution"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "token.ini.nula.onemapasofficeal.github.roloxstution"));

        foreach (var path in candidates)
        {
            string full = Path.GetFullPath(path);
            if (File.Exists(full))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  Token: {full}");
                Console.ResetColor();
                string? result = TokenAuthService.ReadAndDecrypt(full);
                if (result != null) return result;
            }
        }
        return null;
    }

    // ── Envio de e-mail ───────────────────────────────────────────────────────

    static async Task SendEmailFlow(SmtpConfig smtp)
    {
        var users = UserStore.LoadAll();

        // Selecionar remetente
        Console.WriteLine("  De (username, ex: arthur.dono.1) ou Enter para usar o SMTP padrão:");
        Console.Write("  > ");
        string? fromName = Console.ReadLine()?.Trim();
        string fromAddress = string.IsNullOrEmpty(fromName)
            ? smtp.Username
            : EmailUser.BuildAddress(fromName);

        // Selecionar destinatário
        Console.WriteLine();
        Console.WriteLine("  Para:");
        Console.WriteLine("    [1] Digitar endereço manualmente");
        if (users.Count > 0)
        {
            Console.WriteLine("    [2] Escolher da lista de usuários");
        }
        Console.Write("  > ");
        string? toOpt = Console.ReadLine()?.Trim();

        string to;
        if (toOpt == "2" && users.Count > 0)
        {
            to = PickUserAddress(users);
        }
        else
        {
            Console.Write("  Endereço de destino: ");
            to = Console.ReadLine()?.Trim() ?? "";
        }

        Console.Write("  Assunto: ");
        string subject = Console.ReadLine()?.Trim() ?? "";

        Console.Write("  Corpo em HTML? (s/n): ");
        bool isHtml = Console.ReadLine()?.Trim().ToLower() == "s";

        Console.WriteLine("  Corpo (termine com uma linha contendo apenas '.'):");
        var bodyLines = new List<string>();
        string? line;
        while ((line = Console.ReadLine()) != ".")
            bodyLines.Add(line ?? "");
        string body = string.Join("\n", bodyLines);

        Console.Write("  Anexo (caminho completo, ou Enter para pular): ");
        string? attachment = Console.ReadLine()?.Trim();

        var msg = new EmailMessage
        {
            To      = to,
            Subject = subject,
            Body    = body,
            IsHtml  = isHtml,
            From    = fromAddress
        };
        if (!string.IsNullOrEmpty(attachment))
            msg.Attachments.Add(attachment);

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"\n  Enviando de {fromAddress} → {to} ...");
        Console.ResetColor();

        try
        {
            var svc = new EmailService(smtp);
            await svc.SendAsync(msg);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ E-mail enviado para {to}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ Erro: {ex.Message}");
            Console.ResetColor();
        }
    }

    static string PickUserAddress(List<EmailUser> users)
    {
        for (int i = 0; i < users.Count; i++)
            Console.WriteLine($"    [{i + 1}] {users[i].Address}  ({users[i].FullName})");
        Console.Write("  Número: ");
        if (int.TryParse(Console.ReadLine()?.Trim(), out int idx) && idx >= 1 && idx <= users.Count)
            return users[idx - 1].Address;
        Console.WriteLine("  Seleção inválida, usando entrada manual.");
        Console.Write("  Endereço: ");
        return Console.ReadLine()?.Trim() ?? "";
    }

    // ── Gerenciamento de usuários ─────────────────────────────────────────────

    static void ListUsers()
    {
        var users = UserStore.LoadAll();
        if (users.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Nenhum usuário cadastrado.");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  {"USERNAME",-30} {"NOME COMPLETO",-25} {"CARGO",-15}");
        Console.WriteLine($"  {new string('─', 72)}");
        Console.ResetColor();
        foreach (var u in users)
            Console.WriteLine($"  {u.Address,-40} {u.FullName,-25} {u.Role,-15}");
    }

    static void AddUserFlow()
    {
        Console.WriteLine("  Formato: onemapasofficeal.<username>@onemapasofficeal.com");
        Console.Write("  Username (ex: arthur.dono.1): ");
        string name = Console.ReadLine()?.Trim().ToLower() ?? "";

        if (!EmailUser.IsValidUsername(name))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  ✗ Username inválido. Use apenas letras minúsculas, números e pontos.");
            Console.ResetColor();
            return;
        }

        Console.Write("  Nome completo: ");
        string fullName = Console.ReadLine()?.Trim() ?? "";

        Console.Write("  Cargo/Role (ex: dono, admin, membro): ");
        string role = Console.ReadLine()?.Trim() ?? "membro";

        var user = new EmailUser { Name = name, FullName = fullName, Role = role };

        try
        {
            UserStore.Add(user);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Usuário adicionado: {user.Address}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ {ex.Message}");
            Console.ResetColor();
        }
    }

    static void RemoveUserFlow()
    {
        Console.Write("  Username a remover: ");
        string name = Console.ReadLine()?.Trim().ToLower() ?? "";
        try
        {
            UserStore.Remove(name);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  ✓ Usuário '{name}' removido.");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  ✗ {ex.Message}");
            Console.ResetColor();
        }
    }

    // ── SMTP config ───────────────────────────────────────────────────────────

    static SmtpConfig PromptSmtpConfig()
    {
        Console.WriteLine();
        Console.Write("  Host SMTP (ex: smtp.gmail.com): ");
        string host = Console.ReadLine()?.Trim() ?? "smtp.gmail.com";

        Console.Write("  Porta (ex: 587): ");
        int port = int.TryParse(Console.ReadLine()?.Trim(), out int p) ? p : 587;

        Console.Write("  Usuário (e-mail): ");
        string user = Console.ReadLine()?.Trim() ?? "";

        Console.Write("  Senha / App Password: ");
        string pass = ReadPassword();

        Console.Write("  Usar TLS? (s/n, padrão s): ");
        bool ssl = Console.ReadLine()?.Trim().ToLower() != "n";

        return new SmtpConfig { Host = host, Port = port, Username = user, Password = pass, UseSsl = ssl };
    }

    static string ReadPassword()
    {
        var sb = new System.Text.StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && sb.Length > 0)
            { sb.Remove(sb.Length - 1, 1); Console.Write("\b \b"); }
            else if (key.Key != ConsoleKey.Backspace)
            { sb.Append(key.KeyChar); Console.Write('*'); }
        }
        Console.WriteLine();
        return sb.ToString();
    }

    // ── Banner ────────────────────────────────────────────────────────────────

    static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine();
        Console.WriteLine("  ███████╗███╗   ███╗ █████╗ ██╗██╗     ");
        Console.WriteLine("  ██╔════╝████╗ ████║██╔══██╗██║██║     ");
        Console.WriteLine("  █████╗  ██╔████╔██║███████║██║██║     ");
        Console.WriteLine("  ██╔══╝  ██║╚██╔╝██║██╔══██║██║██║     ");
        Console.WriteLine("  ███████╗██║ ╚═╝ ██║██║  ██║██║███████╗");
        Console.WriteLine("  ╚══════╝╚═╝     ╚═╝╚═╝  ╚═╝╚═╝╚══════╝");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  onemapasofficeal.com  •  Sistema de E-mail  •  .NET 8");
        Console.ResetColor();
        Console.WriteLine();
    }
}
