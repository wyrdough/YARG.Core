using System.Text.Json.Serialization;
using YARG.Core;

namespace ChartFinder;

public class Song
{
    public string Artist { get; set; }
    public string Title  { get; set; }
    // public string Folder;
    // public string File;
    // public string Chart;
    public bool   HasBRE         { get; set; }
    [JsonIgnore]
    public bool   HasGuitarLane  => SelectedDifficulty == Difficulties.Any ? GuitarLanes.Any(x => x) : GuitarLanes[(int)SelectedDifficulty];
    [JsonIgnore]
    public bool   HasBassLane    => SelectedDifficulty == Difficulties.Any ? BassLanes.Any(x => x) : BassLanes[(int)SelectedDifficulty];
    [JsonIgnore]
    public bool   HasDrumsLane   => SelectedDifficulty == Difficulties.Any ? DrumsLanes.Any(x => x) : DrumsLanes[(int)SelectedDifficulty];
    [JsonIgnore]
    public bool   HasKeysLane    => SelectedDifficulty == Difficulties.Any ? KeysLanes.Any(x => x) : KeysLanes[(int)SelectedDifficulty];
    [JsonIgnore]
    public bool   HasProKeysLane => SelectedDifficulty == Difficulties.Any ? ProKeysLanes.Any(x => x) : ProKeysLanes[(int)SelectedDifficulty];
    public string Hash         { get; set; }
    public string Source       { get; set; }
    public bool[] GuitarLanes  { get; set; } = [];
    public bool[] BassLanes    { get; set; } = [];
    public bool[] DrumsLanes   { get; set; } = [];
    public bool[] KeysLanes    { get; set; } = [];
    public bool[] ProKeysLanes { get; set; } = [];

    [JsonIgnore]
    public bool HasLanes => HasGuitarLane || HasBassLane || HasDrumsLane || HasKeysLane || HasProKeysLane;

    [JsonIgnore]
    public static Difficulties SelectedDifficulty { get; set; } = Difficulties.Any;

    public enum Difficulties
    {
        Any,
        Easy,
        Medium,
        Hard,
        Expert
    }

    public Song(string artist, string title, string source, string hash)
    {
        Artist = artist;
        Title = title;
        Source = source;
        Hash = hash;

        int diffs = Enum.GetValues(typeof(Difficulties)).Length;

        GuitarLanes = new bool[diffs];
        BassLanes = new bool[diffs];
        DrumsLanes = new bool[diffs];
        KeysLanes = new bool[diffs];
        ProKeysLanes = new bool[diffs];
    }

    public Song()
    {

    }
}