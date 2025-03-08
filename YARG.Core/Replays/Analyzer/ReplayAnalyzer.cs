﻿using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Game;
using YARG.Core.Logging;

namespace YARG.Core.Replays.Analyzer
{
    public class ReplayAnalyzer
    {
        private readonly SongChart  _chart;
        private readonly ReplayData _replay;

        private          double _fps;
        private readonly bool   _doFrameUpdates;

        private readonly Random _random = new();

        public ReplayAnalyzer(SongChart chart, ReplayData replay, double fps)
        {
            _chart = chart;
            _replay = replay;

            _fps = fps;
            _doFrameUpdates = _fps > 0;
        }

        public static AnalysisResult[] AnalyzeReplay(SongChart chart, ReplayData replay, double fps = 0)
        {
            var analyzer = new ReplayAnalyzer(chart, replay, fps);
            return analyzer.Analyze();
        }

        public static string PrintStatDifferences(BaseStats originalStats, BaseStats resultStats)
        {
            var sb = new StringBuilder();

            void AppendStatDifference<T>(string name, T frameStat, T resultStat)
                where T : IEquatable<T>
            {
                if (frameStat.Equals(resultStat))
                    sb.AppendLine($"- {name + ":",-31} {frameStat,-12} (identical)");
                else
                    sb.AppendLine($"- {name + ":",-31} {frameStat,-10} -> {resultStat}");
            }

            sb.AppendLine("Base stats:");
            AppendStatDifference("CommittedScore", originalStats.CommittedScore, resultStats.CommittedScore);
            AppendStatDifference("PendingScore", originalStats.PendingScore, resultStats.PendingScore);
            AppendStatDifference("TotalScore", originalStats.TotalScore, resultStats.TotalScore);
            AppendStatDifference("StarScore", originalStats.StarScore, resultStats.StarScore);
            AppendStatDifference("Combo", originalStats.Combo, resultStats.Combo);
            AppendStatDifference("MaxCombo", originalStats.MaxCombo, resultStats.MaxCombo);
            AppendStatDifference("ScoreMultiplier", originalStats.ScoreMultiplier, resultStats.ScoreMultiplier);
            AppendStatDifference("NotesHit", originalStats.NotesHit, resultStats.NotesHit);
            AppendStatDifference("TotalNotes", originalStats.TotalNotes, resultStats.TotalNotes);
            AppendStatDifference("NotesMissed", originalStats.NotesMissed, resultStats.NotesMissed);
            AppendStatDifference("Percent", originalStats.Percent, resultStats.Percent);
            AppendStatDifference("StarPowerTickAmount", originalStats.StarPowerTickAmount,
                resultStats.StarPowerTickAmount);
            AppendStatDifference("TotalStarPowerTicks", originalStats.TotalStarPowerTicks,
                resultStats.TotalStarPowerTicks);
            AppendStatDifference("TimeInStarPower", originalStats.TimeInStarPower, resultStats.TimeInStarPower);
            AppendStatDifference("IsStarPowerActive", originalStats.IsStarPowerActive, resultStats.IsStarPowerActive);
            AppendStatDifference("StarPowerPhrasesHit", originalStats.StarPowerPhrasesHit,
                resultStats.StarPowerPhrasesHit);
            AppendStatDifference("TotalStarPowerPhrases", originalStats.TotalStarPowerPhrases,
                resultStats.TotalStarPowerPhrases);
            AppendStatDifference("StarPowerPhrasesMissed", originalStats.StarPowerPhrasesMissed,
                resultStats.StarPowerPhrasesMissed);
            AppendStatDifference("SoloBonuses", originalStats.SoloBonuses, resultStats.SoloBonuses);
            AppendStatDifference("StarPowerScore", originalStats.StarPowerScore, resultStats.StarPowerScore);
            // PrintStatDifference("Stars",                  originalStats.Stars,                  resultStats.Stars);

            sb.AppendLine();
            switch (originalStats, resultStats)
            {
                case (GuitarStats originalGuitar, GuitarStats resultGuitar):
                {
                    sb.AppendLine("Guitar stats:");
                    AppendStatDifference("Overstrums", originalGuitar.Overstrums, resultGuitar.Overstrums);
                    AppendStatDifference("HoposStrummed", originalGuitar.HoposStrummed, resultGuitar.HoposStrummed);
                    AppendStatDifference("GhostInputs", originalGuitar.GhostInputs, resultGuitar.GhostInputs);
                    AppendStatDifference("StarPowerWhammyTicks", originalGuitar.StarPowerWhammyTicks,
                        resultGuitar.StarPowerWhammyTicks);
                    AppendStatDifference("SustainScore", originalGuitar.SustainScore, resultGuitar.SustainScore);
                    break;
                }
                case (DrumsStats originalDrums, DrumsStats resultDrums):
                {
                    sb.AppendLine("Drums stats:");
                    AppendStatDifference("Overhits", originalDrums.Overhits, resultDrums.Overhits);
                    break;
                }
                case (VocalsStats originalVocals, VocalsStats resultVocals):
                {
                    sb.AppendLine("Vocals stats:");
                    AppendStatDifference("TicksHit", originalVocals.TicksHit, resultVocals.TicksHit);
                    AppendStatDifference("TicksMissed", originalVocals.TicksMissed, resultVocals.TicksMissed);
                    AppendStatDifference("TotalTicks", originalVocals.TotalTicks, resultVocals.TotalTicks);
                    break;
                }
                case (ProKeysStats originalKeys, ProKeysStats resultKeys):
                {
                    sb.AppendLine("Pro Keys stats:");
                    AppendStatDifference("Overhits", originalKeys.Overhits, resultKeys.Overhits);
                    break;
                }
                default:
                {
                    if (originalStats.GetType() != resultStats.GetType())
                        sb.AppendLine(
                            $"Stats types do not match! Original: {originalStats.GetType()}, result: {resultStats.GetType()}");
                    else
                        sb.AppendLine($"Unhandled stats type {originalStats.GetType()}!");
                    break;
                }
            }
            return sb.ToString();
        }

