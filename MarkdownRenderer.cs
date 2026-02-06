using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdown = Markdig.Wpf.Markdown;


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
                return Markdown.ToFlowDocument(markdown, Pipeline);
            }
            catch (Exception)
            {
                doc.Blocks.Add(new Paragraph(new Run(markdown)));
                return doc;
            }
        }
    }
}
