using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Mime;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Song;
using YARG.Core.Song.Cache;
using YARG.TestConsole;

namespace ChartFinder;

public class SongHandler
{
    public ObservableCollection<Song> Songs { get; set; }
    public List<Song>                 AllSongs = [];
    public Song.Difficulties          SelectedDifficulty = Song.Difficulties.Any;

    private SongCache _songCache;

    public SongHandler()
    {
        Songs = new ObservableCollection<Song>();
    }

    public void SelectDifficulty(Song.Difficulties difficulty)
    {

    }

    public void LoadJSON()
    {
        // Load allSongs from the JSON file
        if (!File.Exists("allsongs.json"))
        {
            return;
        }

        try
        {
            string jsonString = File.ReadAllText("allsongs.json");
            AllSongs = JsonSerializer.Deserialize<List<Song>>(jsonString) ?? [];

            foreach (var song in AllSongs)
            {
                if (song.HasBRE || song.HasLanes)
                {
                    Songs.Add(song);
                }
            }
        }
        catch (Exception e)
        {
            MessageBox.Show($"Error Loading JSON: {e.Message}", "Parse Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public bool LoadSongs(IProgress<Tuple<int, int>> progress)
    {
        // LoadCache should have already been called at this point
        if (_songCache == null)
        {
            return false;
        }

        // This is not exactly correct if there are duplicate charts
        int chartsToScan = _songCache.Entries.Count;
        int chartsScanned = 0;

        var allEntries = _songCache.Entries.ToList();

        foreach (var entry in allEntries)
        {
            foreach (var song in entry.Value)
            {
                SongChart? chart;
                try
                {
                    chart = song.LoadChart();
                }
                catch (Exception e)
                {
                    continue;
                }

                // We have the chart, now we have to get difficulties for the instruments we care about
                // and then check for BREs and lanes. BREs will be in every difficulty, lanes probably only in expert
                // Guess that means we should just load expert for now
                // Just to see if it works, we'll start with guitar

                bool foundPart = false;
                Song outputSong = new Song(song.Artist, song.Name, song.Source.Original, song.Charter.Original, $"{song.Hash}", song.Hash);


                // This one contains all the charts we scanned, not just the ones with BREs or lanes
                AllSongs.Add(outputSong);

                chartsScanned++;
                progress?.Report(new Tuple<int, int>(chartsScanned, chartsToScan));
            }
        }

        return AllSongs.Count > 0;
    }

    public IEnumerable<bool> LoadCache(List<string> folders, IProgress<string> progress)
    {
        var timer = Stopwatch.StartNew();
        progress?.Report("Building song cache...");
        var task = Task.Run(() => CacheHandler.RunScan(
            tryQuickScan: false,
            "songcache_chartfinder.bin",
            "badsongs_chartfinder.txt",
            false,
            folders
        ));

        int dotCount = 0;
        while (!task.IsCompleted)
        {
            if (timer.ElapsedMilliseconds > 125)
            {
                timer.Restart();
                // Write progress to the status bar
                dotCount = (dotCount + 1) % 4;
                string dots = new('.', dotCount);
                progress?.Report($"Building song cache{dots}");
            }

            yield return false;
        }

        _songCache = task.Result;

        yield return true;
    }
}