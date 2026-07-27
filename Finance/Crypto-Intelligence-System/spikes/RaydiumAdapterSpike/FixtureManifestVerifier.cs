using System.Text.Json;

namespace RaydiumAdapterSpike;

internal static class FixtureManifestVerifier
{
    public static void Verify(
        string manifestPath,
        string rawDirectory,
        IdlDiscriminatorRegistry registry)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var verified = 0;

        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            if (!fixture.TryGetProperty("signature", out var signature) ||
                signature.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            var id = RequiredString(fixture, "id");
            var path = Path.Combine(rawDirectory, $"{id}.json");
            if (!File.Exists(path))
            {
                throw new InvalidDataException($"Raw fixture '{path}' does not exist.");
            }

            var parsed = SolanaTransactionFixtureParser.Parse(File.ReadAllText(path), registry);
            var expectedSlot = fixture.GetProperty("slot").GetInt64();
            if (parsed.Slot != expectedSlot)
            {
                throw new InvalidDataException(
                    $"Fixture '{id}' expected slot {expectedSlot}, parsed {parsed.Slot}.");
            }

            var expectedFailure = RequiredString(fixture, "transactionOutcome") == "failed";
            if (parsed.Failed != expectedFailure)
            {
                throw new InvalidDataException(
                    $"Fixture '{id}' failure state does not match the manifest.");
            }

            VerifyExpectedValues(
                id,
                "instruction",
                fixture,
                "expectedInstructions",
                parsed.Instructions.Select(value => value.Name));
            VerifyExpectedValues(
                id,
                "domain event",
                fixture,
                "expectedDomainEvents",
                parsed.Events.Select(value => value.DomainEventType));
            verified++;
        }

        Console.WriteLine($"Manifest assertions passed for {verified} fixtures.");
    }

    private static void VerifyExpectedValues(
        string fixtureId,
        string valueKind,
        JsonElement fixture,
        string propertyName,
        IEnumerable<string> actualValues)
    {
        if (!fixture.TryGetProperty(propertyName, out var expectedElement))
        {
            return;
        }

        var actual = actualValues.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        foreach (var expectedValue in expectedElement.EnumerateArray())
        {
            var expected = expectedValue.GetString()
                ?? throw new InvalidDataException(
                    $"Fixture '{fixtureId}' contains a null expected {valueKind}.");
            if (!actual.Contains(Normalize(expected)))
            {
                throw new InvalidDataException(
                    $"Fixture '{fixtureId}' did not produce expected {valueKind} '{expected}'.");
            }
        }
    }

    private static string RequiredString(JsonElement value, string propertyName) =>
        value.GetProperty(propertyName).GetString()
        ?? throw new InvalidDataException($"Fixture property '{propertyName}' is missing.");

    private static string Normalize(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
