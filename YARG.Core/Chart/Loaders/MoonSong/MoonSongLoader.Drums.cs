using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using YARG.Core.Parsing;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private bool _discoFlip = false;

        private uint _lastLanePhraseTick;
        private List<int>? _validLaneNotes = null;

        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument)
        {
            _discoFlip = false;
            return instrument.ToGameMode() switch
            {
                GameMode.FourLaneDrums => LoadDrumsTrack(instrument, CreateFourLaneDrumNote),
                GameMode.FiveLaneDrums => LoadDrumsTrack(instrument, CreateFiveLaneDrumNote),
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!", nameof(instrument))
            };
        }

        private InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument, CreateNoteDelegate<DrumNote> createNote)
        {
            Dictionary<Difficulty, InstrumentDifficulty<DrumNote>> difficulties = new();
            if (instrument is Instrument.FourLaneDrums)
            {
                difficulties.Add(Difficulty.Beginner, LoadDifficulty(instrument, Difficulty.Easy, CreateFourLaneDrumBeginnerNote, HandleTextEvent, ValidateDrumsPhrase));
            }
            else if (instrument is Instrument.FiveLaneDrums)
            {
                difficulties.Add(Difficulty.Beginner, LoadDifficulty(instrument, Difficulty.Easy, CreateFiveLaneDrumBeginnerNote, HandleTextEvent, ValidateDrumsPhrase));
            }

            difficulties.Add(Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote, HandleTextEvent, ValidateDrumsPhrase));
            difficulties.Add(Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote, HandleTextEvent, ValidateDrumsPhrase));
            difficulties.Add(Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote, HandleTextEvent, ValidateDrumsPhrase));
            difficulties.Add(Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote, HandleTextEvent, ValidateDrumsPhrase));
            difficulties.Add(Difficulty.ExpertPlus, LoadDifficulty(instrument, Difficulty.ExpertPlus, createNote, HandleTextEvent, ValidateDrumsPhrase));

            var track = new InstrumentTrack<DrumNote>(instrument, difficulties);

            // Add animation events
            var animationEvents = GetDrumAnimationEvents(track);
            track.AddAnimationEvent(animationEvents);

            return track;
        }

        private DrumNote CreateFourLaneDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = GetFourLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);

            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            generalFlags = ModifyDrumLaneFlags(moonNote, currentPhrases, generalFlags);

            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick);
        }

        private DrumNote CreateFiveLaneDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = GetFiveLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);

            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            generalFlags = ModifyDrumLaneFlags(moonNote, currentPhrases, generalFlags);

            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick);
        }

        private DrumNote CreateFourLaneDrumBeginnerNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = FourLaneDrumPad.Wildcard;
            var noteType = DrumNoteType.Neutral;
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote((FourLaneDrumPad) pad, noteType, drumFlags, generalFlags, time, moonNote.tick);
        }

        private DrumNote CreateFiveLaneDrumBeginnerNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = FiveLaneDrumPad.Wildcard;
            var noteType = DrumNoteType.Neutral;
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote((FiveLaneDrumPad) pad, noteType, drumFlags, generalFlags, time, moonNote.tick);
        }

        private void HandleTextEvent(MoonText text)
        {
            // Ignore on 5-lane or standard Drums
            if (_settings.DrumsType != DrumsType.FourLane && _currentInstrument is Instrument.FourLaneDrums)
                return;

            // Parse out event data
            if (!TextEvents.TryParseDrumsMixEvent(text.text, out var difficulty, out var config, out var setting))
                return;

            // Ignore if event is not for the given difficulty
            var currentDiff = _currentDifficulty;
            if (currentDiff == Difficulty.ExpertPlus)
                currentDiff = Difficulty.Expert;
            if (difficulty != currentDiff)
                return;

            _discoFlip = setting == DrumsMixSetting.DiscoFlip;
        }

        private Phrase ValidateDrumsPhrase(Phrase phrase)
        {
            if (phrase.Type != PhraseType.DrumFill)
            {
                // We only care about drum fills
                return phrase;
            }

            if (phrase.Time < _codaTime)
            {
                return phrase;
            }

            // If we're here, we were presented a drum fill after a coda and that needs to be a BRE
            return new Phrase(PhraseType.BigRockEnding, phrase.Time, phrase.TimeLength, phrase.Tick, phrase.TickLength);
        }

        private FourLaneDrumPad GetFourLaneDrumPad(MoonNote moonNote)
        {
            var pad = _settings.DrumsType switch
            {
                DrumsType.FourLane => MoonNoteToFourLane(moonNote),
                DrumsType.FiveLane => GetFourLaneFromFiveLane(moonNote),
                _ => throw new InvalidOperationException($"Unexpected drums type {_settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            return pad;
        }

        private FourLaneDrumPad GetFourLaneFromFiveLane(MoonNote moonNote)
        {
            // Conversion table:
            // | 5-lane | 4-lane Pro    |
            // | :----- | :---------    |
            // | Red    | Red           |
            // | Yellow | Yellow cymbal |
            // | Blue   | Blue tom      |
            // | Orange | Green cymbal  |
            // | Green  | Green tom     |
            // | O + G  | G cym + B tom |

            var fiveLanePad = MoonNoteToFiveLane(moonNote);
            var pad = fiveLanePad switch
            {
                FiveLaneDrumPad.Kick   => FourLaneDrumPad.Kick,
                FiveLaneDrumPad.Red    => FourLaneDrumPad.RedDrum,
                FiveLaneDrumPad.Yellow => FourLaneDrumPad.YellowCymbal,
                FiveLaneDrumPad.Blue   => FourLaneDrumPad.BlueDrum,
                FiveLaneDrumPad.Orange => FourLaneDrumPad.GreenCymbal,
                FiveLaneDrumPad.Green  => FourLaneDrumPad.GreenDrum,
                _ => throw new InvalidOperationException($"Invalid five lane drum pad {fiveLanePad}!")
            };

            // Handle potential overlaps
            if (pad is FourLaneDrumPad.GreenCymbal)
            {
                foreach (var note in moonNote.chord)
                {
                    if (note == moonNote)
                        continue;

                    var otherPad = MoonNoteToFiveLane(note);
                    pad = (pad, otherPad) switch
                    {
                        // (Calculated pad, other note in chord) => corrected pad to prevent same-color overlapping
                        (FourLaneDrumPad.GreenCymbal, FiveLaneDrumPad.Green) => FourLaneDrumPad.BlueCymbal,
                        _ => pad
                    };
                }
            }

            // Down-convert to standard 4-lane
            if (_currentInstrument is Instrument.FourLaneDrums)
            {
                pad = pad switch
                {
                    FourLaneDrumPad.YellowCymbal => FourLaneDrumPad.YellowDrum,
                    FourLaneDrumPad.BlueCymbal   => FourLaneDrumPad.BlueDrum,
                    FourLaneDrumPad.GreenCymbal  => FourLaneDrumPad.GreenDrum,
                    _ => pad
                };
            }

            return pad;
        }

        private FiveLaneDrumPad GetFiveLaneDrumPad(MoonNote moonNote)
        {
            return _settings.DrumsType switch
            {
                DrumsType.FiveLane => MoonNoteToFiveLane(moonNote),
                DrumsType.FourLane => GetFiveLaneFromFourLane(moonNote),
                _ => throw new InvalidOperationException($"Unexpected drums type {_settings.DrumsType}! (Drums type should have been calculated by now)")
            };
        }

        private FiveLaneDrumPad GetFiveLaneFromFourLane(MoonNote moonNote)
        {
            // Conversion table:
            // | 4-lane Pro    | 5-lane |
            // | :---------    | :----- |
            // | Red           | Red    |
            // | Yellow cymbal | Yellow |
            // | Yellow tom    | Blue   |
            // | Blue cymbal   | Orange |
            // | Blue tom      | Blue   |
            // | Green cymbal  | Orange |
            // | Green tom     | Green  |
            // | Y tom + B tom | R + B  |
            // | B cym + G cym | Y + O  |

            var fourLanePad = MoonNoteToFourLane(moonNote);
            var pad = fourLanePad switch
            {
                FourLaneDrumPad.Kick         => FiveLaneDrumPad.Kick,
                FourLaneDrumPad.RedDrum      => FiveLaneDrumPad.Red,
                FourLaneDrumPad.YellowCymbal => FiveLaneDrumPad.Yellow,
                FourLaneDrumPad.YellowDrum   => FiveLaneDrumPad.Blue,
                FourLaneDrumPad.BlueCymbal   => FiveLaneDrumPad.Orange,
                FourLaneDrumPad.BlueDrum     => FiveLaneDrumPad.Blue,
                FourLaneDrumPad.GreenCymbal  => FiveLaneDrumPad.Orange,
                FourLaneDrumPad.GreenDrum    => FiveLaneDrumPad.Green,
                _ => throw new InvalidOperationException($"Invalid four lane drum pad {fourLanePad}!")
            };

            // Handle special cases
            if (pad is FiveLaneDrumPad.Blue or FiveLaneDrumPad.Orange)
            {
                foreach (var note in moonNote.chord)
                {
                    if (note == moonNote)
                        continue;

                    var otherPad = MoonNoteToFourLane(note);
                    pad = (pad, otherPad) switch
                    {
                        // (Calculated pad, other note in chord) => corrected pad to prevent same-color overlapping
                        (FiveLaneDrumPad.Blue, FourLaneDrumPad.BlueDrum) => FiveLaneDrumPad.Red,
                        (FiveLaneDrumPad.Orange, FourLaneDrumPad.GreenCymbal) => FiveLaneDrumPad.Yellow,
                        _ => pad
                    };
                }
            }

            return pad;
        }

        private FourLaneDrumPad MoonNoteToFourLane(MoonNote moonNote)
        {
            var pad = moonNote.drumPad switch
            {
                MoonNote.DrumPad.Kick   => FourLaneDrumPad.Kick,
                MoonNote.DrumPad.Red    => FourLaneDrumPad.RedDrum,
                MoonNote.DrumPad.Yellow => FourLaneDrumPad.YellowDrum,
                MoonNote.DrumPad.Blue   => FourLaneDrumPad.BlueDrum,
                MoonNote.DrumPad.Orange => FourLaneDrumPad.GreenDrum,
                MoonNote.DrumPad.Green  => FourLaneDrumPad.GreenDrum,
                _ => throw new ArgumentException($"Invalid Moonscraper drum pad {moonNote.drumPad}!", nameof(moonNote))
            };

            if (_currentInstrument is not Instrument.FourLaneDrums)
            {
                var flags = moonNote.flags;

                // Disco flip
                if (_discoFlip)
                {
                    if (pad == FourLaneDrumPad.RedDrum)
                    {
                        // Red drums in disco flip are turned into yellow cymbals
                        pad = FourLaneDrumPad.YellowDrum;
                        flags |= MoonNote.Flags.ProDrums_Cymbal;
                    }
                    else if (pad == FourLaneDrumPad.YellowDrum)
                    {
                        // Both yellow cymbals and yellow drums are turned into red drums in disco flip
                        pad = FourLaneDrumPad.RedDrum;
                        flags &= ~MoonNote.Flags.ProDrums_Cymbal;
                    }
                }

                // Cymbal marking
                if ((flags & MoonNote.Flags.ProDrums_Cymbal) != 0)
                {
                    pad = pad switch
                    {
                        FourLaneDrumPad.YellowDrum => FourLaneDrumPad.YellowCymbal,
                        FourLaneDrumPad.BlueDrum   => FourLaneDrumPad.BlueCymbal,
                        FourLaneDrumPad.GreenDrum  => FourLaneDrumPad.GreenCymbal,
                        _ => throw new InvalidOperationException($"Cannot mark pad {pad} as a cymbal!")
                    };
                }
            }

            return pad;
        }

        private FiveLaneDrumPad MoonNoteToFiveLane(MoonNote moonNote)
        {
            var pad = moonNote.drumPad switch
            {
                MoonNote.DrumPad.Kick   => FiveLaneDrumPad.Kick,
                MoonNote.DrumPad.Red    => FiveLaneDrumPad.Red,
                MoonNote.DrumPad.Yellow => FiveLaneDrumPad.Yellow,
                MoonNote.DrumPad.Blue   => FiveLaneDrumPad.Blue,
                MoonNote.DrumPad.Orange => FiveLaneDrumPad.Orange,
                MoonNote.DrumPad.Green  => FiveLaneDrumPad.Green,
                _ => throw new ArgumentException($"Invalid Moonscraper drum pad {moonNote.drumPad}!", nameof(moonNote))
            };

            return pad;
        }

        private DrumNoteType GetDrumNoteType(MoonNote moonNote)
        {
            var noteType = DrumNoteType.Neutral;

            // Accents/ghosts
            if ((moonNote.flags & MoonNote.Flags.ProDrums_Accent) != 0)
                noteType = DrumNoteType.Accent;
            else if ((moonNote.flags & MoonNote.Flags.ProDrums_Ghost) != 0)
                noteType = DrumNoteType.Ghost;

            return noteType;
        }

        private DrumNoteFlags GetDrumNoteFlags(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var flags = DrumNoteFlags.None;

            // SP activator
            if (currentPhrases.TryGetValue(MoonPhrase.Type.ProDrums_Activation, out var activationPhrase) &&
                IsNoteClosestToEndOfPhrase(_moonSong, moonNote, activationPhrase))
            {
                flags |= DrumNoteFlags.StarPowerActivator;
            }

            return flags;
        }

        private NoteFlags ModifyDrumLaneFlags(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases, NoteFlags flags)
        {
            MoonPhrase? lanePhrase = null;
            bool isTrill = false;

            if ((flags & NoteFlags.Tremolo) != 0)
            {
                currentPhrases.TryGetValue(MoonPhrase.Type.TremoloLane, out lanePhrase);
            }
            else if ((flags & NoteFlags.Trill) != 0)
            {
                currentPhrases.TryGetValue(MoonPhrase.Type.TrillLane, out lanePhrase);
                isTrill = true;
            }

            if (lanePhrase == null)
            {
                return flags;
            }

            if (_validLaneNotes == null || lanePhrase.tick != _lastLanePhraseTick)
            {
                _lastLanePhraseTick = lanePhrase.tick;
                _validLaneNotes = GetValidLaneNotes(moonNote, lanePhrase, isTrill);
            }

            if (!_validLaneNotes.Contains(moonNote.rawNote))
            {
                flags &= ~NoteFlags.Tremolo;
                flags &= ~NoteFlags.Trill;
                flags &= ~NoteFlags.LaneStart;
                flags &= ~NoteFlags.LaneEnd;
            }

            return flags;

            static List<int> GetValidLaneNotes(MoonNote moonNote, MoonPhrase lanePhrase, bool isTrill)
            {
                // Iterate forward every note in this phrase to find the notes that appear the most
                // Assumes that this will only run when the first note in a phrase is provided
                Dictionary<int,int> noteTotals = new();

                // Stop searching if the current note value has this much of a lead over the others
                const int CLINCH_THRESHOLD = 5;
                int highestTotal = 0;

                for (var noteRef = moonNote; noteRef != null && IsEventInPhrase(noteRef, lanePhrase); noteRef = noteRef.next)
                {
                    if (noteRef.isChord && noteRef.drumPad == MoonNote.DrumPad.Kick)
                    {
                        // Kick tremolos are only possible with a winning total on non-chorded kicks, no ties with other notes
                        continue;
                    }

                    int thisNote = noteRef.rawNote;

                    int thisTotal;
                    if (noteTotals.ContainsKey(thisNote))
                    {
                        thisTotal = ++noteTotals[thisNote];
                    }
                    else
                    {
                        thisTotal = noteTotals[thisNote] = 1;
                    }

                    if (thisTotal <= highestTotal)
                    {
                        continue;
                    }

                    highestTotal = thisTotal;

                    if (thisTotal >= CLINCH_THRESHOLD)
                    {
                        bool stopSearching = true;
                        foreach(var (otherNote, otherTotal) in noteTotals)
                        {
                            if (otherNote == thisNote)
                            {
                                continue;
                            }

                            if (thisTotal - otherTotal < CLINCH_THRESHOLD)
                            {
                                stopSearching = false;
                                break;
                            }
                        }

                        if (stopSearching)
                        {
                            // Safe to say this is the only laned note a tremolo phrase
                            break;
                        }
                    }
                }

                int validNoteTotal = isTrill ? highestTotal - 2 : highestTotal;
                List<int> validTremoloNotes = new();

                foreach (var (note, total) in noteTotals)
                {
                    if (total >= validNoteTotal)
                    {
                        validTremoloNotes.Add(note);
                    }
                }

                return validTremoloNotes;
            }
        }

        private List<AnimationEvent> GetDrumAnimationEvents(InstrumentTrack<DrumNote> track)
        {
            var events = new List<AnimationEvent>();
            var instrument = track.Instrument;

            // Find a difficulty
            // var difficulty = track.FirstDifficulty().Difficulty;
            var difficulty = Difficulty.Expert;

            // Get the relevant MoonChart
            var chart = GetMoonChart(instrument, difficulty);

            foreach (var animNote in chart.animationNotes)
            {
                // Look up the note number and create an appropriate animation event
                var animType = GetDrumAnimationType((byte) animNote.noteNumber);

                if (!animType.HasValue) continue;

                events.Add(new AnimationEvent(animType.Value,
                    _moonSong.TickToTime(animNote.tick), GetLengthInTime(animNote), animNote.tick, animNote.length));
            }

            return events;
        }

        private static AnimationEvent.AnimationType? GetDrumAnimationType(byte noteNumber)
        {
            return noteNumber switch
            {
                MidIOHelper.KICK => AnimationEvent.AnimationType.Kick,
                MidIOHelper.HIHAT_OPEN => AnimationEvent.AnimationType.OpenHiHat,
                MidIOHelper.SNARE_LH_HARD => AnimationEvent.AnimationType.SnareLhHard,
                MidIOHelper.SNARE_LH_SOFT => AnimationEvent.AnimationType.SnareLhSoft,
                MidIOHelper.SNARE_RH_HARD => AnimationEvent.AnimationType.SnareRhHard,
                MidIOHelper.SNARE_RH_SOFT => AnimationEvent.AnimationType.SnareRhSoft,
                MidIOHelper.HIHAT_LH => AnimationEvent.AnimationType.HihatLeftHand,
                MidIOHelper.HIHAT_RH => AnimationEvent.AnimationType.HihatRightHand,
                MidIOHelper.PERCUSSION_RH => AnimationEvent.AnimationType.PercussionRightHand,
                MidIOHelper.CRASH_1_LH_HARD => AnimationEvent.AnimationType.Crash1LhHard,
                MidIOHelper.CRASH_1_LH_SOFT => AnimationEvent.AnimationType.Crash1LhSoft,
                MidIOHelper.CRASH_1_RH_HARD => AnimationEvent.AnimationType.Crash1RhHard,
                MidIOHelper.CRASH_1_RH_SOFT => AnimationEvent.AnimationType.Crash1RhSoft,
                MidIOHelper.CRASH_2_RH_HARD => AnimationEvent.AnimationType.Crash2RhHard,
                MidIOHelper.CRASH_2_RH_SOFT => AnimationEvent.AnimationType.Crash2RhSoft,
                MidIOHelper.CRASH_1_CHOKE => AnimationEvent.AnimationType.Crash1Choke,
                MidIOHelper.CRASH_2_CHOKE => AnimationEvent.AnimationType.Crash2Choke,
                MidIOHelper.RIDE_RH => AnimationEvent.AnimationType.RideRh,
                MidIOHelper.RIDE_LH => AnimationEvent.AnimationType.RideLh,
                MidIOHelper.CRASH_2_LH_HARD => AnimationEvent.AnimationType.Crash2LhHard,
                MidIOHelper.CRASH_2_LH_SOFT => AnimationEvent.AnimationType.Crash2LhSoft,
                MidIOHelper.TOM_1_LH => AnimationEvent.AnimationType.Tom1LeftHand,
                MidIOHelper.TOM_1_RH => AnimationEvent.AnimationType.Tom1RightHand,
                MidIOHelper.TOM_2_LH => AnimationEvent.AnimationType.Tom2LeftHand,
                MidIOHelper.TOM_2_RH => AnimationEvent.AnimationType.Tom2RightHand,
                MidIOHelper.FLOOR_TOM_LH => AnimationEvent.AnimationType.FloorTomLeftHand,
                MidIOHelper.FLOOR_TOM_RH => AnimationEvent.AnimationType.FloorTomRightHand,
                _ => null
            };
        }
    }
}