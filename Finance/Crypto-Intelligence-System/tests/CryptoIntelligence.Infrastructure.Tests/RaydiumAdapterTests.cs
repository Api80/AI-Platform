using System.Numerics;
using System.Text.Json;
using CryptoIntelligence.Application.Ingestion;
using CryptoIntelligence.Infrastructure.Solana.Raydium;

namespace CryptoIntelligence.Infrastructure.Tests;

public sealed class RaydiumAdapterTests
{
    [Fact]
    public void Formal_adapter_matches_all_pinned_mainnet_fixtures()
    {
        var fixtureRoot = Path.Combine(
            FindProjectRoot(),
            "samples",
            "adapter-spike",
            "raydium-launchlab-cpmm");
        var adapter = CreateAdapter(fixtureRoot);
        using var manifestDocument = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixtureRoot, "fixture-manifest.json")));

        foreach (var fixture in manifestDocument.RootElement
                     .GetProperty("fixtures")
                     .EnumerateArray())
        {
            var id = fixture.GetProperty("id").GetString()!;
            var rawPath = Path.Combine(fixtureRoot, "raw", $"{id}.json");
            var first = adapter.Parse(File.ReadAllText(rawPath));
            var second = adapter.Parse(File.ReadAllText(rawPath));
            var expected = fixture.GetProperty("expectedDomainEvents")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToHashSet(StringComparer.Ordinal);
            var actual = first.Events
                .Select(value => value.DomainEventType)
                .ToHashSet(StringComparer.Ordinal);

            Assert.Equal(
                fixture.GetProperty("transactionOutcome").GetString() == "failed",
                first.Failed);
            Assert.Subset(actual, expected);
            Assert.Equal(Fingerprint(first), Fingerprint(second));
        }
    }

    [Fact]
    public void Adapter_rejects_unknown_supported_program_discriminator()
    {
        var fixtureRoot = Path.Combine(
            FindProjectRoot(),
            "samples",
            "adapter-spike",
            "raydium-launchlab-cpmm");
        var adapter = CreateAdapter(fixtureRoot);
        var supportedProgram = adapter.ProgramIds.First();
        var json = JsonSerializer.Serialize(new
        {
            slot = 1,
            meta = new
            {
                err = (object?)null,
                innerInstructions = Array.Empty<object>(),
                logMessages = Array.Empty<string>()
            },
            transaction = new
            {
                message = new
                {
                    accountKeys = new[] { supportedProgram },
                    instructions = new[]
                    {
                        new
                        {
                            programIdIndex = 0,
                            data = Base58Encode([1, 2, 3, 4, 5, 6, 7, 8])
                        }
                    }
                }
            }
        });

        Assert.Throws<UnsupportedProgramVersionException>(() => adapter.Parse(json));
    }

    private static RaydiumTransactionAdapter CreateAdapter(string fixtureRoot) => new(
        "raydium-launchlab-cpmm-v1",
        File.ReadAllText(Path.Combine(fixtureRoot, "idl", "raydium_launchpad.json")),
        File.ReadAllText(Path.Combine(fixtureRoot, "idl", "raydium_cp_swap.json")));

    private static string Fingerprint(AdapterParseResult result) => string.Join(
        '|',
        result.Events.Select(value =>
            $"{value.ProgramId}:{value.Name}:{value.InstructionIndex}:" +
            $"{value.InnerInstructionIndex}:{value.EventOrdinal}:" +
            $"{value.DomainEventType}:{value.Source}:{value.PayloadFingerprint}"));

    private static string Base58Encode(byte[] bytes)
    {
        const string alphabet =
            "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
        var number = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
        var encoded = string.Empty;
        while (number > 0)
        {
            number = BigInteger.DivRem(number, 58, out var remainder);
            encoded = alphabet[(int)remainder] + encoded;
        }

        return new string('1', bytes.TakeWhile(value => value == 0).Count()) + encoded;
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CryptoIntelligence.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("CryptoIntelligence.sln was not found.");
    }
}
