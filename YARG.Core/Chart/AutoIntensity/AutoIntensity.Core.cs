using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Core.Chart.AutoIntensity
{
    public partial class AutoIntensity
    {
        public static void SetVels(List<Chord> chords)
        {
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 1)
                {
                    // The first chord has no local velocity
                    chords[i].Vel = 0.0;
                }
                else
                {
                    // Velocity = the change in time
                    chords[i].SetVel(chords[i - 1].Time);
                }
            }
        }

        public static void SetAccs(List<Chord> chords)
        {
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 2)
                {
                    // The first two chords have no local acceleration
                    chords[i].Acc = 0.0;
                }
                else
                {
                    // Acceleration = the change in velocity
                    chords[i].SetAcc(chords[i - 1].Vel.Value);
                }
            }
        }

        public static void SetLeniencies(List<Chord> chords)
        {
            double radius = HIT_WINDOW_SIZE / 2;
            double strumFactor = radius - STRUM_NOTE_LENIENCY;
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 1)
                {
                    chords[i].SetLeniency(0.0);
                }
                else if (i < 2)
                {
                    chords[i].SetLeniency(2 * radius -
                        strumFactor * (chords[i - 1].RhActions.HasValue ? chords[i - 1].RhActions.Value : 0));
                }
                else
                {
                    double diff = chords[i].Time - chords[i - 1].Time;
                    double lowerBound = diff;
                    double upperBound = diff + 2 * radius -
                        strumFactor * (chords[i - 1].RhActions.HasValue ? chords[i - 1].RhActions.Value : 0);
                    int j = i - 2;
                    double leniency = -1;
                    while (j > 0 && upperBound - lowerBound > HIT_WINDOW_NOISE)
                    {
                        double newLower = (chords[i].Time - chords[j].Time) / (i - j);
                        double newUpper = (chords[i].Time - chords[j].Time + 2 * radius -
                            strumFactor * (chords[j].RhActions.HasValue ? chords[j].RhActions.Value : 0)) / (i - j);
                        if (newLower > upperBound)
                        {
                            leniency = upperBound;
                            break;
                        }

                        if (newUpper < lowerBound)
                        {
                            leniency = lowerBound;
                            break;
                        }

                        upperBound = Math.Min(upperBound, newUpper);
                        lowerBound = Math.Max(lowerBound, newLower);
                        j--;
                    }

                    if (leniency < 0)
                    {
                        if (j == 0)
                        {
                            leniency = upperBound;
                        }
                        else
                        {
                            leniency = lowerBound;
                        }
                    }

                    leniency -= diff;
                    chords[i].SetLeniency(leniency);
                }
            }
        }

        public static void SetLhActions(List<Chord> chords)
        {
            List<int> frettingIndices = new List<int>
            {
                0,
                0
            };
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 1)
                {
                    chords[i].LhComplexity = 0.0;
                }
                else
                {
                    if (chords[i - 1].Shape != chords[i].Shape)
                    {
                        frettingIndices.Add(i - 1);
                        frettingIndices.RemoveAt(0);
                    }

                    List<Chord> prevChords = new List<Chord>()
                    {
                        chords[frettingIndices.Last()],
                        chords[frettingIndices.ElementAt(frettingIndices.Count - 2)]
                    };
                    List<int> prevShapes = new();
                    List<double> prevTimes = new();
                    foreach (var chord in prevChords)
                    {
                        prevShapes.Add(chord.Shape);
                        prevTimes.Add(chord.Time);
                    }

                    chords[i].SetPresses(prevShapes);
                    chords[i].SetLifts(prevShapes);
                    chords[i].SetLhVel(prevTimes);
                    chords[i].SetLhActions();
                }
            }
        }

        public static void SetRhActions(List<Chord> chords)
        {
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 1)
                {
                    chords[i].RhComplexity = 0.0;
                    chords[i].RhActions = 1;
                    chords[i].OverstrumProb = 1;
                }
                else
                {
                    chords[i].SetRhActions(chords[i - 1].Shape);
                    if (i == chords.Count - 1)
                    {
                        chords[i].OverstrumProb = chords[i].RhActions;
                    }
                    else
                    {
                        chords[i].SetOverstrumProb(chords[i + 1].Shape, chords[i + 1].Time);
                    }
                }
            }
        }

        public static void SetRhVels(List<Chord> chords)
        {
            List<int> strumIndices = new List<int>
            {
                0,
                0
            };
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 1)
                {
                    chords[i].RhComplexity = 0.0;
                }
                else
                {
                    List<Chord> prevChords = new List<Chord>()
                    {
                        chords[strumIndices.Last()],
                        chords[strumIndices.ElementAt(strumIndices.Count - 2)]
                    };
                    List<double> prevTimes = prevChords.Select(chord => chord.Time).ToList();
                    chords[i].SetRhVel(prevTimes);
                    if (chords[i].RhActions == 1)
                    {
                        strumIndices.Add(i);
                        strumIndices.RemoveAt(0);
                    }
                }
            }
        }

        public static void SetAnchoredShapesAndCounts(List<Chord> chords)
        {
            for (int i = 0; i < chords.Count; i++)
            {
                if (i < 1)
                {
                    // No frets anchored
                    chords[i].AnchoredShape = 0;
                }
                else
                {
                    chords[i].SetAnchoredShapeAndCount(chords[i - 1].AnchorableShape);
                }
            }
        }

        public static void PrintName(string name, bool isLastChart)
        {
            Console.WriteLine($"{(isLastChart ? '└' : '├')}── {name}");
        }

        public static void PrintStat(string stat, string value, bool isLastChart, bool isLastStat)
        {
            Console.WriteLine($"{(isLastChart ? ' ' : '│')}   {(isLastStat ? '└' : '├')}── {stat,-20}{value}");
        }

        public static void PrepareChartForStatCollection(Chart chart)
        {
            List<Chord> chords = chart.Chords;

            SetVels(chords);
            SetAccs(chords);
            SetAnchoredShapesAndCounts(chords);
            SetRhActions(chords);
            SetLeniencies(chords);
            SetRhVels(chords);
            SetLhActions(chords);
        }

        public static bool SimulateRun(List<double> intensities, List<Forcing> chordActions,
            List<double> overstrumProbs, int meter, double start, double capability)
        {
            double meterLevel = start;
            double skillHeatsinkMax = capability * SKILL_HEATSINK_MAX;
            double skillHeatsink = skillHeatsinkMax;
            bool chartPassed = true;
            double prevHitProb = 1;
            for (int i = 0; i < intensities.Count; i++)
            {
                double intensity = intensities[i];
                if (skillHeatsink > 0)
                {
                    skillHeatsink = Math.Min(skillHeatsinkMax, skillHeatsink + capability - intensity);
                }
                else
                {
                    skillHeatsink += capability;
                }

                double hitProb = Math.Min(capability / intensity, 1);
                double overstrumProb = overstrumProbs[i];
                double ePrevHit, ePrevMiss;
                if (chordActions[i] == Forcing.STRUM)
                {
                    ePrevHit = hitProb * 0.25 - (1 - hitProb) * (1 + overstrumProb);
                    ePrevMiss = hitProb * 0.25 - (1 - hitProb) * (1 + overstrumProb);
                }
                else if (chordActions[i] == Forcing.HOPO)
                {
                    ePrevHit = hitProb * 0.25 - (1 - hitProb);
                    ePrevMiss = HOPO_RECOVERY * (hitProb * 0.25 - (1 - hitProb) * (1 + overstrumProb)) -
                        (1 - HOPO_RECOVERY);
                }
                else if (chordActions[i] == Forcing.TAP)
                {
                    ePrevHit = hitProb * 0.25 - (1 - hitProb);
                    ePrevMiss = hitProb * 0.25 - (1 - hitProb);
                }
                else
                {
                    throw new ArgumentException("Invalid Forcing Type");
                }

                double expectedChange = prevHitProb * ePrevHit + (1 - prevHitProb) * ePrevMiss;
                meterLevel = Math.Min(meter, meterLevel + expectedChange);
                prevHitProb = hitProb;
                if (meterLevel < 0)
                {
                    chartPassed = false;
                    break;
                }
            }

            return chartPassed;
        }

        public static (double, bool) FindMinPassIntensity(List<double> intensities, List<Forcing> chordActions,
            List<double> overstrumProbs, int meter, double start)
        {
            if (intensities.Count == 0)
            {
                return (MIN_CAPABILITY, false);
            }

            double left = MIN_CAPABILITY;
            double right = intensities.Max();

            while (right - left > 0.005)
            {
                double capability = (left + right) / 2;
                bool chartPassed = SimulateRun(intensities, chordActions, overstrumProbs, meter, start, capability);
                if (chartPassed)
                {
                    right = capability;
                }
                else
                {
                    left = capability;
                }
            }

            // Can a player 2 intensity level higher (minimum 5) play this chart competently? If not, put asterisk
            bool asterisk = !SimulateRun(intensities, chordActions, overstrumProbs,
                meter / 8, start / 8,
                Math.Max(Math.Pow(2, 5 / CURVE_FINAL_MULT), right * Math.Pow(2, 2 / CURVE_FINAL_MULT)));
            return (right, asterisk);
        }

        public static string CalculateChartStats(Chart chart, bool isLast, bool printResults)
        {
            List<Chord> chords = chart.Chords;
            int n = chords.Count;

            List<double> localIntensities = chords.Skip(1).Select(chord => chord.GetIntensity()).ToList();
            List<Forcing> chordActions = chords.Skip(1)
                .Select(chord => chord.RhActions == 1 ? Forcing.STRUM : chord.Forcing).ToList();
            List<double> overstrumProbs = chords.Skip(1).Select(chord => chord.OverstrumProb.Value).ToList();

            (double minPassIntensity, bool asterisk) = FindMinPassIntensity(localIntensities, chordActions,
                overstrumProbs,
                DIFF_TO_ROCK_METER_SIZE[chart.Diff], 5 * DIFF_TO_ROCK_METER_SIZE[chart.Diff] / 6);

            // Make chart intensity agnostic with respect to the rock meter size
            // chart_intensity = CURVE_FINAL_MULT * math.log(relative_intensity_max / sample_size, 2)
            double chartIntensity = CURVE_FINAL_MULT * Math.Max(0, Math.Log(minPassIntensity / MIN_CAPABILITY, 2));

            if (printResults)
            {
                PrintName(chart.Name, isLast);
                PrintStat("note count", $"{n:F2} n", isLast, false);
                PrintStat("intensity", $"{chartIntensity:F2}" + (asterisk ? "*" : ""), isLast, true);
            }

            return $"{chartIntensity:F2}";
        }

        public static Dictionary<string, string> CalculateAllChartStats(List<Chart> charts)
        {
            // Console.WriteLine("CHART STATISTICS");
            Dictionary<string, string> results = new Dictionary<string, string>();
            for (int i = 0; i < charts.Count; i++)
            {
                PrepareChartForStatCollection(charts[i]);
                results[charts[i].Name] = CalculateChartStats(charts[i], i == charts.Count - 1, false);
            }

            return results;
        }
    }
}