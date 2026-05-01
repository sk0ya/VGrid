using System.Collections.Generic;
using System.Windows.Media;
using VGrid.Models;

namespace VGrid.Services
{
    /// <summary>
    /// Service for calculating optimal column widths based on cell content
    /// </summary>
    public interface IColumnWidthService
    {
        /// <summary>
        /// Calculates the optimal width for a specific column based on all cell contents
        /// </summary>
        /// <param name="document">The TSV document</param>
        /// <param name="columnIndex">The column index to calculate width for</param>
        /// <param name="typeface">The font typeface used for rendering</param>
        /// <param name="fontSize">The font size used for rendering</param>
        /// <returns>The calculated column width in pixels</returns>
        double CalculateColumnWidth(TsvDocument document, int columnIndex, Typeface typeface, double fontSize);

        /// <summary>
        /// Calculates optimal widths for all columns in the document
        /// </summary>
        /// <param name="document">The TSV document</param>
        /// <param name="typeface">The font typeface used for rendering</param>
        /// <param name="fontSize">The font size used for rendering</param>
        /// <returns>Dictionary mapping column indices to their calculated widths</returns>
        Dictionary<int, double> CalculateAllColumnWidths(TsvDocument document, Typeface typeface, double fontSize);

        /// <summary>
        /// Measures the width of a single text string
        /// </summary>
        /// <param name="text">The text to measure</param>
        /// <param name="typeface">The font typeface used for rendering</param>
        /// <param name="fontSize">The font size used for rendering</param>
        /// <returns>The measured width in pixels including padding</returns>
        double MeasureTextWidth(string text, Typeface typeface, double fontSize);

        /// <summary>
        /// Gets or sets the minimum column width in pixels (default: 60px)
        /// </summary>
        double MinColumnWidth { get; set; }

        /// <summary>
        /// Gets or sets the maximum column width in pixels (default: 600px)
        /// </summary>
        double MaxColumnWidth { get; set; }

        /// <summary>
        /// Gets or sets the cell padding to add to measurements (default: 19px = 4+4+1+10)
        /// </summary>
        double CellPadding { get; set; }
    }
}
