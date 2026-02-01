using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TodoWpfApp.Models;

namespace TodoWpfApp
{
    /// <summary>
    /// Interaction logic for GeneralNotesWindow.xaml
    /// </summary>
    public partial class GeneralNotesWindow : Window
    {
        private readonly ObservableCollection<GeneralNote> _notes;
        private readonly Action _onDataChanged;
        private GeneralNote? _currentNote;
        private bool _isUpdating;

        public GeneralNotesWindow(ObservableCollection<GeneralNote> notes, Action onDataChanged)
        {
            InitializeComponent();
            _notes = notes;
            _onDataChanged = onDataChanged;

            NotesListBox.ItemsSource = _notes;

            if (_notes.Count > 0)
            {
                NotesListBox.SelectedIndex = 0;
            }
            else
            {
                ClearEditor();
            }
        }

        private void AddNoteButton_Click(object sender, RoutedEventArgs e)
        {
            var newNote = new GeneralNote
            {
                Title = "New Note",
                Content = string.Empty,
                ContentIsMarkdown = true,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };

            _notes.Add(newNote);
            NotesListBox.SelectedItem = newNote;
            _onDataChanged?.Invoke();

            TitleTextBox.Focus();
            TitleTextBox.SelectAll();
        }

        private void DeleteNoteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentNote == null)
            {
                MessageBox.Show("Please select a note to delete.", "No Note Selected", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Delete note '{_currentNote.Title}'? This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _notes.Remove(_currentNote);
                _currentNote = null;
                _onDataChanged?.Invoke();

                if (_notes.Count > 0)
                {
                    NotesListBox.SelectedIndex = 0;
                }
                else
                {
                    ClearEditor();
                }
            }
        }

        private void NotesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NotesListBox.SelectedItem is GeneralNote note)
            {
                LoadNoteIntoEditor(note);
            }
        }

        private void LoadNoteIntoEditor(GeneralNote note)
        {
            _isUpdating = true;
            _currentNote = note;

            TitleTextBox.Text = note.Title;
            ContentTextBox.Text = note.Content;
            ContentMarkdownCheckBox.IsChecked = note.ContentIsMarkdown;
            UpdateStatus();
            UpdateMarkdownPreview();

            _isUpdating = false;
        }

        private void ClearEditor()
        {
            _isUpdating = true;
            _currentNote = null;

            TitleTextBox.Text = string.Empty;
            ContentTextBox.Text = string.Empty;
            ContentMarkdownCheckBox.IsChecked = false;
            TitleTextBox.IsEnabled = false;
            ContentTextBox.IsEnabled = false;
            ContentMarkdownCheckBox.IsEnabled = false;
            ContentMarkdownPreview.Visibility = Visibility.Collapsed;
            StatusTextBlock.Text = "No note selected. Click '+ New Note' to create one.";

            _isUpdating = false;
        }

        private void TitleTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _currentNote == null)
                return;

            _currentNote.Title = TitleTextBox.Text;
            _onDataChanged?.Invoke();
            NotesListBox.Items.Refresh();
            UpdateStatus();
        }

        private void ContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _currentNote == null)
                return;

            _currentNote.Content = ContentTextBox.Text;
            _onDataChanged?.Invoke();
            UpdateStatus();
            UpdateMarkdownPreview();
        }

        private void UpdateStatus()
        {
            if (_currentNote != null)
            {
                TitleTextBox.IsEnabled = true;
                ContentTextBox.IsEnabled = true;
                ContentMarkdownCheckBox.IsEnabled = true;
                StatusTextBlock.Text = $"Created: {_currentNote.CreatedAt:g} | Modified: {_currentNote.ModifiedAt:g}";
            }
        }

        private void ContentMarkdownCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentNote == null)
                return;

            _currentNote.ContentIsMarkdown = ContentMarkdownCheckBox.IsChecked == true;
            _onDataChanged?.Invoke();
            UpdateMarkdownPreview();
        }

        private void UpdateMarkdownPreview()
        {
            if (_currentNote == null)
                return;

            bool isMarkdown = ContentMarkdownCheckBox.IsChecked == true;
            ContentMarkdownPreview.Visibility = isMarkdown ? Visibility.Visible : Visibility.Collapsed;
            if (isMarkdown)
            {
                ContentMarkdownPreview.Document = MarkdownRenderer.ToFlowDocument(ContentTextBox.Text);
            }
        }
    }
}
