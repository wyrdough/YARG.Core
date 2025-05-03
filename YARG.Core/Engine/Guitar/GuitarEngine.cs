﻿using System;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarEngineParameters,
        GuitarStats>
    {
        protected const byte OPEN_MASK = 64;
        // Mask of all the solo buttons in bit math
        protected const ushort SOLO_MASK = 31744;

        public delegate void OverstrumEvent();

        public OverstrumEvent? OnOverstrum;

        public byte EffectiveButtonMask { get; protected set; } = OPEN_MASK;
        public ushort InputButtonMask { get; protected set; }
        public byte LastButtonMask { get; protected set; }

        public bool StandardButtonHeld  { get; private set; }

        protected bool HasFretted;
        protected bool HasStrummed;
        protected bool HasTapped = true;
        protected bool HasWhammied;

        protected bool IsFretPress;

        public bool WasNoteGhosted { get; protected set; }

        /// <summary>
        /// The amount of time a hopo is allowed to take a strum input.
        /// Strum after this time and it will overstrum.
        /// </summary>
        protected EngineTimer HopoLeniencyTimer;

        /// <summary>
        /// The amount of time a strum can be inputted before fretting the correct note.
        /// Fretting after this time will overstrum.
        /// </summary>
        protected EngineTimer StrumLeniencyTimer;

        protected double FrontEndExpireTime;

        protected GuitarEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
            StrumLeniencyTimer = new EngineTimer(engineParameters.StrumLeniency);
            HopoLeniencyTimer = new EngineTimer(engineParameters.HopoLeniency);
            StarPowerWhammyTimer = new EngineTimer(engineParameters.StarPowerWhammyBuffer);

            GetWaitCountdowns(Notes);
        }

        public EngineTimer GetHopoLeniencyTimer() => HopoLeniencyTimer;
        public EngineTimer GetStrumLeniencyTimer() => StrumLeniencyTimer;
        public double GetFrontEndExpireTime() => FrontEndExpireTime;

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = CurrentTime;

            // Check all timers
            if (HopoLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(HopoLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing hopo leniency end time at {0}", HopoLeniencyTimer.EndTime);
                    QueueUpdateTime(HopoLeniencyTimer.EndTime, "HOPO Leniency End");
                }
            }

            if (StrumLeniencyTimer.IsActive)
            {
                if (IsTimeBetween(StrumLeniencyTimer.EndTime, previousTime, nextTime))
                {
                    YargLogger.LogFormatTrace("Queuing strum leniency end time at {0}",
                        StrumLeniencyTimer.EndTime);
                    QueueUpdateTime(StrumLeniencyTimer.EndTime, "Strum Leniency End");
                }
            }
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            byte buttons = EffectiveButtonMask;

            EffectiveButtonMask = OPEN_MASK;

            HasFretted = false;
            HasStrummed = false;
            HasTapped = true;

            WasNoteGhosted = false;

            StrumLeniencyTimer.Disable();
            HopoLeniencyTimer.Disable();
            StarPowerWhammyTimer.Disable();

            FrontEndExpireTime = 0;

            ActiveSustains.Clear();

            base.Reset(keepCurrentButtons);

            if (keepCurrentButtons)
            {
                EffectiveButtonMask = buttons;
            }
        }

        protected virtual void Overstrum()
        {
            // Can't overstrum before first note is hit/missed
            if (NoteIndex == 0)
            {
                return;
            }

            // Cancel overstrum if past last note and no active sustains
            if (NoteIndex >= Chart.Notes.Count && ActiveSustains.Count == 0)
            {
                return;
            }

            // Cancel overstrum if WaitCountdown is active
            if (IsWaitCountdownActive)
            {
                YargLogger.LogFormatTrace("Overstrum prevented during WaitCountdown at time: {0}, tick: {1}", CurrentTime, CurrentTick);
                return;
            }

            // Prevent overstrum if the current ButtonMask satisfies the active lane
            var laneMask = GetLaneMask();
            if (ActiveLaneIncludesNote(laneMask))
            {
                return;
            }

            if (IsLaneActive)
            {
                YargLogger.LogFormatTrace("Punishing lane overstrum at {0}. Current mask: {1}, RequiredLaneNote: {2}, NextTrillNote: {3}", CurrentTime, laneMask, RequiredLaneNote, NextTrillNote);
            }

            YargLogger.LogFormatTrace("Overstrummed at {0}", CurrentTime);

            // Break all active sustains
            for (int i = 0; i < ActiveSustains.Count; i++)
            {
                var sustain = ActiveSustains[i];
                ActiveSustains.RemoveAt(i);
                YargLogger.LogFormatTrace("Ended sustain (end time: {0}) at {1}", sustain.GetEndTime(SyncTrack, 0), CurrentTime);
                i--;

                double finalScore = CalculateSustainPoints(ref sustain, CurrentTick);
                EngineStats.CommittedScore += (int) Math.Ceiling(finalScore);
                OnSustainEnd?.Invoke(sustain.Note, CurrentTime, sustain.HasFinishedScoring);
            }

            if (NoteIndex < Notes.Count)
            {
                // Don't remove the phrase if the current note being overstrummed is the start of a phrase
                if (!Notes[NoteIndex].IsStarPowerStart)
                {
                    StripStarPower(Notes[NoteIndex]);
                }
            }

            ResetCombo();
            EngineStats.Overstrums++;

            UpdateMultiplier();

            OnOverstrum?.Invoke();
        }

        protected override bool CanSustainHold(GuitarNote note)
        {
            var mask = note.IsDisjoint ? note.DisjointMask : note.NoteMask;

            var buttonsMasked = EffectiveButtonMask;
            if ((mask & OPEN_MASK) != 0)
            {
                buttonsMasked |= OPEN_MASK;
            }

            bool extendedSustainHold = (mask & buttonsMasked) == mask;

            // Open chord
            if ((note.ParentOrSelf.NoteMask & OPEN_MASK) != 0 && note.ParentOrSelf.NoteMask != OPEN_MASK &&
                (note.DisjointMask & OPEN_MASK) != 0)
            {
                if (note.IsDisjoint || note.IsExtendedSustain)
                {
                    return true;
                }
            }

            return note.IsExtendedSustain ? extendedSustainHold : CanNoteBeHit(note);
        }

        protected override void HitNote(GuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Fret: {0}, Index: {1}, Hit: {2}, Missed: {3})", note.Fret, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetHitState(true, true);

            // Detect if the last note(s) were skipped
            bool skipped = SkipPreviousNotes(note);

            if (note.IsStarPower && note.IsStarPowerEnd)
            {
                AwardStarPower(note);
                EngineStats.StarPowerPhrasesHit++;
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            if (IsSoloActive)
            {
                Solos[CurrentSoloIndex].NotesHit++;
            }

            if (note.IsSoloEnd)
            {
                EndSolo();
            }

            IncrementCombo();

            EngineStats.NotesHit++;

            UpdateMultiplier();

            AddScore(note);

            if (note.IsDisjoint)
            {
                foreach (var chordNote in note.AllNotes)
                {
                    if (!chordNote.IsSustain)
                    {
                        continue;
                    }

                    StartSustain(chordNote);
                }
            }
            else if (note.IsSustain)
            {
                StartSustain(note);
            }

            WasNoteGhosted = false;

            OnNoteHit?.Invoke(NoteIndex, note);
            base.HitNote(note);
        }

        protected override void MissNote(GuitarNote note)
        {
            if (note.WasHit || note.WasMissed)
            {
                YargLogger.LogFormatTrace("Tried to hit/miss note twice (Fret: {0}, Index: {1}, Hit: {2}, Missed: {3})", note.Fret, NoteIndex, note.WasHit, note.WasMissed);
                return;
            }

            note.SetMissState(true, true);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note.IsSoloEnd)
            {
                EndSolo();
            }

            if (note.IsSoloStart)
            {
                StartSolo();
            }

            WasNoteGhosted = false;

            ResetCombo();

            UpdateMultiplier();

            OnNoteMissed?.Invoke(NoteIndex, note);
            base.MissNote(note);
        }

        protected override void AddScore(GuitarNote note)
        {
            int notePoints = POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
            EngineStats.NoteScore += notePoints;
            AddScore(notePoints);
        }

        protected override void UpdateMultiplier()
        {
            int previousMultiplier = EngineStats.ScoreMultiplier;
            base.UpdateMultiplier();
            int newMultiplier = EngineStats.ScoreMultiplier;

            // Rebase sustains when the multiplier changes so that
            // there aren't huge jumps in points on extended sustains
            if (newMultiplier != previousMultiplier)
            {
                // Temporarily reset multiplier to calculate score correctly
                EngineStats.ScoreMultiplier = previousMultiplier;
                RebaseSustains(CurrentTick);
                EngineStats.ScoreMultiplier = newMultiplier;
            }
        }

        protected override bool ActiveLaneIncludesNote(int mask)
        {
            if (!IsLaneActive)
            {
                return false;
            }

            if (MaskIsMultiFret(RequiredLaneNote)) // Active lane is chord tremolo
            {
                if (mask == RequiredLaneNote)
                {
                    // All frets held are an exact match
                    return true;
                }
            }
            else // Active lane is a single fret
            {
                var heldMSB = GetMostSignificantBit(mask);
                if (heldMSB == GetMostSignificantBit(RequiredLaneNote) || (NextTrillNote != -1 && heldMSB == GetMostSignificantBit(NextTrillNote)))
                {
                    // The right-most held fret matches the active lane
                    return true;
                }
            }

            return false;
        }

        public override void SetSpeed(double speed)
        {
            base.SetSpeed(speed);
            HopoLeniencyTimer.SetSpeed(speed);
            StrumLeniencyTimer.SetSpeed(speed);
        }

        protected sealed override int CalculateBaseScore()
        {
            int score = 0;
            foreach (var note in Notes)
            {
                score += POINTS_PER_NOTE * (1 + note.ChildNotes.Count);
                score += (int) Math.Ceiling(note.TickLength / TicksPerSustainPoint);

                // If a note is disjoint, each sustain is counted separately.
                if (note.IsDisjoint)
                {
                    foreach (var child in note.ChildNotes)
                    {
                        score += (int) Math.Ceiling(child.TickLength / TicksPerSustainPoint);
                    }
                }
            }

            return score;
        }

        protected void ToggleFret(int fret, bool active)
        {
            InputButtonMask = (ushort) (active ? InputButtonMask | (1 << fret) : InputButtonMask & ~(1 << fret));

            // What we're doing here is transposing bits 10-14 of the input mask down to 0-4 of the effective mask
            // used elsewhere so that solo buttons are treated as if they were regular buttons
            byte soloButtonMask = (byte) (InputButtonMask >> 10);

            // EffectiveButtonMask is the bitwise or of the low 8 bits of InputButtonMask and soloButtonMask
            EffectiveButtonMask = (byte) (InputButtonMask | soloButtonMask);
            StandardButtonHeld = (InputButtonMask & ~OPEN_MASK & ~SOLO_MASK) > 0;
        }

        public bool IsFretHeld(GuitarAction fret)
        {
            return (EffectiveButtonMask & (1 << (int) fret)) != 0;
        }
        
        protected int GetLaneMask()
        {
            var laneMask = ButtonMask;

            foreach(var sustain in ActiveSustains)
            {
                // Remove any frets from disjointed sustains
                laneMask &= (byte) ~sustain.Note.NoteMask;
            }

            if ((RequiredLaneNote & OPEN_MASK) != 0 && MaskIsMultiFret(RequiredLaneNote))
            {
                // Active tremolo lane is an open chord, add open note to the final mask
                laneMask |= OPEN_MASK;
            }

            return laneMask;
        }

        protected static bool MaskIsMultiFret(int mask)
        {
            return (mask & (mask - 1)) != 0;
        }

        protected static int GetMostSignificantBit(int mask)
        {
            // Gets the most significant bit of the mask
            var msbIndex = 0;
            while (mask != 0)
            {
                mask >>= 1;
                msbIndex++;
            }

            return msbIndex;
        }

        protected static bool IsFretInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.GreenFret or
                    GuitarAction.RedFret or
                    GuitarAction.YellowFret or
                    GuitarAction.BlueFret or
                    GuitarAction.OrangeFret or
                    GuitarAction.SoloGreenFret or
                    GuitarAction.SoloRedFret or
                    GuitarAction.SoloYellowFret or
                    GuitarAction.SoloBlueFret or
                    GuitarAction.SoloOrangeFret or
                    GuitarAction.White3Fret => true,
                _ => false,
            };
        }

        protected static bool IsStrumInput(GameInput input)
        {
            return input.GetAction<GuitarAction>() switch
            {
                GuitarAction.StrumUp or
                    GuitarAction.StrumDown => true,
                _ => false,
            };
        }
    }
}
