using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DatabaseFixer;
using YARG.Core.Logging;

namespace ChartFinder;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private List<string> _searchFolders = new();
    private SongHandler  _songHandler = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadFolders();
        _songHandler.LoadJSON();
        if (_songHandler.AllSongs.Count > 0)
        {
            StatusText.Text = $"Loaded {_songHandler.AllSongs.Count} songs from cache...";
            databasefixer_Update();
        }
        OutputGrid.ItemsSource = _songHandler.AllSongs;
        // Difficulty.ItemsSource = Enum.GetValues(typeof(Song.Difficulties));
        ProgressBar.Value = 0;
        ProgressBar.Maximum = 1;
    }

    private void MainWindow_Closed(object? sender, EventArgs eventArgs)
    {
        YargLogger.KillLogger();
    }

    private void LoadFolders()
    {
        if (File.Exists("console_songdirs.txt"))
        {
            try
            {
                _searchFolders = File.ReadAllLines("console_songdirs.txt").ToList();
                run_Update();
            }
            catch (Exception ex)
            {
                // ignored
            }
        }
    }

    private void folders_Click(object sender, RoutedEventArgs e)
    {
        var folderWindow = new FolderListWindow(_searchFolders);
        folderWindow.Owner = this;

        if (folderWindow.ShowDialog() == true)
        {
            _searchFolders = folderWindow.Folders.ToList();
            run_Update();
        }
    }

    private async void run_Click(object sender, RoutedEventArgs e)
    {
        Run.IsEnabled = false;
        Folders.IsEnabled = false;
        StatusText.Text = "Starting scan...";
        _songHandler.Songs.Clear();
        ProgressBar.Value = 0;

        var progress = new Progress<string>(status =>
        {
            StatusText.Text = status;
        });

        foreach (var completed in _songHandler.LoadCache(_searchFolders, progress))
        {
            await Task.Delay(10);
            if (completed)
            {
                break;
            }
        }

        StatusText.Text = "Parsing songs...";

        ProgressBar.Visibility = Visibility.Visible;

        var songProgress = new Progress<Tuple<int, int>>(report =>
        {
            ProgressBar.Maximum = report.Item2 > 0 ? report.Item2 : 1;
            ProgressBar.Value = report.Item1;

            StatusText.Text = $"Parsing song {report.Item1} of {report.Item2}";
        });

        bool songsFound = await Task.Run(() => _songHandler.LoadSongs(songProgress));


        // Save some JSON with all the scanned songs
        // Open a JSON file to store the list
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        StatusText.Text = "Saving song JSON...";

        await using FileStream createStream = new("allsongs.json", FileMode.Create);
        await JsonSerializer.SerializeAsync(createStream, _songHandler.AllSongs, jsonOptions);

        if (songsFound)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Song loading complete. Found {_songHandler.AllSongs.Count} songs.";
            });
        }
        else
        {
            StatusText.Text = "No songs found in specified folders...";
        }

        ProgressBar.Visibility = Visibility.Hidden;

        run_Update();
        databasefixer_Update();
        Folders.IsEnabled = true;
    }

    private void run_Update()
    {
        Run.IsEnabled = _searchFolders.Count > 0;
    }

    private void databasefixer_Update()
    {
        FixDatabase.IsEnabled = _songHandler.AllSongs.Count > 0;
    }

    private void run_Databasefixer(object sender, RoutedEventArgs e)
    {
        var databaseHandler = new DatabaseHandler(_songHandler);
        var fixedSongs = databaseHandler.FixDatabase();

        if (fixedSongs == -1)
        {
            StatusText.Text = "No songs found in specified folders...How did you even click the button?";
        }

        if (fixedSongs == 0)
        {
            StatusText.Text = "No matches found in the database...";
        }

        if (fixedSongs > 0)
        {
            StatusText.Text =
                $"Found {fixedSongs} matches in the database and updated corresponding GameRecord entries";
        }
    }

    private void difficulty_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: Song.Difficulties selectedDifficulty })
        {
            Song.SelectedDifficulty = selectedDifficulty;
            OutputGrid.Items.Refresh();
        }
    }

    private void AllSongs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            bool isChecked = checkBox.IsChecked ?? false;
            OutputGrid.ItemsSource = isChecked ? _songHandler.AllSongs : _songHandler.Songs;
            OutputGrid.Items.Refresh();
        }
    }
}