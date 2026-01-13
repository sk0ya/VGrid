using System.Collections.Generic;
using System.Linq;

namespace VGrid.Services;

/// <summary>
/// Represents the type of diff operation for a line
/// </summary>
public enum DiffOperationType
{
    /// <summary>Line is unchanged</summary>
    Unchanged,
    /// <summary>Line was deleted from the left version</summary>
    Deleted,
    /// <summary>Line was added in the right version</summary>
    Added,
    /// <summary>Line was modified (content changed)</summary>
    Modified
}

/// <summary>
/// Represents a single line in the diff result
/// </summary>
public class DiffLine
{
    public DiffOperationType Type { get; set; }
    public int? LeftLineNumber { get; set; }
    public int? RightLineNumber { get; set; }
    public string? LeftContent { get; set; }
    public string? RightContent { get; set; }
}

/// <summary>
/// Implements Myers' diff algorithm for line-based text comparison
/// </summary>
public class DiffAlgorithm
{
    /// <summary>
    /// Computes the diff between two arrays of lines
    /// </summary>
    public static List<DiffLine> ComputeDiff(string[] leftLines, string[] rightLines)
    {
        var result = new List<DiffLine>();

        // Compute LCS (Longest Common Subsequence) based diff
        var lcs = ComputeLCS(leftLines, rightLines);
        var diffOps = BacktrackLCS(leftLines, rightLines, lcs);

        int leftIdx = 0;
        int rightIdx = 0;

        foreach (var op in diffOps)
        {
            switch (op.Type)
            {
                case DiffOperationType.Unchanged:
                    result.Add(new DiffLine
                    {
                        Type = DiffOperationType.Unchanged,
                        LeftLineNumber = leftIdx + 1,
                        RightLineNumber = rightIdx + 1,
                        LeftContent = leftLines[leftIdx],
                        RightContent = rightLines[rightIdx]
                    });
                    leftIdx++;
                    rightIdx++;
                    break;

                case DiffOperationType.Deleted:
                    result.Add(new DiffLine
                    {
                        Type = DiffOperationType.Deleted,
                        LeftLineNumber = leftIdx + 1,
                        RightLineNumber = null,
                        LeftContent = leftLines[leftIdx],
                        RightContent = null
                    });
                    leftIdx++;
                    break;

                case DiffOperationType.Added:
                    result.Add(new DiffLine
                    {
                        Type = DiffOperationType.Added,
                        LeftLineNumber = null,
                        RightLineNumber = rightIdx + 1,
                        LeftContent = null,
                        RightContent = rightLines[rightIdx]
                    });
                    rightIdx++;
                    break;

                case DiffOperationType.Modified:
                    result.Add(new DiffLine
                    {
                        Type = DiffOperationType.Modified,
                        LeftLineNumber = leftIdx + 1,
                        RightLineNumber = rightIdx + 1,
                        LeftContent = leftLines[leftIdx],
                        RightContent = rightLines[rightIdx]
                    });
                    leftIdx++;
                    rightIdx++;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Computes the LCS matrix using dynamic programming
    /// </summary>
    private static int[,] ComputeLCS(string[] left, string[] right)
    {
        int m = left.Length;
        int n = right.Length;
        var lcs = new int[m + 1, n + 1];

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (left[i - 1] == right[j - 1])
                {
                    lcs[i, j] = lcs[i - 1, j - 1] + 1;
                }
                else
                {
                    lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                }
            }
        }

        return lcs;
    }

    /// <summary>
    /// Backtracks through the LCS matrix to determine diff operations
    /// </summary>
    private static List<DiffLine> BacktrackLCS(string[] left, string[] right, int[,] lcs)
    {
        var result = new List<DiffLine>();
        int i = left.Length;
        int j = right.Length;

        while (i > 0 || j > 0)
        {
            if (i > 0 && j > 0 && left[i - 1] == right[j - 1])
            {
                // Lines are identical
                result.Add(new DiffLine { Type = DiffOperationType.Unchanged });
                i--;
                j--;
            }
            else if (i > 0 && j > 0 && lcs[i, j] == lcs[i - 1, j - 1])
            {
                // Lines are different - mark as modified
                result.Add(new DiffLine { Type = DiffOperationType.Modified });
                i--;
                j--;
            }
            else if (j > 0 && (i == 0 || lcs[i, j - 1] >= lcs[i - 1, j]))
            {
                // Line was added
                result.Add(new DiffLine { Type = DiffOperationType.Added });
                j--;
            }
            else if (i > 0)
            {
                // Line was deleted
                result.Add(new DiffLine { Type = DiffOperationType.Deleted });
                i--;
            }
        }

        result.Reverse();
        return result;
    }
}