        private AnalysisResult[] Analyze()
        {
            var frames = new List<ReplayFrame>();
            for (int i = 0; i < _replay.Frames.Length; i++)
            {
                frames.Add(_replay.Frames[i]);
            }

            var replayResults = RunFrames(frames);
            return replayResults.ToArray();
        }

        private List<AnalysisResult> RunFrames(List<ReplayFrame> frames)
        {
            var engines = new List<BaseEngine>();
            var manager = new EngineManager();
            // I don't think the engines will break if their update time goes beyond their last input
            double maxTime = 0;
            var results = new List<AnalysisResult>();
            foreach (var frame in frames)
            {
                var engine = CreateEngine(frame.Profile, frame.EngineParameters);
                engines.Add(engine);
                manager.Register(engine, frame.Profile.CurrentInstrument, _chart);
                engine.SetSpeed(frame.EngineParameters.SongSpeed);
                engine.Reset();

                maxTime = _chart.GetEndTime();
                if (frame.Inputs.Length > 0)
                {
                    double last = frame.Inputs[^1].Time;
                    if (last > maxTime)
                    {
                        maxTime = last;
                    }
                }
                maxTime += 2;
            }

            // TODO: Figure out why we're failing the participant count assertion in UnisonEvent
            //  when bonus SP is being awarded. (Validation succeeds, though, so it's not awarding extra SP in the replay)

            // Seems like a sensible default?
            _fps = _fps > 0 ? _fps : 60;
            int currentInput = 0;
            foreach (var time in GenerateFrameTimes(-2, maxTime))
            {
                for (var i = 0;i < engines.Count; i++) {
                    for (; currentInput < frames[i].Inputs.Length; currentInput++)
                    {
                        var input = frames[i].Inputs[currentInput];
                        if (input.Time > time)
                        {
                            break;
                        }

                        engines[i].QueueInput(ref input);
                    }
                }

                foreach (var engine in engines)
                {
                    engine.Update(time);
                }
            }

            for (var i = 0; i < frames.Count; i++)
            {
                bool passed = IsPassResult(frames[i].Stats, engines[i].BaseStats);
                results.Add(new AnalysisResult
                {
                    Passed = passed,
                    Frame = frames[i],
                    OriginalStats = frames[i].Stats,
                    ResultStats = engines[i].BaseStats,
                });
            }
            return results;
        }

