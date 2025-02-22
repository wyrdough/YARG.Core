using System;
using System.ComponentModel;

namespace YARG.Core.Engine
{
    /// <summary>
    /// A Coda Section (aka Big Rock Ending)
    ///
    /// During a coda section, the player can press frets or strike pads at will
    /// and can collect whatever bonus is currently available for the corresponding
    /// lane. If the player successfully plays the notes at the end of the coda
    /// section, the collected bonus score will be awarded to the player.
    /// </summary>
    public class CodaSection
    {
        // This is not the number of visible lanes on the track, this is the
        // number of notional lanes used for calculating the bonus score based
        // on fret presses or drum hits.
        // Could be 5 or 6 for Five/Six fret guitar or 1 for drums
        public int Lanes { get; private set; }

        // Last time bonus was collected for given lane
        public double[] LastCollectedTime { get; private set; }
        // Maximum bonus for one fret press or drum hit
        public int MaxLaneScore { get; private set; }

        // The total bonus that will be awarded if the BRE is successful
        public int TotalCodaBonus { get; private set; }

        public double StartTime { get; private set; }
        public double EndTime { get; private set; }

        public bool Success { get; private set; }

        private const int MAX_DRUM_SCORE  = 750;
        private const int MAX_FRET_SCORE  = 150;

        // Time taken for bonus to recharge after collection
        private const double BONUS_RECHARGE_TIME = 1.5;

        public CodaSection(int lanes, int maxScore, double startTime, double endTime)
        {
            Lanes = lanes;
            LastCollectedTime = new double[lanes];
            MaxLaneScore = maxScore;
            TotalCodaBonus = 0;
            StartTime = startTime;
            EndTime = endTime;
            // MissNote will change this if necessary
            Success = true;
        }

        // When called for drums, the default is fine, as there is only one lane
        // Five fret instruments should pass something that can be interpreted as
        // a lane index. (We could take a GuitarAction or DrumsAction here, but
        // then we'd have to be a generic for no good reason)
        public void HitLane(double time, int fret = 0)
        {
            // Discard values that don't correspond to a lane
            if (fret < 0 || fret > Lanes - 1)
            {
                return;
            }

            // Collect bonus for this lane
            int bonusScore = (int) Math.Floor((Math.Min(time - LastCollectedTime[fret], BONUS_RECHARGE_TIME) / BONUS_RECHARGE_TIME) * MaxLaneScore);
            TotalCodaBonus += bonusScore;

            LastCollectedTime[fret] = time;
        }

        public void MissNote()
        {
            Success = false;
            // TotalCodaBonus = 0;
        }
    }
}