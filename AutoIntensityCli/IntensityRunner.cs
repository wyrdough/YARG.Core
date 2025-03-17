using System.Collections.Concurrent;
using System.Diagnostics;
using YARG.Core.Chart;
using YARG.Core.Chart.AutoIntensity;
using YARG.Core.Song;
using YARG.TestConsole;

namespace AutoIntensityCli;

public class IntensityRunner
{
    private static readonly string                         SongDirsPath   = Path.Combine(Environment.CurrentDirectory, "console_songdirs.txt");
    public const            string                         CHART_PATH_VAR = "TEST_CHART_PATH";
    private                 SongCache                      _songCache;
    private                 Thread                         _chartLoadingThread;
    private                 Thread                         _chartProcessingThread;
    private                 ConcurrentQueue<SongWithChart> _loadedCharts = new();
    private StreamWriter _csvFile;

    private struct SongWithChart
    {
        public SongEntry Entry;
        public SongChart Chart;
    }

    public void IntensityTest()
    {
        ConsoleUtilities.WriteMenuHeader("Auto Intensity Test");

        string chartPath = ConsoleUtilities.PromptTextInput("Please enter a chart directory: ", (input) =>
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "Invalid input!";
            }

            if (!Directory.Exists(input))
            {
                return "Directory does not exist!";
            }

            return null;
        });

        File.WriteAllText(SongDirsPath, chartPath);
        Console.WriteLine();

        _csvFile = File.CreateText(Path.Combine(chartPath, "results-csharp.csv"));
        WriteCsvHeader(_csvFile);

        _songCache = CacheLoader.LoadCache();

        _chartLoadingThread = new Thread(LoadCharts) { IsBackground = true };
        _chartProcessingThread = new Thread(ProcessAllCharts) { IsBackground = true };
        _chartLoadingThread.Start();
        _chartProcessingThread.Start();
        _chartProcessingThread.Join();

        _csvFile.Close();
    }

    private void ProcessAllCharts()
    {
        int chartCount = 0;
        int failedCount = 0;
        var timer = new Stopwatch();
        timer.Start();

        while (_chartLoadingThread.IsAlive || !_loadedCharts.IsEmpty)
        {
            if (!_loadedCharts.TryDequeue(out var chart))
            {
                Thread.Sleep(1);
                continue;
            }

            chartCount++;
            failedCount += ProcessChart(chart.Entry, chart.Chart, _csvFile) ? 0 : 1;
        }
        timer.Stop();

        Console.WriteLine($"Processed {chartCount} charts in {timer.ElapsedMilliseconds} ms.");
        if (failedCount > 0)
        {
            Console.WriteLine($"Failed to read {failedCount} charts.");
        }


    }

    private bool ProcessChart(SongEntry cacheEntry, SongChart songChart, StreamWriter csvFile)
    {
        var artist = cacheEntry.Artist;
        var title = cacheEntry.Name;

        var chartList = Chart.ReadChart($"{artist} - {title}", songChart);

        if (chartList.Count == 0)
        {
            Console.WriteLine($"No charts (that we care about) found in chart file! ({cacheEntry.Artist} - {cacheEntry.Name})");
            return false;
        }

        var timer = new Stopwatch();

        timer.Start();
        var results = AutoIntensity.CalculateAllChartStats(chartList);
        timer.Stop();

        string artistString = '"' + cacheEntry.Artist + '"';
        string nameString = '"' + cacheEntry.Name + '"';

        string line = string.Join(",", artistString, nameString);
        foreach (var key in results.Keys)
        {
            // Console.WriteLine($"{key}: {results[key]}");
            line = string.Join(",", line, results[key]);
        }
        csvFile.WriteLine(line);

        Console.WriteLine($"Chart processing time for {cacheEntry.Artist}: {cacheEntry.Name}: {timer.ElapsedMilliseconds}ms");
        return true;
    }

    private void LoadCharts()
    {
        var loadTimer = new Stopwatch();
        foreach (var node in _songCache.Entries.Values)
        {
            foreach (var entry in node)
            {
                // loadTimer.Start();
                var songChart = entry.LoadChart();
                // loadTimer.Stop();
                // Console.WriteLine($"Loaded chart for {entry.Name} in {loadTimer.ElapsedMilliseconds}ms");
                // loadTimer.Reset();
                if (songChart == null)
                {
                    Console.WriteLine($"Unable to load chart! ({entry.Artist}: {entry.Name})");
                    continue;
                }
                _loadedCharts.Enqueue(new SongWithChart {Entry = entry, Chart = songChart});
            }
        }
    }

    private static void WriteCsvHeader(StreamWriter csvFile)
    {
        // TODO: Add rhythm later
        csvFile.WriteLine("artist,name,Guitar Easy,Guitar Medium,Guitar Hard,Guitar Expert,Bass Easy,Bass Medium,Bass Hard,Bass Expert");
    }
}