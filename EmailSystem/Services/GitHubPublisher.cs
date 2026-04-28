using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace EmailSystem.Services;

/// <summary>
/// Publica arquivos no GitHub via REST API (sem git instalado).
/// </summary>
public static class GitHubPublisher
{
    private const string Owner  = "onemapasofficeal-office";
    private const string Repo   = "system-email-onemapasofficeal";
    private const string Branch = "main";
    private const string ApiBase = "https://api.github.com";

    private static HttpClient MakeClient(string pat)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EmailSystem", "1.0"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>Cria ou atualiza um arquivo no repositório.</summary>
    public static async Task PutFileAsync(string pat, string repoPath, string content, string commitMessage)
    {
        using var client = MakeClient(pat);
        string url = $"{ApiBase}/repos/{Owner}/{Repo}/contents/{repoPath}";

        // Verifica se o arquivo já existe (para pegar o SHA)
        string? sha = null;
        var getResp = await client.GetAsync(url);
        if (getResp.IsSuccessStatusCode)
        {
            var doc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
            sha = doc.RootElement.GetProperty("sha").GetString();
        }

        var body = new Dictionary<string, object?>
        {
            ["message"] = commitMessage,
            ["content"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            ["branch"]  = Branch
        };
        if (sha != null) body["sha"] = sha;

        var json    = JsonSerializer.Serialize(body);
        var putResp = await client.PutAsync(url,
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!putResp.IsSuccessStatusCode)
        {
            string err = await putResp.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API error {putResp.StatusCode}: {err}");
        }
    }
}
