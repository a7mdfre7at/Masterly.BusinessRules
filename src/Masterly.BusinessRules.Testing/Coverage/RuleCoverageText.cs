using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Masterly.BusinessRules.Testing
{
    /// <summary>
    /// The constants and formatting behind <see cref="RuleCoverageReport.ToText"/> and the coverage exception messages.
    /// </summary>
    internal static class RuleCoverageText
    {
        public const string CoverageOperator = "COVERAGE";
        public const string Covered = "✔";
        public const string Uncovered = "✘";
        public const string BrokenColumn = "broken ";
        public const string PassedColumn = "passed ";
        public const string ColumnSeparator = "  ";
        public const string AnnotationSeparator = "   <- ";
        public const string NeverPassedAnnotation = "never exercised on the happy path";
        public const string NeverBrokenAnnotation = "never exercised on the broken path";
        public const string NeverEvaluatedAnnotation = "never evaluated";
        public const string EmptyReport = "No rules were wrapped with coverage.";
        public const string NegativeCount = "A count cannot be negative.";
        public const string NullEntry = "The entries contain a null entry.";
        public const string MinimumOutOfRange = "The minimum outcome coverage must be between 0.0 and 1.0.";
        public const string PercentFormat = "0.#";
        private const string ListSeparator = ", ";

        public static string DuplicateCode(string code)
        {
            return $"The entries contain the code '{code}' more than once.";
        }

        public static string Percent(double share)
        {
            return $"{(share * 100.0).ToString(PercentFormat, CultureInfo.InvariantCulture)}%";
        }

        public static string Summary(RuleCoverageReport report)
        {
            return $"outcome coverage {Percent(report.OutcomeCoverage)} ({report.CoveredOutcomes} of {report.TotalOutcomes} outcomes, {report.FullyCoveredCount} of {report.RuleCount} rules fully covered)";
        }

        public static string FormatEntry(RuleCoverageEntry entry, int codeWidth)
        {
            StringBuilder line = new StringBuilder();
            line.Append(entry.Code.PadRight(codeWidth));
            line.Append(ColumnSeparator).Append(BrokenColumn).Append(entry.SeenBroken ? Covered : Uncovered);
            line.Append(ColumnSeparator).Append(PassedColumn).Append(entry.SeenPassed ? Covered : Uncovered);

            string annotation = Annotation(entry);
            if (annotation.Length > 0)
            {
                line.Append(AnnotationSeparator).Append(annotation);
            }

            return line.ToString();
        }

        public static string FormatReport(RuleCoverageReport report)
        {
            if (report.RuleCount == 0)
            {
                return EmptyReport;
            }

            int codeWidth = 0;
            foreach (RuleCoverageEntry entry in report.Entries)
            {
                codeWidth = Math.Max(codeWidth, entry.Code.Length);
            }

            List<string> lines = new List<string>();
            foreach (RuleCoverageEntry entry in report.Entries)
            {
                lines.Add(FormatEntry(entry, codeWidth));
            }

            lines.Add(Summary(report));
            return string.Join(Environment.NewLine, lines);
        }

        public static string BelowMinimum(RuleCoverageReport report, double minimum)
        {
            StringBuilder message = new StringBuilder();
            message.Append($"Outcome coverage {Percent(report.OutcomeCoverage)} is below the required {Percent(minimum)}.");

            if (report.RuleCount == 0)
            {
                message.Append(' ').Append(EmptyReport);
                return message.ToString();
            }

            AppendCodes(message, "Never broken", report.NeverBroken);
            AppendCodes(message, "Never passed", report.NeverPassed);
            AppendCodes(message, "Never evaluated", report.NeverEvaluated);
            message.Append(Environment.NewLine).Append(FormatReport(report));
            return message.ToString();
        }

        private static string Annotation(RuleCoverageEntry entry)
        {
            if (!entry.WasEvaluated)
            {
                return NeverEvaluatedAnnotation;
            }

            if (!entry.SeenPassed)
            {
                return NeverPassedAnnotation;
            }

            if (!entry.SeenBroken)
            {
                return NeverBrokenAnnotation;
            }

            return string.Empty;
        }

        private static void AppendCodes(StringBuilder message, string label, IReadOnlyList<string> codes)
        {
            if (codes.Count > 0)
            {
                message.Append(' ').Append(label).Append(": ").Append(string.Join(ListSeparator, codes)).Append('.');
            }
        }
    }
}
