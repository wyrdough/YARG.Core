using System;
using System.Collections.Generic;

namespace YARG.Core.Chart.AutoIntensity
{
    public class Chart
    {
        public string              Name;
        public AutoIntensity.Diff  Diff;
        public List<Chord>         Chords;
        public const int           OPEN_MASK = 64;

        public Chart(string name, AutoIntensity.Diff difficulty, List<Chord> chords)
        {
            Name = name;
            Diff = difficulty;
            // I think this should perhaps just be a new list, not passed in, but we'll see
            Chords = chords;
        }

        public static List<Chart> ReadChart(string label, SongChart chart)
        {
            List<Chart> chartList = new();

            // The idea is to take a chart, get the tempo map and stash it somewhere, then read all the
            // instrument difficulties, picking out the notes/chords, strums, hopos, taps and what would be lanes
            // if we supported them

            // We can get tempo changes and time signature changes from SyncTrack

            // Wish I could use a damn loop here, but nah, C# makes that impossible without using dynamic,
            // which everyone hates, including me
            // ReadChart(chart, chart.ProKeys);
            // ReadChart(chart, chart.Keys);
            var guitarChart = ReadChart(label, chart, chart.FiveFretGuitar);
            var bassChart = ReadChart(label, chart, chart.FiveFretBass);
            var rhythmChart = ReadChart(label, chart, chart.FiveFretRhythm);
            // ReadChart(chart, chart.FiveFretCoop);
            // ReadChart(chart, chart.FourLaneDrums);
            // ReadChart(chart, chart.FiveLaneDrums);
            // ReadChart(chart, chart.ProDrums);
            // ReadChart(chart, chart.ProGuitar_17Fret);
            // ReadChart(chart, chart.ProGuitar_22Fret);
            // ReadChart(chart, chart.ProBass_17Fret);
            // ReadChart(chart, chart.ProBass_22Fret);

            if (guitarChart != null)
            {
                chartList.AddRange(guitarChart);
            }

            if (bassChart != null)
            {
                chartList.AddRange(bassChart);
            }

            if (rhythmChart != null)
            {
                chartList.AddRange(rhythmChart);
            }

            return chartList;
        }

        private static List<Chart>? ReadChart(string label, SongChart chart, InstrumentTrack<GuitarNote> track)
        {
            if (track.IsEmpty)
            {
                return null;
            }

            var chartList = new List<Chart>();

            foreach(Difficulty difficulty in Enum.GetValues(typeof(Difficulty)))
            {
                if (difficulty is Difficulty.Beginner or Difficulty.ExpertPlus)
                {
                    continue;
                }

                if(!track.TryGetDifficulty(difficulty, out var id))
                {
                    continue;
                }

                var chords = new List<Chord>();
#pragma warning disable CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
                var thisDiff = difficulty switch
#pragma warning restore CS8509 // The switch expression does not handle all possible values of its input type (it is not exhaustive).
                {
                    Difficulty.Easy   => AutoIntensity.Diff.EASY,
                    Difficulty.Medium => AutoIntensity.Diff.MEDIUM,
                    Difficulty.Hard   => AutoIntensity.Diff.HARD,
                    Difficulty.Expert => AutoIntensity.Diff.EXPERT,
                };

                double lastTime = 0;
                foreach (var note in id.Notes)
                {
                    int forcing = 0;
                    if(note.IsHopo || note.IsTap) {
                        forcing = (note.IsHopo ? 1 : 0) + (note.IsTap ? 2 : 0);
                    }

                    var noteMask = note.NoteMask;
                    // TODO: make sure we're operating on a five fret chart before doing this
                    // TODO: Fix the AutoIntensity code to look for the open mask in bit 64
                    // The open note needs to be in bit 1 instead of 64 if it is set and everything else needs
                    // to be shifted left to match AutoIntensity's masks
                    if ((noteMask & OPEN_MASK) != 0)
                    {
                        noteMask = ((noteMask & ~OPEN_MASK) << 1) | 1;
                        // noteMask &= ~OPEN_MASK;
                        // noteMask <<= 1;
                        // noteMask |= 1;
                    }
                    else
                    {
                        noteMask <<= 1;
                    }


                    var laned = false;

                    // When purplo's work is merged, we can do this:
                    // laned = (note.IsTremolo || note.IsTrill);

                    // Last parameter (laned) is always false at the moment because YARG no support lanes
                    chords.Add(new Chord(note.Time, noteMask, (AutoIntensity.Forcing) forcing, laned));
                    // A direct translation looks like the above should use lastTime and lastTime should be
                    // set here, but testing in the debugger shows that isn't how it actually ends up working in
                    // the python. First note is _not_ 0 there.
                    lastTime = note.Time;
                }

                if (chords.Count == 0)
                {
                    continue;
                }

                var instrumentName = track.Instrument.ToString();
                var difficultyName = difficulty.ToString();
                chartList.Add(new Chart($"{label}: {instrumentName} - {difficultyName}", thisDiff, chords));
            }
            return chartList;
        }
    }
}