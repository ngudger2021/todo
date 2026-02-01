using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace TodoWpfApp
{
    /// <summary>
    /// Provides watermark (placeholder text) functionality for TextBox controls.
    /// Usage: &lt;TextBox local:WatermarkHelper.Watermark="Enter text here..." /&gt;
    /// </summary>
    public static class WatermarkHelper
    {
        // Attached property for watermark text
        public static readonly DependencyProperty WatermarkProperty =
            DependencyProperty.RegisterAttached(
                "Watermark",
                typeof(string),
                typeof(WatermarkHelper),
                new PropertyMetadata(string.Empty, OnWatermarkChanged));

        public static string GetWatermark(DependencyObject obj)
        {
            return (string)obj.GetValue(WatermarkProperty);
        }

        public static void SetWatermark(DependencyObject obj, string value)
        {
            obj.SetValue(WatermarkProperty, value);
        }

        private static void OnWatermarkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.Loaded -= TextBox_Loaded;
                textBox.TextChanged -= TextBox_TextChanged;
                textBox.GotFocus -= TextBox_GotFocus;
                textBox.LostFocus -= TextBox_LostFocus;

                if (!string.IsNullOrEmpty((string)e.NewValue))
                {
                    textBox.Loaded += TextBox_Loaded;
                    textBox.TextChanged += TextBox_TextChanged;
                    textBox.GotFocus += TextBox_GotFocus;
                    textBox.LostFocus += TextBox_LostFocus;
                }
            }
        }

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                ShowWatermark(textBox);
            }
        }

        private static void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    ShowWatermark(textBox);
                }
                else
                {
                    RemoveWatermark(textBox);
                }
            }
        }

        private static void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                RemoveWatermark(textBox);
            }
        }

        private static void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && string.IsNullOrEmpty(textBox.Text))
            {
                ShowWatermark(textBox);
            }
        }

        private static void ShowWatermark(TextBox textBox)
        {
            if (string.IsNullOrEmpty(textBox.Text))
            {
                var watermarkText = GetWatermark(textBox);
                if (!string.IsNullOrEmpty(watermarkText))
                {
                    // Create adorner layer for watermark
                    var adornerLayer = AdornerLayer.GetAdornerLayer(textBox);
                    if (adornerLayer != null)
                    {
                        // Remove any existing watermark adorner first
                        var adorners = adornerLayer.GetAdorners(textBox);
                        if (adorners != null)
                        {
                            foreach (var adorner in adorners)
                            {
                                if (adorner is WatermarkAdorner)
                                {
                                    adornerLayer.Remove(adorner);
                                }
                            }
                        }

                        // Add new watermark adorner
                        adornerLayer.Add(new WatermarkAdorner(textBox, watermarkText));
                    }
                }
            }
        }

        private static void RemoveWatermark(TextBox textBox)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(textBox);
            if (adornerLayer != null)
            {
                var adorners = adornerLayer.GetAdorners(textBox);
                if (adorners != null)
                {
                    foreach (var adorner in adorners)
                    {
                        if (adorner is WatermarkAdorner)
                        {
                            adornerLayer.Remove(adorner);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adorner that displays watermark text over a TextBox
        /// </summary>
        private class WatermarkAdorner : Adorner
        {
            private readonly string _watermarkText;
            private readonly TextBox _textBox;

            public WatermarkAdorner(TextBox adornedElement, string watermarkText) : base(adornedElement)
            {
                _textBox = adornedElement;
                _watermarkText = watermarkText;
                IsHitTestVisible = false; // Allow click-through to textbox
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                if (!_textBox.IsFocused && string.IsNullOrEmpty(_textBox.Text))
                {
                    var typeface = new Typeface(_textBox.FontFamily, _textBox.FontStyle, FontWeights.Normal, _textBox.FontStretch);
                    var formattedText = new FormattedText(
                        _watermarkText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        _textBox.FlowDirection,
                        typeface,
                        _textBox.FontSize,
                        Brushes.Gray,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    // Position watermark with padding consideration
                    var left = _textBox.Padding.Left + 2;
                    var top = _textBox.Padding.Top + (_textBox.ActualHeight - formattedText.Height) / 2;

                    drawingContext.DrawText(formattedText, new Point(left, top));
                }
            }
        }
    }
}
