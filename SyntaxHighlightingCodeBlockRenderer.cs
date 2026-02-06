using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ColorCode;
using ColorCode.Common;
using ColorCode.Compilation;
using ColorCode.Parsing;
using ColorCode.Styling;
using Markdig.Renderers.Wpf;
using Markdig.Syntax;
using Markdig.Wpf;

namespace TodoWpfApp
{
    public sealed class SyntaxHighlightingCodeBlockRenderer : WpfObjectRenderer<CodeBlock>
    {
        private static readonly StyleDictionary CodeStyles = StyleDictionary.DefaultLight;
        private static readonly Dictionary<string, Style> StyleLookup =
            CodeStyles.ToDictionary(style => style.ScopeName, StringComparer.OrdinalIgnoreCase);
        private static readonly ILanguageParser LanguageParser = CreateLanguageParser();

        protected override void Write(WpfRenderer renderer, CodeBlock obj)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));

            var paragraph = new Paragraph();
            paragraph.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.CodeBlockStyleKey);
            paragraph.FontFamily = new FontFamily("Consolas");

            var codeText = GetCodeText(obj);
            if (string.IsNullOrEmpty(codeText))
            {
                renderer.Push(paragraph);
                renderer.Pop();
                return;
            }

            var language = GetLanguage(obj);
            if (language == null)
            {
                paragraph.Inlines.Add(new Run(codeText));
            }
            else
            {
                foreach (var inline in BuildHighlightedInlines(codeText, language))
                {
                    paragraph.Inlines.Add(inline);
                }
            }

            renderer.Push(paragraph);
            renderer.Pop();
        }

        private static string GetCodeText(CodeBlock obj)
        {
            var builder = new StringBuilder();
            var lines = obj.Lines;

            for (int i = 0; i < lines.Count; i++)
            {
                var slice = lines.Lines[i].Slice;
                builder.Append(slice.Text, slice.Start, slice.Length);
                if (i < lines.Count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static ILanguage? GetLanguage(CodeBlock obj)
        {
            if (obj is FencedCodeBlock fenced)
            {
                var info = fenced.Info?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(info))
                {
                    var languageId = info.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0];
                    return Languages.FindById(languageId);
                }
            }

            return null;
        }

        private static IEnumerable<Inline> BuildHighlightedInlines(string code, ILanguage language)
        {
            var inlines = new List<Inline>();

            LanguageParser.Parse(code, language, (segment, scopes) =>
            {
                if (scopes == null || scopes.Count == 0)
                {
                    inlines.Add(new Run(segment));
                    return;
                }

                var insertions = new List<StyledInsertion>();
                foreach (var scope in scopes)
                {
                    CollectInsertions(scope, insertions);
                }

                insertions.Sort((left, right) =>
                {
                    var comparison = left.Index.CompareTo(right.Index);
                    return comparison != 0 ? comparison : left.Order.CompareTo(right.Order);
                });

                var stack = new Stack<Scope>();
                var offset = 0;

                foreach (var insertion in insertions)
                {
                    if (insertion.Index > offset)
                    {
                        var text = segment.Substring(offset, insertion.Index - offset);
                        inlines.Add(CreateRun(text, stack.Count > 0 ? stack.Peek() : null));
                    }

                    if (insertion.IsStart)
                    {
                        stack.Push(insertion.Scope);
                    }
                    else if (stack.Count > 0)
                    {
                        stack.Pop();
                    }

                    offset = insertion.Index;
                }

                if (offset < segment.Length)
                {
                    inlines.Add(CreateRun(segment.Substring(offset), stack.Count > 0 ? stack.Peek() : null));
                }
            });

            return inlines;
        }

        private static Run CreateRun(string text, Scope? scope)
        {
            var run = new Run(text);
            if (scope == null)
            {
                return run;
            }

            if (StyleLookup.TryGetValue(scope.Name, out var style))
            {
                if (!string.IsNullOrWhiteSpace(style.Foreground))
                {
                    run.Foreground = (Brush)new BrushConverter().ConvertFromString(style.Foreground)!;
                }

                if (!string.IsNullOrWhiteSpace(style.Background))
                {
                    run.Background = (Brush)new BrushConverter().ConvertFromString(style.Background)!;
                }
            }

            return run;
        }

        private static void CollectInsertions(Scope scope, ICollection<StyledInsertion> insertions)
        {
            insertions.Add(new StyledInsertion
            {
                Index = scope.Index,
                Scope = scope,
                IsStart = true,
                Order = insertions.Count
            });

            foreach (var child in scope.Children)
            {
                CollectInsertions(child, insertions);
            }

            insertions.Add(new StyledInsertion
            {
                Index = scope.Index + scope.Length,
                Scope = scope,
                IsStart = false,
                Order = insertions.Count
            });
        }

        private static ILanguageParser CreateLanguageParser()
        {
            var languageMap = new Dictionary<string, ILanguage>(StringComparer.OrdinalIgnoreCase);
            var repository = new LanguageRepository(languageMap);
            foreach (var language in Languages.All)
            {
                repository.Load(language);
            }

            var compiler = new LanguageCompiler(new Dictionary<string, CompiledLanguage>(), new ReaderWriterLockSlim());
            return new LanguageParser(compiler, repository);
        }

        private sealed class StyledInsertion
        {
            public int Index { get; init; }
            public bool IsStart { get; init; }
            public Scope Scope { get; init; } = null!;
            public int Order { get; init; }
        }
    }
}
