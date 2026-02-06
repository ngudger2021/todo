using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Wpf;


namespace TodoWpfApp
{
    public static class MarkdownRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

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

            try
            {
                var parsed = Markdig.Markdown.Parse(markdown, Pipeline);
                var renderer = new WpfRenderer(doc);
                renderer.ObjectRenderers.RemoveAll<CodeBlockRenderer>();
                renderer.ObjectRenderers.Add(new SyntaxHighlightingCodeBlockRenderer());
                renderer.Render(parsed);
                AttachHyperlinkHandlers(doc);
                return doc;
            }
            catch (Exception)
            {
                doc.Blocks.Add(new Paragraph(new Run(markdown)));
                return doc;
            }
        }

        private static void AttachHyperlinkHandlers(FlowDocument document)
        {
            foreach (var hyperlink in FindHyperlinks(document))
            {
                hyperlink.Cursor = Cursors.Hand;
                hyperlink.RequestNavigate -= Hyperlink_RequestNavigate;
                hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
            }
        }

        private static void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch
            {
                // Ignore navigation failures; the app should remain responsive.
            }
        }

        private static IEnumerable<Hyperlink> FindHyperlinks(FlowDocument document)
        {
            foreach (var block in document.Blocks)
            {
                foreach (var hyperlink in FindHyperlinks(block))
                {
                    yield return hyperlink;
                }
            }
        }

        private static IEnumerable<Hyperlink> FindHyperlinks(Block block)
        {
            switch (block)
            {
                case Paragraph paragraph:
                    foreach (var hyperlink in FindHyperlinks(paragraph.Inlines))
                    {
                        yield return hyperlink;
                    }
                    break;
                case Section section:
                    foreach (var inner in section.Blocks)
                    {
                        foreach (var hyperlink in FindHyperlinks(inner))
                        {
                            yield return hyperlink;
                        }
                    }
                    break;
                case List list:
                    foreach (var item in list.ListItems)
                    {
                        foreach (var inner in item.Blocks)
                        {
                            foreach (var hyperlink in FindHyperlinks(inner))
                            {
                                yield return hyperlink;
                            }
                        }
                    }
                    break;
                case Table table:
                    foreach (var rowGroup in table.RowGroups)
                    {
                        foreach (var row in rowGroup.Rows)
                        {
                            foreach (var cell in row.Cells)
                            {
                                foreach (var inner in cell.Blocks)
                                {
                                    foreach (var hyperlink in FindHyperlinks(inner))
                                    {
                                        yield return hyperlink;
                                    }
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private static IEnumerable<Hyperlink> FindHyperlinks(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case Hyperlink hyperlink:
                        yield return hyperlink;
                        foreach (var nested in FindHyperlinks(hyperlink.Inlines))
                        {
                            yield return nested;
                        }
                        break;
                    case Span span:
                        foreach (var nested in FindHyperlinks(span.Inlines))
                        {
                            yield return nested;
                        }
                        break;
                }
            }
        }
    }
}
