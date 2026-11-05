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

        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".html":
                    var html = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    await File.WriteAllTextAsync(path, await RedactHtmlAsync(html, secret).ConfigureAwait(false)).ConfigureAwait(false);
                    break;
                case ".json":
                    var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    await File.WriteAllTextAsync(path, RedactJson(JsonNode.Parse(json), secret)!.ToJsonString()).ConfigureAwait(false);
                    break;
                case ".trx":
                case ".xml":
                    var document = XDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
                    foreach (var text in document.DescendantNodes().OfType<XText>())
                    {
                        text.Value = Mask(text.Value, secret);
                    }

                    foreach (var attribute in document.Descendants().Attributes())
                    {
                        attribute.Value = Mask(attribute.Value, secret);
                    }

                    await File.WriteAllTextAsync(path, document.ToString()).ConfigureAwait(false);
                    break;
                case ".log":
                case ".txt":
                    var textContent = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                    await File.WriteAllTextAsync(path, Mask(textContent, secret)).ConfigureAwait(false);
                    break;
            }
        }
    }

    internal static async Task RedactSummaryAsync(string? secret)
    {
        var summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");
        if (summary is not null && File.Exists(summary))
        {
            var text = await File.ReadAllTextAsync(summary, CancellationToken.None).ConfigureAwait(false);
            await File.WriteAllTextAsync(summary, Mask(text, secret), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task<string> RedactHtmlAsync(string html, string secret)
    {
        var match = HtmlPayload().Match(html);
        if (!match.Success)
        {
            throw new InvalidDataException("The TUnit HTML payload format changed; no unsanitized report can be published.");
        }

        var payload = match.Groups["payload"];
        var input = new MemoryStream(Convert.FromBase64String(payload.Value));
        await using var inputLifetime = input.ConfigureAwait(false);
        var gzip = new GZipStream(input, CompressionMode.Decompress);
        await using var gzipLifetime = gzip.ConfigureAwait(false);
        var node = await JsonNode.ParseAsync(gzip).ConfigureAwait(false);
        var json = RedactJson(node, secret)!.ToJsonString();
        var output = new MemoryStream();
        await using var outputLifetime = output.ConfigureAwait(false);
        var compressor = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);
        await using (compressor.ConfigureAwait(false))
        {
            await compressor.WriteAsync(Encoding.UTF8.GetBytes(json)).ConfigureAwait(false);
        }

        return Mask(html[..payload.Index] + Convert.ToBase64String(output.ToArray()) + html[(payload.Index + payload.Length)..], secret);
    }

    private static JsonNode? RedactJson(JsonNode? node, string secret)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue<string>(out var text):
                return JsonValue.Create(Mask(text, secret));
            case JsonObject obj:
                foreach (var item in obj.ToArray())
                {
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
