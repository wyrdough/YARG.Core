using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Controls;

namespace ChartFinder;

public partial class FolderListWindow : Window
{
    public ObservableCollection<string> Folders { get; } = new();

    public FolderListWindow(IEnumerable<string>? initialFolders = null)
    {
        InitializeComponent();

        if (initialFolders != null)
        {
            foreach (var folder in initialFolders)
            {
                Folders.Add(folder);
            }
        }

        // This should never happen if the file exists since we're loading it when the app starts
        if (Folders.Count == 0)
        {
            // Attempt to load the last saved list of folders
            try
            {
                File.ReadAllLines("console_songdirs.txt").ToList().ForEach(x => Folders.Add(x));
            }
            catch (Exception ex)
            {
                // ignored
            }
        }

        FolderListView.ItemsSource = Folders;
    }

    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select a folder to add to the search list"
        };

        if (dialog.ShowDialog() == true)
        {
            if (!Folders.Contains(dialog.FolderName))
            {
                Folders.Add(dialog.FolderName);
            }
            else
            {
                MessageBox.Show("This folder is already in the list.", "Duplicate Folder",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedItem = FolderListView.SelectedItem as string;
        if (selectedItem != null)
        {
            Folders.Remove(selectedItem);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            File.WriteAllLines("console_songdirs.txt", Folders);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving folder list: {ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}