        private BaseEngine CreateEngine(YargProfile profile, BaseEngineParameters parameters)
        {
            switch (profile.GameMode)
            {
                case GameMode.FiveFretGuitar:
                {
                    // Reset the notes
                    var notes = _chart.GetFiveFretTrack(profile.CurrentInstrument)
                        .GetDifficulty(profile.CurrentDifficulty).Clone();
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.AllNotes)
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargFiveFretEngine(
                        notes,
                        _chart.SyncTrack,
                        (GuitarEngineParameters) parameters,
                        profile.IsBot);
                }
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                {
                    // Reset the notes
                    var notes = _chart.GetDrumsTrack(profile.CurrentInstrument)
                        .GetDifficulty(profile.CurrentDifficulty).Clone();
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.AllNotes)
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargDrumsEngine(
                        notes,
                        _chart.SyncTrack,
                        (DrumsEngineParameters) parameters,
                        profile.IsBot);
                }
                case GameMode.ProKeys:
                {
                    // Reset the notes
                    var notes = _chart.ProKeys.GetDifficulty(profile.CurrentDifficulty).Clone();
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.AllNotes)
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargProKeysEngine(
                        notes,
                        _chart.SyncTrack,
                        (ProKeysEngineParameters) parameters,
                        profile.IsBot);
                }
                case GameMode.Vocals:
                {
                    // Get the notes
                    var notes = _chart.GetVocalsTrack(profile.CurrentInstrument)
                        .Parts[profile.HarmonyIndex].Clone();
                    profile.ApplyVocalModifiers(notes);

                    // Create engine
                    return new YargVocalsEngine(
                        // hate the double-clone lol but for now it'll be fine
                        notes.CloneAsInstrumentDifficulty(),
                        _chart.SyncTrack,
                        (VocalsEngineParameters) parameters,
                        profile.IsBot);
                }
                default:
                    throw new InvalidOperationException("Game mode not configured!");
            }
        }

        private List<double> GenerateFrameTimes(double from, double to)
        {
            YargLogger.Assert(to > from, "Invalid time range");

            double frameTime = 1.0 / _fps;

            var times = new List<double>();
            for (double time = from; time < to; time += frameTime)
            {
                // Add up to 45% random adjustment to the frame time
                var randomAdjustment = _random.NextDouble() * 0.5;

                // Randomly make the adjustment negative
                if (_random.Next(2) == 0 && time > from)
                {
                    randomAdjustment = -randomAdjustment;
                }

                double adjustedTime = time + frameTime * randomAdjustment;

                if (adjustedTime > to)
                {
                    adjustedTime = to;
                }

                times.Add(adjustedTime);
            }

            // Add the end time just in case
            times.Add(to);

            return times;
        }

        private static bool IsPassResult(BaseStats original, BaseStats result)
        {
            YargLogger.LogFormatDebug("Score: {0} == {1}\nHit: {2} == {3}\nMissed: {4} == {5}\nCombo: {6} == {7}\nMaxCombo: {8} == {9}\n",
                original.CommittedScore, result.CommittedScore,
                original.NotesHit, result.NotesHit,
                original.NotesMissed, result.NotesMissed,
                original.Combo, result.Combo,
                original.MaxCombo, result.MaxCombo);

            YargLogger.LogFormatDebug("Solo: {0} == {1}\nSP Bonus: {2} == {3}\nSP Phrases: {4} == {5}\n" +
                "Time In SP: {6} == {7}\nSP Ticks: {8} == {9}",
                original.SoloBonuses, result.SoloBonuses,
                original.StarPowerScore, result.StarPowerScore,
                original.StarPowerPhrasesHit, result.StarPowerPhrasesHit,
                original.TimeInStarPower, result.TimeInStarPower,
                original.TotalStarPowerTicks, result.TotalStarPowerTicks);

            bool instrumentPass = true;

            if(original is GuitarStats originalGuitar && result is GuitarStats resultGuitar)
            {
                instrumentPass = originalGuitar.Overstrums == resultGuitar.Overstrums &&
                    originalGuitar.GhostInputs == resultGuitar.GhostInputs &&
                    originalGuitar.HoposStrummed == resultGuitar.HoposStrummed &&
                    originalGuitar.StarPowerWhammyTicks == resultGuitar.StarPowerWhammyTicks &&
                    originalGuitar.SustainScore == resultGuitar.SustainScore;

                YargLogger.LogFormatDebug("Guitar:\nOverstrums: {0} == {1}\nGhost Inputs: {2} == {3}\nHOPOs Strummed: {4} == {5}\n" +
                    "Whammy Ticks: {6} == {7}\nSustain Points: {8} == {9}",
                    originalGuitar.Overstrums, resultGuitar.Overstrums,
                    originalGuitar.GhostInputs, resultGuitar.GhostInputs,
                    originalGuitar.HoposStrummed, resultGuitar.HoposStrummed,
                    originalGuitar.StarPowerWhammyTicks, resultGuitar.StarPowerWhammyTicks,
                    originalGuitar.SustainScore, resultGuitar.SustainScore);
            }

            bool generalPass = original.CommittedScore == result.CommittedScore &&
                original.NotesHit == result.NotesHit &&
                original.NotesMissed == result.NotesMissed &&
                original.Combo == result.Combo &&
                original.MaxCombo == result.MaxCombo &&
                original.SoloBonuses == result.SoloBonuses &&
                original.StarPowerScore == result.StarPowerScore &&
                original.StarPowerPhrasesHit == result.StarPowerPhrasesHit &&
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                original.TimeInStarPower == result.TimeInStarPower &&
                original.TotalStarPowerTicks == result.TotalStarPowerTicks;

            return generalPass && instrumentPass;
        }
    }
}