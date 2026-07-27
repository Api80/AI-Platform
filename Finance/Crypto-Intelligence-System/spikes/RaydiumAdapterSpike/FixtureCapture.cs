using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace RaydiumAdapterSpike;

internal static class FixtureCapture
{
    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task CaptureTransactionsAsync(
        string manifestPath,
        string outputDirectory,
        string rpcUrl,
        CancellationToken cancellationToken)
    {
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            manifestPath,
            cancellationToken));
        Directory.CreateDirectory(outputDirectory);

        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            if (!fixture.TryGetProperty("signature", out var signatureElement) ||
                signatureElement.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var id = fixture.GetProperty("id").GetString()
                ?? throw new InvalidDataException("Fixture id is missing.");
            var signature = signatureElement.GetString()
                ?? throw new InvalidDataException($"Fixture '{id}' signature is missing.");
            var outputPath = Path.Combine(outputDirectory, $"{id}.json");

            var response = await FetchTransactionWithRetryAsync(
                client,
                rpcUrl,
                signature,
                cancellationToken);
            await File.WriteAllTextAsync(
                outputPath,
                response.ToJsonString(OutputJsonOptions) + Environment.NewLine,
                cancellationToken);
            Console.WriteLine($"Captured {id}.");
        }
    }

    public static async Task CaptureUrlAsync(
        string sourceUrl,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        var json = await client.GetStringAsync(sourceUrl, cancellationToken);
        using var document = JsonDocument.Parse(json);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await File.WriteAllTextAsync(
            outputPath,
            JsonSerializer.Serialize(document.RootElement, OutputJsonOptions) + Environment.NewLine,
            cancellationToken);
        Console.WriteLine($"Captured {sourceUrl}.");
    }

    private static async Task<JsonNode> FetchTransactionWithRetryAsync(
        HttpClient client,
        string rpcUrl,
        string signature,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            jsonrpc = "2.0",
            id = signature,
            method = "getTransaction",
            @params = new object[]
            {
                signature,
                new
                {
                    encoding = "json",
                    commitment = "finalized",
                    maxSupportedTransactionVersion = 0
                }
            }
        };

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync(
                rpcUrl,
                requestBody,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var node = JsonNode.Parse(json)
                ?? throw new InvalidDataException("RPC returned empty JSON.");

            if (node["error"] is not null)
            {
                throw new InvalidDataException($"RPC error for {signature}: {node["error"]}");
            }

            if (node["result"] is not null)
            {
                return node;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken);
        }

        throw new InvalidDataException(
            $"Transaction '{signature}' was unavailable after retry.");
    }
}
