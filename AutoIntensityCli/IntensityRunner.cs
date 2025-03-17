using System.Collections.Concurrent;
using System.Diagnostics;
using YARG.Core.Chart;
using YARG.Core.Chart.AutoIntensity;
using YARG.Core.Song;
using YARG.TestConsole;

namespace AutoIntensityCli;

public class IntensityRunner
{
    private static readonly int                            NumThreads              = (int) Math.Floor(Environment.ProcessorCount / 1.5);
    private static readonly string                         SongDirsPath            = Path.Combine(Environment.CurrentDirectory, "console_songdirs.txt");
    private                 SongCache                      _songCache              = null!;
    private readonly        Thread[]                       _chartLoadingThreads    = new Thread[NumThreads];
    private readonly        Thread[]                       _chartProcessingThreads = new Thread[NumThreads / 2];
    // We aren't currently using multiple queues, but this makes it easy for testing
    private readonly        ConcurrentQueue<SongWithChart>[] _loadedCharts         = new ConcurrentQueue<SongWithChart>[1];
    private readonly        int[]                          _processedCharts        = new int[NumThreads / 2];
    private                 StreamWriter                   _csvFile                = null!;

    private readonly Dictionary<HashWrapper, List<SongEntry>>[] _songEntries = new Dictionary<HashWrapper,List<SongEntry>>[NumThreads];

    private struct SongWithChart
    {
        public SongEntry Entry;
        public SongChart Chart;
    }

    public void IntensityTest()
    {
        ConsoleUtilities.WriteMenuHeader($"Auto Intensity Test: {NumThreads + NumThreads * 0.5} Threads");

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
        var allEntries = _songCache.Entries.ToList();

        for (var i = 0; i < _loadedCharts.Length; i++)
        {
            _loadedCharts[i] = new ConcurrentQueue<SongWithChart>();
        }

        for (var i = 0; i < NumThreads; i++)
        {
            _songEntries[i] = new Dictionary<HashWrapper, List<SongEntry>>();
        }

        for (var i = 0; i < allEntries.Count; i++)
        {
            _songEntries[i % NumThreads].Add(allEntries[i].Key, allEntries[i].Value);
        }

        for (var i = 0; i < _chartLoadingThreads.Length; i++)
        {
            var x = i;
            _chartLoadingThreads[i] = new Thread(() => LoadCharts(x))
            {
                IsBackground = true,
                Name = $"Chart Loader {x + 1}"
            };
            _chartLoadingThreads[i].Start();
        }

        for (var i = 0; i < _chartProcessingThreads.Length; i++)
        {
            var x = i;
            _chartProcessingThreads[i] = new Thread(() => ProcessAllCharts(x)) { IsBackground = true, Name = $"Chart Processing Thread {x + 1}" };
            _chartProcessingThreads[i].Start();
        }

        var timer = new Stopwatch();
        timer.Start();

        var sequence = @"/-\|";
        var counter = 0;
        Console.Write("Processing charts: ");
        // The chart processing threads won't end until the loading threads end, so we can just wait for them
        while (_chartProcessingThreads.Any(x => x.IsAlive))
        {
            var symbol = sequence[counter % 4];
            Console.Write(symbol);
            Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
            counter++;
            Thread.Yield();
        }

        timer.Stop();
        var totalProcessed = _processedCharts.Sum();
        Console.WriteLine($"Total charts processed: {totalProcessed} in {timer.ElapsedMilliseconds}ms");

        _csvFile.Close();
    }

    private bool ThreadsAreAlive => (_chartLoadingThreads.Any(x => x.IsAlive));

    private void ProcessAllCharts(int threadNumber)
    {
        int queueNum = threadNumber % _loadedCharts.Length;
        int chartCount = 0;
        int failedCount = 0;
        var timer = new Stopwatch();
        timer.Start();

        while (ThreadsAreAlive || !_loadedCharts[queueNum].IsEmpty)
        {
            if (!_loadedCharts[queueNum].TryDequeue(out var chart))
            {
                Thread.Sleep(1);
                continue;
            }

            chartCount++;
            failedCount += ProcessChart(chart.Entry, chart.Chart, _csvFile) ? 0 : 1;
        }
        timer.Stop();

        _processedCharts[threadNumber] = chartCount;

        // Console.WriteLine($"Processed {chartCount} charts in {timer.ElapsedMilliseconds} ms.");
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

        // Console.WriteLine($"Chart processing time for {cacheEntry.Artist}: {cacheEntry.Name}: {timer.ElapsedMilliseconds}ms");
        return true;
    }

    private void LoadCharts(int threadNumber)
    {
        var queueNum = threadNumber % _loadedCharts.Length;
        var loadTimer = new Stopwatch();
        foreach (var node in _songEntries[threadNumber].Values)
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
                _loadedCharts[queueNum].Enqueue(new SongWithChart {Entry = entry, Chart = songChart});
            }
        }
    }

    private static void WriteCsvHeader(StreamWriter csvFile)
    {
        // TODO: Add rhythm later
        csvFile.WriteLine("artist,name,Guitar Easy,Guitar Medium,Guitar Hard,Guitar Expert,Bass Easy,Bass Medium,Bass Hard,Bass Expert");
    }
}