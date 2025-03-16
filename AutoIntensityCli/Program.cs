using System.Diagnostics;
using YARG.Core.Chart.AutoIntensity;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.TestConsole;

namespace AutoIntensityCli
{
    public static class Program
    {
        public const            string  CHART_PATH_VAR = "TEST_CHART_PATH";
        private static readonly string  SongDirsPath   = Path.Combine(Environment.CurrentDirectory, "console_songdirs.txt");

        public static void Main()
        {
            IntensityTest();
            // ConsoleUtilities.WaitForKey("Press any key to exit...");
            YargLogger.KillLogger();
        }

        private static void IntensityTest()
        {
            ConsoleUtilities.WriteMenuHeader("Auto Intensity Test");
            // TODO: Make this use the song cache, there's a CLI program that does that somewhere
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

            var csvFile = File.CreateText(Path.Combine(chartPath, "results-csharp.csv"));
            WriteCsvHeader(csvFile);

            var cache = CacheLoader.LoadCache();
            int chartCount = 0;
            int failedCount = 0;
            var timer = new Stopwatch();
            timer.Start();
            foreach (var node in cache.Entries.Values)
            {
                foreach (var entry in node)
                {
                    failedCount += ProcessChart(entry, csvFile) ? 0 : 1;
                    chartCount++;
                }
            }

            csvFile.Close();

            timer.Stop();

            Console.WriteLine($"Processed {chartCount} charts in {timer.ElapsedMilliseconds} ms.");
            // if (failedCount > 0)
            // {
            //     Console.WriteLine($"Failed loading charts: {failedCount} failed.");
            // }
            // TODO: Write out a CSV
        }

        private static bool ProcessChart(SongEntry cacheEntry, StreamWriter csvFile)
        {
            var artist = cacheEntry.Artist;
            var title = cacheEntry.Name;
            var songChart = cacheEntry.LoadChart();

            if (songChart == null)
            {
                Console.WriteLine("Unable to load chart");
                return false;
            }
            var chartList = Chart.ReadChart($"{artist} - {title}", songChart);

            if (chartList.Count == 0)
            {
                Console.WriteLine($"No charts found in chart file! ({cacheEntry.Artist} - {cacheEntry.Name})");
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

        private static void WriteCsvHeader(StreamWriter csvFile)
        {
            // TODO: Add rhythm later
            csvFile.WriteLine("artist,name,Guitar Easy,Guitar Medium,Guitar Hard,Guitar Expert,Bass Easy,Bass Medium,Bass Hard,Bass Expert");
        }
    }
}