using System.Text.RegularExpressions;

namespace RaydiumAdapterSpike;

internal sealed record LocatedProgramEvent(
    string ProgramId,
    string Kind,
    string Value,
    int LogIndex,
    int EventOrdinal);

internal static partial class SolanaLogEventLocator
{
    public static IReadOnlyList<LocatedProgramEvent> Locate(
        IReadOnlyList<string> logMessages,
        IReadOnlySet<string> supportedProgramIds)
    {
        ArgumentNullException.ThrowIfNull(logMessages);
        ArgumentNullException.ThrowIfNull(supportedProgramIds);

        var callStack = new Stack<string>();
        var result = new List<LocatedProgramEvent>();

        for (var logIndex = 0; logIndex < logMessages.Count; logIndex++)
        {
            var message = logMessages[logIndex];
            var invokeMatch = ProgramInvokeRegex().Match(message);

            if (invokeMatch.Success)
            {
                callStack.Push(invokeMatch.Groups["program"].Value);
                continue;
            }

            var completionMatch = ProgramCompletionRegex().Match(message);
            if (completionMatch.Success)
            {
                PopCompletedProgram(callStack, completionMatch.Groups["program"].Value);
                continue;
            }

            if (callStack.Count == 0 || !supportedProgramIds.Contains(callStack.Peek()))
            {
                continue;
            }

            const string instructionPrefix = "Program log: Instruction: ";
            if (message.StartsWith(instructionPrefix, StringComparison.Ordinal))
            {
                result.Add(new LocatedProgramEvent(
                    callStack.Peek(),
                    "Instruction",
                    message[instructionPrefix.Length..],
                    logIndex,
                    result.Count));
                continue;
            }

            const string dataPrefix = "Program data: ";
            if (message.StartsWith(dataPrefix, StringComparison.Ordinal))
            {
                result.Add(new LocatedProgramEvent(
                    callStack.Peek(),
                    "ProgramData",
                    message[dataPrefix.Length..],
                    logIndex,
                    result.Count));
            }
        }

        return result;
    }

    private static void PopCompletedProgram(Stack<string> callStack, string completedProgram)
    {
        while (callStack.Count > 0)
        {
            var program = callStack.Pop();
            if (string.Equals(program, completedProgram, StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    [GeneratedRegex("^Program (?<program>\\S+) invoke \\[\\d+\\]$")]
    private static partial Regex ProgramInvokeRegex();

    [GeneratedRegex("^Program (?<program>\\S+) (success|failed: .+)$")]
    private static partial Regex ProgramCompletionRegex();
}
