using System.Net.Http.Json;

namespace Arrkit.Library;

/// <summary>Tells Jellyfin about new files so it picks them up now, without scanning every library.</summary>
public static class JellyfinScan
{
    /// <summary>
    /// POST /Library/Media/Updated with the new files (Jellyfin must see them at the same paths: /movies, /tv).
    /// Returns the error, or null when Jellyfin took it.
    /// </summary>
    public static async Task<string?> FilesAddedAsync(HttpClient http, string url, string apiKey, IEnumerable<string> paths,
        CancellationToken cancellationToken)
    {
        var updates = paths.Distinct(StringComparer.Ordinal).Select(p => new { Path = p, UpdateType = "Created" }).ToList();
        if (updates.Count == 0 || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{url.Trim().TrimEnd('/')}/Library/Media/Updated");
            request.Headers.TryAddWithoutValidation("Authorization", $"MediaBrowser Token=\"{apiKey.Trim()}\"");
            request.Content = JsonContent.Create(new { Updates = updates });
            using var response = await http.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode ? null : $"Jellyfin answered {(int)response.StatusCode} {response.ReasonPhrase}.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return $"Could not reach Jellyfin: {e.Message}";
        }
    }
}
