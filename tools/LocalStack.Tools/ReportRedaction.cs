using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LocalStack.Tools;

internal static partial class ReportRedaction
{
    /// <summary>
    /// Matches TUnit 1.75's gzip/base64 report JSON, which visible-text masking cannot protect.
    /// </summary>
    [GeneratedRegex("""<script id="report-data" type="application/octet-stream">(?<payload>[A-Za-z0-9+/=\r\n]+)</script>""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 2000)]
    private static partial Regex HtmlPayload();

    [GeneratedRegex("""\bid\s*=\s*["']report-data["']""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 2000)]
    private static partial Regex HtmlPayloadIds();

    [GeneratedRegex("""<script\b[^>]*\btype\s*=\s*["']application/octet-stream["'][^>]*>""",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, 2000)]
    private static partial Regex HtmlPayloadScripts();

    internal static string Mask(string text, string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return text;
        }

        return text.Replace(secret, "[REDACTED]", StringComparison.Ordinal)
            .Replace(WebUtility.HtmlEncode(secret), "[REDACTED]", StringComparison.Ordinal)
            .Replace(Uri.EscapeDataString(secret), "[REDACTED]", StringComparison.Ordinal);
    }

    internal static async Task RedactDirectoryAsync(string directory, string? secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            var failure = await RedactFileAsync(directory, path, secret).ConfigureAwait(false);
            if (failure is not null)
            {
                throw new InvalidDataException(failure);
            }
        }
    }

    private static async Task<string?> RedactFileAsync(string directory, string path, string? secret)
    {
        try
        {
            Check(Path.GetRelativePath(directory, path), secret);
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException();
            }

            if (Directory.Exists(path))
            {
                return null;
            }

            var content = await File.ReadAllTextAsync(path, CancellationToken.None).ConfigureAwait(false);
            content = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".html" => await RedactHtmlAsync(content, secret).ConfigureAwait(false),
                ".json" => RedactJson(JsonNode.Parse(content), secret)?.ToJsonString() ?? "null",
                ".trx" or ".xml" => RedactXml(content, secret),
                ".log" or ".txt" => Mask(content, secret),
                _ => throw new InvalidDataException(),
            };
            Check(content, secret);
            await File.WriteAllTextAsync(path, content, CancellationToken.None).ConfigureAwait(false);
            return null;
        }
        catch (Exception)
        {
            return $"Report could not be safely checked: {Mask(Path.GetRelativePath(directory, path), secret)}";
        }
    }

    private static string RedactXml(string content, string? secret)
    {
        var document = XDocument.Parse(content);
        foreach (var text in document.DescendantNodes().OfType<XText>())
        {
            text.Value = Mask(text.Value, secret);
        }

        foreach (var attribute in document.Descendants().Attributes())
        {
            attribute.Value = Mask(attribute.Value, secret);
        }

        return document.ToString();
    }

    internal static async Task<string?> RedactSummaryAsync(string source, string? destination, string? secret)
    {
        try
        {
            if (!File.Exists(source) && !Directory.Exists(source))
            {
                return null;
            }

            var text = Mask(await File.ReadAllTextAsync(source, CancellationToken.None).ConfigureAwait(false), secret);
            Check(text, secret);
            await File.WriteAllTextAsync(source, text, CancellationToken.None).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(destination))
            {
                await File.AppendAllTextAsync(destination, text, CancellationToken.None).ConfigureAwait(false);
            }

            return null;
        }
        catch (Exception)
        {
            return $"Private runtime summary could not be safely finalized: {Mask(Path.GetFileName(source), secret)}";
        }
    }

    private static void Check(string text, string? secret)
    {
        if (!string.IsNullOrEmpty(secret) && (text.Contains(secret, StringComparison.Ordinal)
            || text.Contains(WebUtility.HtmlEncode(secret), StringComparison.Ordinal)
            || text.Contains(Uri.EscapeDataString(secret), StringComparison.Ordinal)))
        {
            throw new InvalidDataException();
        }
    }

    private static async Task<string> RedactHtmlAsync(string html, string? secret)
    {
        var matches = HtmlPayload().Matches(html);
        if (matches.Count != 1 || HtmlPayloadIds().Count(html) != 1 || HtmlPayloadScripts().Count(html) != 1)
        {
            throw new InvalidDataException("The TUnit HTML payload format changed; no unsanitized report can be published.");
        }

        var payload = matches[0].Groups["payload"];
        var input = new MemoryStream(Convert.FromBase64String(payload.Value));
        await using var inputLifetime = input.ConfigureAwait(false);
        var gzip = new GZipStream(input, CompressionMode.Decompress);
        await using var gzipLifetime = gzip.ConfigureAwait(false);
        var node = await JsonNode.ParseAsync(gzip, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        var json = RedactJson(node, secret)!.ToJsonString();
        Check(json, secret);
        var output = new MemoryStream();
        await using var outputLifetime = output.ConfigureAwait(false);
        var compressor = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
        await using (compressor.ConfigureAwait(false))
        {
            await compressor.WriteAsync(Encoding.UTF8.GetBytes(json), CancellationToken.None).ConfigureAwait(false);
        }

        return Mask(html[..payload.Index] + Convert.ToBase64String(output.ToArray()) + html[(payload.Index + payload.Length)..], secret);
    }

    private static JsonNode? RedactJson(JsonNode? node, string? secret)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonValue.Create(Mask(text, secret));
            case JsonObject obj:
                foreach (var item in obj.ToArray())
                {
                    Check(item.Key, secret);
                    var replacement = RedactJson(item.Value, secret);
                    if (!ReferenceEquals(replacement, item.Value))
                    {
                        obj[item.Key] = replacement;
                    }
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    var replacement = RedactJson(array[index], secret);
                    if (!ReferenceEquals(replacement, array[index]))
                    {
                        array[index] = replacement;
                    }
                }

                break;
        }

        return node;
    }
}
