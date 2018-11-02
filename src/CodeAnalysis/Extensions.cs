using Microsoft.CodeAnalysis;

namespace CodeAnalysis
{
    public static class Extensions
    {
        public static bool IsBetween(this int lineNumber, FileLinePositionSpan span) =>
            lineNumber <= span.EndLinePosition.Line &&
            lineNumber >= span.StartLinePosition.Line;

    }

}