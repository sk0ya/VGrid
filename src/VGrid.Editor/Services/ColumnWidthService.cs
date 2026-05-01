using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using VGrid.Models;

namespace VGrid.Services
{
    /// <summary>
    /// Service for calculating optimal column widths based on cell content
    /// </summary>
    public class ColumnWidthService : IColumnWidthService
    {
        private const int WidthSamplingThreshold = 2000;
        private const int HeadTailSampleSize = 500;
        private const int MiddleSampleSize = 500;

        public double MinColumnWidth { get; set; } = 60;
        public double MaxColumnWidth { get; set; } = 600;
        public double CellPadding { get; set; } = 19; // 4(left) + 4(right) + 1(border) + 10(margin)

        /// <summary>
        /// Measures the width of a single text string
        /// </summary>
        public double MeasureTextWidth(string text, Typeface typeface, double fontSize)
        {
            if (string.IsNullOrEmpty(text))
                return MinColumnWidth;

            try
            {
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    TextFormattingMode.Display,
                    96.0); // Default DPI

                // Add padding and ensure within min/max bounds
                double width = formattedText.Width + CellPadding;
                return Math.Max(MinColumnWidth, Math.Min(width, MaxColumnWidth));
            }
            catch
            {
                // Fallback if text measurement fails
                return MinColumnWidth;
            }
        }

        /// <summary>
        /// Calculates the optimal width for a specific column based on all cell contents
        /// </summary>
        public double CalculateColumnWidth(TsvDocument document, int columnIndex, Typeface typeface, double fontSize)
        {
            if (document == null || columnIndex < 0)
                return MinColumnWidth;

            double maxWidth = MinColumnWidth;
            int rowCount = document.RowCount;

            if (rowCount == 0)
                return MinColumnWidth;

            // For larger files, sample representative row slices to keep tab opening responsive.
            bool shouldSample = rowCount > WidthSamplingThreshold;
            IEnumerable<Row> rowsToCheck = shouldSample
                ? SampleRows(document.Rows, rowCount)
                : document.Rows;

            foreach (var row in rowsToCheck)
            {
                var cell = row.GetCell(columnIndex);
                if (cell != null && !string.IsNullOrEmpty(cell.Value))
                {
                    double width = MeasureTextWidth(cell.Value, typeface, fontSize);
                    if (width > maxWidth)
                    {
                        maxWidth = width;
                        if (maxWidth >= MaxColumnWidth)
                        {
                            return MaxColumnWidth; // Early exit optimization
                        }
                    }
                }
            }

            return maxWidth;
        }

        /// <summary>
        /// Calculates optimal widths for all columns in the document
        /// </summary>
        public Dictionary<int, double> CalculateAllColumnWidths(TsvDocument document, Typeface typeface, double fontSize)
        {
            var widths = new Dictionary<int, double>();

            if (document == null)
                return widths;

            int columnCount = document.ColumnCount;

            for (int i = 0; i < columnCount; i++)
            {
                widths[i] = CalculateColumnWidth(document, i, typeface, fontSize);
            }

            return widths;
        }

        /// <summary>
        /// Samples representative rows from a large document (head + middle + tail)
        /// </summary>
        private IEnumerable<Row> SampleRows(System.Collections.ObjectModel.ObservableCollection<Row> rows, int totalCount)
        {
            if (totalCount <= WidthSamplingThreshold)
                return rows;

            var indices = new HashSet<int>();

            int headCount = Math.Min(HeadTailSampleSize, totalCount);
            for (int i = 0; i < headCount; i++)
            {
                indices.Add(i);
            }

            int middleStart = Math.Max(headCount, (totalCount - MiddleSampleSize) / 2);
            int middleEnd = Math.Min(totalCount, middleStart + MiddleSampleSize);
            for (int i = middleStart; i < middleEnd; i++)
            {
                indices.Add(i);
            }

            int tailStart = Math.Max(headCount, totalCount - HeadTailSampleSize);
            for (int i = tailStart; i < totalCount; i++)
            {
                indices.Add(i);
            }

            return indices.OrderBy(i => i).Select(i => rows[i]);
        }
    }
}
