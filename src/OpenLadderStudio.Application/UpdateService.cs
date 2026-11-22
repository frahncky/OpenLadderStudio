using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenLadderStudio.Application;

/// <summary>Release publicada: versão e os dois arquivos que a atualização precisa.</summary>
public sealed record UpdateRelease(string Version, string? SetupUrl, string? HashUrl)
{
    public bool IsComplete => !string.IsNullOrEmpty(SetupUrl) && !string.IsNullOrEmpty(HashUrl);
}

public enum UpdateState
{
    UpToDate,
    Available,
    Incomplete
}

public sealed record UpdateCheck(UpdateState State, string CurrentVersion, UpdateRelease Release);

/// <summary>Acesso à rede da atualização (substituível no teste, que não usa a internet).</summary>
public interface IUpdateTransport
{
    Task<string> GetStringAsync(string url, CancellationToken cancellation);
    Task DownloadAsync(string url, string path, IProgress<int>? progress, CancellationToken cancellation);
}

/// <summary>
/// Atualização pelo GitHub, como no editor distribuído: compara a versão instalada com a
/// última release, baixa o instalador e o .sha256 e só entrega o instalador se o SHA-256
/// conferir. Não executa nada: quem chama decide quando instalar.
/// </summary>
public sealed class UpdateService
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/frahncky/OpenLadderStudio/releases/latest";
    public const string SetupAssetName = "OpenLadder-Studio-Setup.exe";
    public const string HashAssetName = "OpenLadder-Studio-Setup.exe.sha256";

    /// <summary>Argumentos do instalador (Inno Setup), os mesmos do atualizador distribuído.</summary>
    public const string InstallerArguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS";

    private readonly IUpdateTransport transport;

    public UpdateService(IUpdateTransport transport, string currentVersion)
    {
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        CurrentVersion = (currentVersion ?? string.Empty).Trim().TrimStart('v', 'V');
    }

    public string CurrentVersion { get; }

    public async Task<UpdateCheck> CheckAsync(CancellationToken cancellation = default)
    {
        string json = await transport.GetStringAsync(LatestReleaseUrl, cancellation).ConfigureAwait(false);
        UpdateRelease release = ParseRelease(json);
        UpdateState state = CompareVersions(release.Version, CurrentVersion) <= 0
            ? UpdateState.UpToDate
            : release.IsComplete ? UpdateState.Available : UpdateState.Incomplete;
        return new UpdateCheck(state, CurrentVersion, release);
    }

    /// <summary>
    /// Baixa o .sha256 e o instalador para a pasta e devolve o caminho do instalador.
    /// SHA-256 ausente ou diferente: apaga o instalador e lança InvalidDataException.
    /// </summary>
    public async Task<string> DownloadVerifiedAsync(UpdateRelease release, string folder, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (!release.IsComplete) throw new InvalidDataException("Pacote de atualização incompleto.");
        Directory.CreateDirectory(folder);
        string hashFile = Path.Combine(folder, HashAssetName);
        string setup = Path.Combine(folder, SetupAssetName);

        await transport.DownloadAsync(release.HashUrl!, hashFile, null, cancellation).ConfigureAwait(false);
        string expected = ExtractHash(await File.ReadAllTextAsync(hashFile, cancellation).ConfigureAwait(false))
            ?? throw new InvalidDataException("O arquivo SHA-256 da release é inválido.");
        await transport.DownloadAsync(release.SetupUrl!, setup, progress, cancellation).ConfigureAwait(false);
        if (!string.Equals(expected, Sha256Of(setup), StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(setup);
            throw new InvalidDataException("O SHA-256 do instalador não confere com o publicado. Nada foi instalado.");
        }
        return setup;
    }

    public static UpdateRelease ParseRelease(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            string tag = root.TryGetProperty("tag_name", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
            string version = tag.Trim().TrimStart('v', 'V');
            if (!Regex.IsMatch(version, @"^\d+(\.\d+)*")) throw new InvalidDataException("A release não informa uma versão válida.");
            string? setup = null, hash = null;
            if (root.TryGetProperty("assets", out JsonElement assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
                    string? url = asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() : null;
                    if (string.Equals(name, SetupAssetName, StringComparison.OrdinalIgnoreCase)) setup = url;
                    else if (string.Equals(name, HashAssetName, StringComparison.OrdinalIgnoreCase)) hash = url;
                }
            }
            return new UpdateRelease(version, setup, hash);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Resposta inesperada do servidor de releases.", ex);
        }
    }

    /// <summary>Compara versões numéricas por partes ("1.9" &lt; "1.10").</summary>
    public static int CompareVersions(string? a, string? b)
    {
        string[] pa = (a ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
        string[] pb = (b ?? string.Empty).Trim().TrimStart('v', 'V').Split('.');
        for (int i = 0; i < Math.Max(pa.Length, pb.Length); i++)
        {
            int va = i < pa.Length ? LeadingInt(pa[i]) : 0;
            int vb = i < pb.Length ? LeadingInt(pb[i]) : 0;
            if (va != vb) return va.CompareTo(vb);
        }
        return 0;
    }

    /// <summary>Primeiro hash de 64 dígitos hexadecimais do texto (formato "hash  arquivo" ou só o hash).</summary>
    public static string? ExtractHash(string text)
    {
        Match match = Regex.Match(text ?? string.Empty, @"\b[0-9A-Fa-f]{64}\b");
        return match.Success ? match.Value : null;
    }

    public static string Sha256Of(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static int LeadingInt(string part)
    {
        Match match = Regex.Match(part ?? string.Empty, @"^\d+");
        return match.Success && int.TryParse(match.Value, out int value) ? value : 0;
    }
}

/// <summary>Transporte real: HTTPS com o User-Agent genérico do atualizador.</summary>
public sealed class HttpUpdateTransport : IUpdateTransport, IDisposable
{
    private readonly HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };

    public HttpUpdateTransport()
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenLadder-Studio-Updater");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public Task<string> GetStringAsync(string url, CancellationToken cancellation) => client.GetStringAsync(url, cancellation);

    public async Task DownloadAsync(string url, string path, IProgress<int>? progress, CancellationToken cancellation)
    {
        using HttpResponseMessage response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        long? total = response.Content.Headers.ContentLength;
        await using Stream source = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        await using FileStream target = File.Create(path);
        var buffer = new byte[81920];
        long done = 0;
        int read, last = -1;
        while ((read = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
            done += read;
            if (total is > 0 && progress is not null)
            {
                int percent = (int)(done * 100 / total.Value);
                if (percent != last) progress.Report(last = percent);
            }
        }
    }

    public void Dispose() => client.Dispose();
}
