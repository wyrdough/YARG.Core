using System;
using System.Collections.Generic;
using System.Linq;
using static YARG.Core.Chart.AutoIntensity.AutoIntensity;


namespace YARG.Core.Chart.AutoIntensity
{
    public class Chord
    {
        public double Time { get; set; } // The timestamp of the chord, NOT length
        public int Shape { get; set; } // Bitwise; 0b_00011_0 = GR; 0b_00000_1 = P (using int to represent bitwise)
        public Forcing Forcing { get; set; } // Strum, HOPO, or tap
        public bool Laned { get; set; } // Whether this note is in a lane

        public double? Leniency { get; set; } // Extra amount of time allowed by 140ms hit window based on previous notes
        public double? OverstrumProb { get; set; } // Heuristic probability that note is overstrummed given it is missed
        public List<double> LhVel { get; set; } = new List<double>(); // Reciprocals of delta time from previous distinct chords (includes leniency)
        public List<double> RhVel { get; set; } = new List<double>(); // Reciprocals of delta time from previous strummed chords (includes leniency)
        public double? Vel { get; set; } // Reciprocal of delta time from previous chord
        public double? Acc { get; set; } // Acceleration by delta velocity

        public List<int> Presses { get; set; } = new(); // New frets that WERE NOT in the last
        public List<int> Lifts { get; set; } = new();// Absent frets that WERE in the last
        // Not currently used
        public int? Holds { get; set; } // Present frets that WERE in the last, or NONE if identical
        public List<double> LhActions { get; set; } = new(); // Number of LH actions relative to previous distinct chords
        public int? RhActions { get; set; } // Number of RH actions

        public int AnchorableShape { get; set; } // SHAPE that you're ALLOWED to anchor
        public int AnchoredShape { get; set; } // SHAPE that MAY be anchored at this time
        public int AnchoredCount { get; set; } // COUNT of frets in anchored_shape

        public double LhComplexity { get; set; } // Composite fretting complexity
        public double RhComplexity { get; set; } // Composite strumming complexity

        public Chord(double time, int shape, Forcing forcing, bool laned)
        {
            Time = time;
            Shape = shape;
            Forcing = forcing;
            Laned = laned;

            // If a single B (0b_01000_0), then all frets under + open (0b_00111_1)
            AnchorableShape = IsSingleNote() ? (shape - 1) : shape;
        }

        public bool IsSingleNote() => PopCount((uint) Shape) == 1;

        public void SetLeniency(double leniency)
        {
            Leniency = leniency;
        }

        public void SetLhVel(List<double> prevTimes)
        {
            double laneMultiplier = 1 + (Laned ? 1 : 0);
            LhVel = new List<double>();
            foreach (double prevTime in prevTimes)
            {
                LhVel.Add(1 / (laneMultiplier * (Time - prevTime) + (Leniency ?? 0)));
            }
        }

        public void SetRhVel(List<double> prevTimes)
        {
            double laneMultiplier = 1 + (Laned ? 1 : 0);
            RhVel = new List<double>();
            foreach (double prevTime in prevTimes)
            {
                RhVel.Add(1 / (laneMultiplier * (Time - prevTime) + (Leniency ?? 0)));
            }
        }

        public void SetVel(double prevTime)
        {
            Vel = 1 / (Time - prevTime);
        }

        public void SetAcc(double prevVel)
        {
            if (Vel.HasValue)
            {
                Acc = Vel - prevVel;
            }
        }

        public void SetAnchoredShapeAndCount(int prevAnchoredShape)
        {
            AnchoredShape = Shape & prevAnchoredShape;
            AnchoredCount = CountFrets(AnchoredShape);
        }

        public void SetPresses(List<int> prevShapes)
        {
            Presses.Clear();
            foreach (int prevShape in prevShapes)
            {
                Presses.Add((Shape >> 1) & ~(prevShape >> 1));
            }
        }

        public void SetLifts(List<int> prevShapes)
        {
            Lifts.Clear();
            foreach (int prevShape in prevShapes)
            {
                Lifts.Add((prevShape >> 1) & ~(Shape >> 1));
            }
        }

        public void SetLhActions()
        {
            // Composite of presses and lifts
            LhActions.Clear();
            for(int i = 0; i < Lifts.Count; i++)
            {
                 LhActions.Add(HarmonicSum(CountFrets(Lifts[i]) + CountFrets(Presses[i])));
            }
        }

        public void SetRhActions(int prevShape)
        {
            // 1 if a strum or identical shape to previous chord
            RhActions = (FORCING_TO_RH_ACTIONS[Forcing] || (prevShape == Shape)) ? 1 : 0;
        }

        public void SetOverstrumProb(int nextShape, double nextTime)
        {
            if (nextShape != Shape)
            {
                if (Forcing == Forcing.TAP)
                {
                    OverstrumProb = 0;
                }
                else
                {
                    OverstrumProb = 1;
                }
            }
            else
            {
                double delta = nextTime - Time;
                if (delta < HIT_WINDOW_SIZE)
                {
                    OverstrumProb = 0;
                }
                else
                {
                    OverstrumProb = (delta - HIT_WINDOW_SIZE) / (delta - HIT_WINDOW_SIZE / 2);
                }
                OverstrumProb = 1;
            }
        }

        public double GetIntensity()
        {
            /*Local intensity of the current chord*/
            if (!LhVel.Any() || !LhActions.Any() || !RhVel.Any() || !RhActions.HasValue) return 0; // Handle invalid state.
            double p = HAND_INDEPENDENCE;
            double lhIntensity = 0;
            for (int i = 0; i < LhVel.Count; i++)
            {
                lhIntensity += LhVel[i] * LhActions[i];
            }
            double rhIntensity = 0;
            for(int i = 0; i < RhVel.Count; i++){
              rhIntensity += RhVel[i];
            }
            rhIntensity *= RhActions.Value;

            lhIntensity = Math.Max(lhIntensity, EPSILON);
            rhIntensity = Math.Max(rhIntensity, EPSILON);

            // Expected contribution of the chord n previous is 1/n, readjust sum accordingly
            double noteLookbackFactor = 1 / HarmonicSum(LhVel.Count);

            // Floor of a note's intensity is 1 (1 action per second; a refretting + strum takes 3 actions)
            double localIntensity = Math.Max(1, noteLookbackFactor * Math.Pow(Math.Pow(lhIntensity, p) + Math.Pow(rhIntensity, p), 1 / p)); // (lh_intensity + rh_intensity)
            return localIntensity;
        }
    }
}