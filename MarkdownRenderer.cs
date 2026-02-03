using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TodoWpfApp
{
    public static class MarkdownRenderer
    {
        private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"\*(.+?)\*", RegexOptions.Compiled);
        private static readonly Regex CodeRegex = new(@"`(.+?)`", RegexOptions.Compiled);
        private static readonly Regex StrikeRegex = new(@"~~(.+?)~~", RegexOptions.Compiled);
        private static readonly Regex LinkRegex = new(@"\[(.+?)\]\((.+?)\)", RegexOptions.Compiled);

        public static FlowDocument ToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };

            if (string.IsNullOrWhiteSpace(markdown))
            {
                doc.Blocks.Add(new Paragraph(new Run("No note")));
                return doc;
            }

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var currentList = default(List);
            var currentListOrdered = false;
            var codeBlockLines = new List<string>();
            var inCodeBlock = false;

            for (var index = 0; index < lines.Length; index++)
            {
                var rawLine = lines[index];
                var line = rawLine.TrimEnd();

                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCodeBlock)
                    {
                        doc.Blocks.Add(BuildCodeBlock(codeBlockLines));
                        codeBlockLines.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                    }
                    currentList = null;
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlockLines.Add(rawLine);
                    continue;
                }

                if (IsTableHeader(line, index < lines.Length - 1 ? lines[index + 1] : null))
                {
                    var tableLines = new List<string> { line };
                    index++;
                    while (index < lines.Length)
                    {
                        var tableLine = lines[index].TrimEnd();
                        if (string.IsNullOrWhiteSpace(tableLine))
                        {
                            break;
                        }

                        if (!tableLine.Contains('|'))
                        {
                            break;
                        }

                        tableLines.Add(tableLine);
                        index++;
                    }

                    index--;
                    doc.Blocks.Add(BuildTable(tableLines));
                    currentList = null;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(line))
                {
                    currentList = null;
                    doc.Blocks.Add(new Paragraph(new Run("")));
                    continue;
                }

                if (IsHorizontalRule(line))
                {
                    currentList = null;
                    doc.Blocks.Add(BuildHorizontalRule());
                    continue;
                }

                if (IsListItem(line, out var listText, out var isOrdered))
                {
                    if (currentList == null || currentListOrdered != isOrdered)
                    {
                        currentList = new List
                        {
                            MarkerStyle = isOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                            Margin = new Thickness(12, 0, 0, 0)
                        };
                        currentListOrdered = isOrdered;
                    }
                    currentList.ListItems.Add(new ListItem(new Paragraph(BuildInlineRuns(listText))));
                    if (!doc.Blocks.Contains(currentList))
                    {
                        doc.Blocks.Add(currentList);
                    }
                    continue;
                }

                currentList = null;

                if (line.StartsWith("#"))
                {
                    var headerLevel = line.TakeWhile(ch => ch == '#').Count();
                    var header = line.TrimStart('#').Trim();
                    var headerPara = new Paragraph(BuildInlineRuns(header))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = HeaderFontSize(headerLevel),
                        Margin = new Thickness(0, 6, 0, 6)
                    };
                    doc.Blocks.Add(headerPara);
                    continue;
                }

                if (line.StartsWith(">", StringComparison.Ordinal))
                {
                    var quoteText = line.TrimStart('>').Trim();
                    var quoteParagraph = new Paragraph(BuildInlineRuns(quoteText))
                    {
                        Margin = new Thickness(12, 2, 0, 2),
                        Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 96))
                    };
                    doc.Blocks.Add(quoteParagraph);
                    continue;
                }

                var paragraph = new Paragraph(BuildInlineRuns(line)) { Margin = new Thickness(0, 2, 0, 2) };
                doc.Blocks.Add(paragraph);
            }

            return doc;
        }

        private static bool IsListItem(string line, out string text, out bool isOrdered)
        {
            if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                text = line.Substring(2).Trim();
                isOrdered = false;
                return true;
            }

            var orderedMatch = Regex.Match(line, @"^(\d+)\.\s+(.+)$");
            if (orderedMatch.Success)
            {
                text = orderedMatch.Groups[2].Value.Trim();
                isOrdered = true;
                return true;
            }

            text = string.Empty;
            isOrdered = false;
            return false;
        }

        private static bool IsHorizontalRule(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 3)
            {
                return false;
            }

            return trimmed.All(ch => ch == '-' || ch == '_' || ch == '*');
        }

        private static bool IsTableHeader(string line, string? nextLine)
        {
            if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(nextLine))
            {
                return false;
            }

            if (!line.Contains('|'))
            {
                return false;
            }

            var separator = nextLine.Trim();
            if (!separator.Contains('|'))
            {
                return false;
            }

            return separator.All(ch => ch == '|' || ch == '-' || ch == ':' || char.IsWhiteSpace(ch));
        }

        private static Block BuildHorizontalRule()
        {
            var rectangle = new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(0, 6, 0, 6)
            };

            return new BlockUIContainer(rectangle);
        }

        private static Block BuildCodeBlock(IEnumerable<string> codeLines)
        {
            var paragraph = new Paragraph(new Run(string.Join("\n", codeLines)))
            {
                FontFamily = new FontFamily("Consolas"),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Margin = new Thickness(0, 6, 0, 6)
            };

            return paragraph;
        }

        private static Block BuildTable(IReadOnlyList<string> tableLines)
        {
            var headerCells = SplitTableRow(tableLines[0]);
            var table = new Table { CellSpacing = 1 };
            foreach (var _ in headerCells)
            {
                table.Columns.Add(new TableColumn());
            }

            var group = new TableRowGroup();
            var headerRow = new TableRow();
            foreach (var cell in headerCells)
            {
                headerRow.Cells.Add(BuildTableCell(cell, true));
            }
            group.Rows.Add(headerRow);

            for (var i = 2; i < tableLines.Count; i++)
            {
                var rowCells = SplitTableRow(tableLines[i]);
                var row = new TableRow();
                for (var cellIndex = 0; cellIndex < headerCells.Count; cellIndex++)
                {
                    var cellText = cellIndex < rowCells.Count ? rowCells[cellIndex] : string.Empty;
                    row.Cells.Add(BuildTableCell(cellText, false));
                }
                group.Rows.Add(row);
            }

            table.RowGroups.Add(group);
            return table;
        }

        private static TableCell BuildTableCell(string text, bool isHeader)
        {
            var paragraph = new Paragraph(BuildInlineRuns(text))
            {
                Margin = new Thickness(4, 2, 4, 2)
            };

            if (isHeader)
            {
                paragraph.FontWeight = FontWeights.SemiBold;
                paragraph.Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            }

            return new TableCell(paragraph)
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0.5)
            };
        }

        private static List<string> SplitTableRow(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("|", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(1);
            }

            if (trimmed.EndsWith("|", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 1);
            }

            return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
        }

        private static double HeaderFontSize(int level)
        {
            return level switch
            {
                <= 1 => 18,
                2 => 16,
                3 => 14,
                _ => 12
            };
        }

        private static Inline BuildInlineRuns(string text)
        {
            var span = new Span();

            var tokens = SplitIntoTokens(text);
            foreach (var token in tokens)
            {
                if (token.Type == MarkdownTokenType.Bold)
                {
                    span.Inlines.Add(new Bold(new Run(token.Value)));
                }
                else if (token.Type == MarkdownTokenType.Italic)
                {
                    span.Inlines.Add(new Italic(new Run(token.Value)));
                }
                else if (token.Type == MarkdownTokenType.Code)
                {
                    span.Inlines.Add(new Run(token.Value)
                    {
                        FontFamily = new FontFamily("Consolas"),
                        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240))
                    });
                }
                else if (token.Type == MarkdownTokenType.Strike)
                {
                    var run = new Run(token.Value) { TextDecorations = TextDecorations.Strikethrough };
                    span.Inlines.Add(run);
                }
                else if (token.Type == MarkdownTokenType.Link)
                {
                    var hyperlink = new Hyperlink(new Run(token.Value));
                    if (Uri.TryCreate(token.Extra, UriKind.Absolute, out var uri))
                    {
                        hyperlink.NavigateUri = uri;
                        hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(0, 102, 204));
                        hyperlink.TextDecorations = TextDecorations.Underline;
                    }
                    span.Inlines.Add(hyperlink);
                }
                else
                {
                    span.Inlines.Add(new Run(token.Value));
                }
            }

            return span;
        }

        private static List<MarkdownToken> SplitIntoTokens(string text)
        {
            var tokens = new List<MarkdownToken> { new(MarkdownTokenType.Text, text) };
            tokens = ApplyRegex(tokens, CodeRegex, MarkdownTokenType.Code);
            tokens = ApplyRegex(tokens, LinkRegex, MarkdownTokenType.Link, useExtraGroup: true);
            tokens = ApplyRegex(tokens, BoldRegex, MarkdownTokenType.Bold);
            tokens = ApplyRegex(tokens, ItalicRegex, MarkdownTokenType.Italic);
            tokens = ApplyRegex(tokens, StrikeRegex, MarkdownTokenType.Strike);
            return tokens;
        }

        private static List<MarkdownToken> ApplyRegex(List<MarkdownToken> input, Regex regex, MarkdownTokenType type, bool useExtraGroup = false)
        {
            var output = new List<MarkdownToken>();
            foreach (var token in input)
            {
                if (token.Type != MarkdownTokenType.Text)
                {
                    output.Add(token);
                    continue;
                }

                var remaining = token.Value;
                while (true)
                {
                    var match = regex.Match(remaining);
                    if (!match.Success)
                    {
                        if (!string.IsNullOrEmpty(remaining))
                        {
                            output.Add(new MarkdownToken(MarkdownTokenType.Text, remaining));
                        }
                        break;
                    }

                    if (match.Index > 0)
                    {
                        output.Add(new MarkdownToken(MarkdownTokenType.Text, remaining.Substring(0, match.Index)));
                    }

                    output.Add(useExtraGroup
                        ? new MarkdownToken(type, match.Groups[1].Value, match.Groups[2].Value)
                        : new MarkdownToken(type, match.Groups[1].Value));
                    remaining = remaining.Substring(match.Index + match.Length);
                }
            }

            return output;
        }

        private enum MarkdownTokenType
        {
            Text,
            Bold,
            Italic,
            Code,
            Strike,
            Link
        }

        private readonly record struct MarkdownToken(MarkdownTokenType Type, string Value, string? Extra = null);
    }
}
