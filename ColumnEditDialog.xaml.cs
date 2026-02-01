using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for ColumnEditDialog.xaml
    /// </summary>
    public partial class ColumnEditDialog : Window
    {
        public string ColumnName { get; private set; } = string.Empty;
        public string ColumnColor { get; private set; } = "#3498db";

        private class ColorOption
        {
            public string Name { get; set; } = string.Empty;
            public string HexValue { get; set; } = string.Empty;
            public SolidColorBrush Brush { get; set; } = Brushes.Gray;
        }

        public ColumnEditDialog(KanbanColumn? existingColumn)
        {
            InitializeComponent();

            // Populate color options
            var colors = new List<ColorOption>
            {
                new ColorOption { Name = "Gray", HexValue = "#95a5a6", Brush = new SolidColorBrush(Color.FromRgb(0x95, 0xa5, 0xa6)) },
                new ColorOption { Name = "Blue", HexValue = "#3498db", Brush = new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xdb)) },
                new ColorOption { Name = "Green", HexValue = "#27ae60", Brush = new SolidColorBrush(Color.FromRgb(0x27, 0xae, 0x60)) },
                new ColorOption { Name = "Orange", HexValue = "#f39c12", Brush = new SolidColorBrush(Color.FromRgb(0xf3, 0x9c, 0x12)) },
                new ColorOption { Name = "Red", HexValue = "#e74c3c", Brush = new SolidColorBrush(Color.FromRgb(0xe7, 0x4c, 0x3c)) },
                new ColorOption { Name = "Purple", HexValue = "#9b59b6", Brush = new SolidColorBrush(Color.FromRgb(0x9b, 0x59, 0xb6)) },
                new ColorOption { Name = "Teal", HexValue = "#1abc9c", Brush = new SolidColorBrush(Color.FromRgb(0x1a, 0xbc, 0x9c)) },
                new ColorOption { Name = "Yellow", HexValue = "#f1c40f", Brush = new SolidColorBrush(Color.FromRgb(0xf1, 0xc4, 0x0f)) }
            };

            ColorComboBox.ItemsSource = colors;
            ColorComboBox.SelectedIndex = 1; // Default to Blue

            // If editing existing column, populate fields
            if (existingColumn != null)
            {
                Title = "Edit Column";
                ColumnNameTextBox.Text = existingColumn.Name;

                // Find matching color
                for (int i = 0; i < colors.Count; i++)
                {
                    if (colors[i].HexValue.Equals(existingColumn.Color, System.StringComparison.OrdinalIgnoreCase))
                    {
                        ColorComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                Title = "Add Column";
            }

            ColumnNameTextBox.Focus();
        }

        private void ColorComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ColorComboBox.SelectedItem is ColorOption option)
            {
                ColumnColor = option.HexValue;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ColumnName = ColumnNameTextBox.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(ColumnName))
            {
                MessageBox.Show("Please enter a column name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ColorComboBox.SelectedItem is ColorOption option)
            {
                ColumnColor = option.HexValue;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
