using ChartFinder;
using Microsoft.Data.Sqlite;
using YARG.Core.Song;

namespace DatabaseFixer;

public class DatabaseHandler(SongHandler songHandler)
{
    private const string      INPUT_DATABASE_PATH  = "scores.db";
    private const string      OUTPUT_DATABASE_PATH = "scores_fixed.db";
    private       SongHandler _songHandler         = songHandler;

    public int FixDatabase()
    {
        int fixedSongs = 0;
        // If AllSongs is empty, do nothing (we should probably throw an error, but I'm lazy)
        if (_songHandler.AllSongs.Count == 0)
        {
            return -1;
        }

        var allSongs = _songHandler.AllSongs;
        string inputConnectionString  = $"Data Source={INPUT_DATABASE_PATH}";
        // string outputConnectionString = $"Data Source={OUTPUT_DATABASE_PATH}";

        using var connection = new SqliteConnection(inputConnectionString);
        connection.Open();

        var gameRecordQueryString =
            "SELECT Id, SongName, SongArtist, SongCharter from GameRecords WHERE SongChecksum IS NULL";
        var gameRecordUpdateString = "UPDATE GameRecords SET SongChecksum = @songChecksum WHERE Id = @recordId";

        using (var command = new SqliteCommand(gameRecordQueryString, connection))
        {
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var Id = reader.GetInt32(0);
                    var songName = reader.GetString(1);
                    var songArtist = reader.GetString(2);
                    var songCharter = reader.GetString(3);

                    // Look for matches in _songHandler.AllSongs and construct an update query to
                    // put the matching SongChecksum in the database
                    var matchingSongs = allSongs.FindAll(s => s.Title == songName && s.Artist == songArtist && s.Charter == songCharter);
                    if (matchingSongs.Count == 0)
                    {
                        Console.WriteLine($"No matching song found for {songName} by {songArtist} ({songCharter})");
                        continue;
                    }

                    if (matchingSongs.Count > 1)
                    {
                        Console.WriteLine($"Multiple matching songs found for {songName} by {songArtist} ({songCharter})");
                        continue;
                    }
                    // At this point we know there is one and only one match
                    var matchingSong = matchingSongs[0];
                    fixedSongs++;
                    using (var updateCommand = new SqliteCommand(gameRecordUpdateString, connection))
                    {
                        // Hash may need to be turned back into a HashWrapper...we'll see...
                        var wrapper = HashWrapper.FromString(matchingSong.Hash);
                        updateCommand.Parameters.AddWithValue("@songChecksum", wrapper.HashBytes);
                        updateCommand.Parameters.AddWithValue("@recordId", Id);
                        updateCommand.ExecuteNonQuery();
                    }

                }
            }
        }

        return fixedSongs;
    }
}