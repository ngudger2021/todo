using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TodoWpfApp
{
    public static class MarkdownRenderer
    {
        private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"\*(.+?)\*", RegexOptions.Compiled);
        private static readonly Regex CodeRegex = new(@"`(.+?)`", RegexOptions.Compiled);

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

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    currentList = null;
                    doc.Blocks.Add(new Paragraph(new Run("")));
                    continue;
                }

                if (IsListItem(line, out var listText))
                {
                    currentList ??= new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(12, 0, 0, 0) };
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
                    var header = line.TrimStart('#').Trim();
                    var headerPara = new Paragraph(BuildInlineRuns(header))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 16,
                        Margin = new Thickness(0, 6, 0, 6)
                    };
                    doc.Blocks.Add(headerPara);
                    continue;
                }

                var paragraph = new Paragraph(BuildInlineRuns(line)) { Margin = new Thickness(0, 2, 0, 2) };
                doc.Blocks.Add(paragraph);
            }

            return doc;
        }

        private static bool IsListItem(string line, out string text)
        {
            if (line.StartsWith("- ") || line.StartsWith("* ") || line.StartsWith("+ "))
            {
                text = line.Substring(2).Trim();
                return true;
            }

            text = string.Empty;
            return false;
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
            tokens = ApplyRegex(tokens, BoldRegex, MarkdownTokenType.Bold);
            tokens = ApplyRegex(tokens, CodeRegex, MarkdownTokenType.Code);
            tokens = ApplyRegex(tokens, ItalicRegex, MarkdownTokenType.Italic);
            return tokens;
        }

        private static List<MarkdownToken> ApplyRegex(List<MarkdownToken> input, Regex regex, MarkdownTokenType type)
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

                    output.Add(new MarkdownToken(type, match.Groups[1].Value));
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
            Code
        }

        private readonly record struct MarkdownToken(MarkdownTokenType Type, string Value);
    }
}
