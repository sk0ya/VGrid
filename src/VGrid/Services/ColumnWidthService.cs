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
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    System.Windows.Media.Brushes.Black,
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

            // For large files (>10000 rows), sample first 1000 and last 1000 rows
            bool shouldSample = rowCount > 10000;
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
        /// Samples rows from a large document (first 1000 + last 1000)
        /// </summary>
        private IEnumerable<Row> SampleRows(System.Collections.ObjectModel.ObservableCollection<Row> rows, int totalCount)
        {
            // Take first 1000 rows
            var firstBatch = rows.Take(1000);

            // Take last 1000 rows
            var lastBatch = rows.Skip(Math.Max(0, totalCount - 1000));

            return firstBatch.Concat(lastBatch);
        }
    }
}
