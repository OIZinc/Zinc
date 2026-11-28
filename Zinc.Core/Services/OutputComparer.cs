using Zinc.Core.Abstractions;
using Zinc.Core.Models;

namespace Zinc.Core.Services;

public class OutputComparer : IOutputComparer
{
    private const string EndOfFileMarker = "<EOF>";

    public ComparisonResult Compare(string actual, string expected)
    {
        var result = new ComparisonResult();

        var actualLines = SplitLines(actual);
        var expectedLines = SplitLines(expected);

        // 判定基于归一化后的行内容；差异展示基于原始行内容。
        result.NormalizedActual = string.Join("\n", actualLines.Select(TrimLineEnd));
        result.NormalizedExpected = string.Join("\n", expectedLines.Select(TrimLineEnd));

        int actualEnd = ContentLineCount(actualLines);
        int expectedEnd = ContentLineCount(expectedLines);

        result.IsMatch = LinesEqual(actualLines, expectedLines, actualEnd, expectedEnd);

        if (!result.IsMatch)
        {
            CollectDifferences(result, actualLines, expectedLines, actualEnd, expectedEnd);
        }

        return result;
    }

    /// <summary>换行归一化为 \n 后按行切分，保留原始行内容。</summary>
    private static string[] SplitLines(string? text)
        => NormalizeNewlines(text ?? string.Empty)
            .Split('\n');

    private static string NormalizeNewlines(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n');

    /// <summary>去掉一行末尾的空格与制表符（不含换行本身）。</summary>
    private static string TrimLineEnd(string line)
        => line.TrimEnd(' ', '\t');

    /// <summary>文件末尾的空行（仅含空白）不计入内容行数。</summary>
    private static int ContentLineCount(string[] lines)
    {
        int count = lines.Length;
        while (count > 0 && TrimLineEnd(lines[count - 1]).Length == 0)
        {
            count--;
        }

        return count;
    }

    private static bool LinesEqual(
        string[] actual, string[] expected, int actualEnd, int expectedEnd)
    {
        if (actualEnd != expectedEnd)
            return false;

        for (int i = 0; i < actualEnd; i++)
        {
            if (!string.Equals(TrimLineEnd(actual[i]), TrimLineEnd(expected[i]), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static void CollectDifferences(
        ComparisonResult result,
        string[] actual, string[] expected, int actualEnd, int expectedEnd)
    {
        int max = Math.Max(actualEnd, expectedEnd);
        for (int i = 0; i < max; i++)
        {
            string actualLine = i < actualEnd ? actual[i] : EndOfFileMarker;
            string expectedLine = i < expectedEnd ? expected[i] : EndOfFileMarker;

            bool differs = i >= actualEnd
                           || i >= expectedEnd
                           || !string.Equals(TrimLineEnd(actualLine), TrimLineEnd(expectedLine), StringComparison.Ordinal);

            if (differs)
            {
                result.Differences.Add(new Difference
                {
                    LineNumber = i + 1,
                    Actual = actualLine,
                    Expected = expectedLine,
                });
            }
        }
    }
}
