using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Core.Chart.AutoIntensity
{
    public partial class AutoIntensity
    {
        // Curve so that 1.25x speed = intensity increased by 1
        // This scale is 0.0-14.0/20.0 for setlist/customs
        public const double CURVE_FINAL_MULT = 3.1;

        public const double GLOBAL_COEFF = 1.4;
        public const double CURVE_LEN_COEFF = 5;

        // Based on default engine
        public const double HIT_WINDOW_SIZE = 0.14;
        public const double STRUM_FRET_LENIENCY = 0.05;
        public const double STRUM_NOTE_LENIENCY = 0.025;

        // For terminating the loop in set_leniencies
        public const double HIT_WINDOW_NOISE = 0.005;

        // Least min_pass_intensity possible: intensity 0
        public const double MIN_CAPABILITY = 0.8;

        // When missing a HOPO, probability that player strums to recover
        public const double HOPO_RECOVERY = 0.4;

        // The amount of greater intensity a player can operate at a time
        public const double SKILL_HEATSINK_MAX = 0;

        // Separation of left and right hand in get_intensity
        // HOPOs to strums = intensity increased by 1
        public const double HAND_INDEPENDENCE = 1.4;

        // Based on assumption: playing same song twice = ? increase in difficulty
        public const double ENDURANCE_CURVE = 0.0;

        public const double EPSILON = 1e-7; // 10**(-7) in Python

        // Based on whether a strum is required
        public static readonly Dictionary<Forcing, bool> FORCING_TO_RH_ACTIONS = new Dictionary<Forcing, bool>
        {
            { Forcing.STRUM, true },
            { Forcing.HOPO, false },
            { Forcing.TAP, false },
        };

        // Based roughly on subsequent multiplications by 0.75
        public static readonly Dictionary<Diff, int> DIFF_TO_ROCK_METER_SIZE = new Dictionary<Diff, int>
        {
            { Diff.EASY, 42 }, // 21,
            { Diff.MEDIUM, 42 }, // 28,
            { Diff.HARD, 42 }, // 37,
            { Diff.EXPERT, 42 } // 50
        };

        // MIDI to lanes
        public static readonly List<int> MID_TO_LANES = new List<int> { 126, 127 };

        enum ChartDifficulties
        {
            GuitarEasy   = Diff.EASY,
            GuitarMedium = Diff.MEDIUM,
            GuitarHard   = Diff.HARD,
            GuitarExpert = Diff.EXPERT,
            BassEasy     = Diff.EASY,
            BassMedium   = Diff.MEDIUM,
            BassHard     = Diff.HARD,
            BassExpert   = Diff.EXPERT,
            RhythmEasy   = Diff.EASY,
            RhythmMedium = Diff.MEDIUM,
            RhythmHard   = Diff.HARD,
            RhythmExpert = Diff.EXPERT
        }

        // Verified to work
        public static double HarmonicSum(int n)
        {
            double sum = 0;
            int rounds = n + 1;
            for (int i = 1; i < rounds; i++) {
                sum += 1.0 / i;
            }
            return sum;
        }

        public static int CountFrets(int shape) => PopCount((uint) shape);

        // Ripped from newer .net software fallback
        public static int PopCount(uint value)
        {
            const uint c1 = 0x_55555555u;
            const uint c2 = 0x_33333333u;
            const uint c3 = 0x_0F0F0F0Fu;
            const uint c4 = 0x_01010101u;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (((value + (value >> 4)) & c3) * c4) >> 24;

            return (int)value;
        }
        public enum Diff
        {
            EASY,
            MEDIUM,
            HARD,
            EXPERT
        }

        public enum Forcing
        {
            STRUM,
            HOPO,
            TAP
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class TypeValueAttribute : Attribute
    {
        public Type Value { get; }

        public TypeValueAttribute(Type value)
        {
            Value = value;
        }
    }

    public enum NoteTypes
    {
        [TypeValue(typeof(GuitarNote))]
        GuitarNoteValue,
        [TypeValue(typeof(DrumNote))]
        DrumNoteValue,
        [TypeValue(typeof(ProKeysNote))]
        ProKeysNoteValue,
        [TypeValue(typeof(ProGuitarNote))]
        ProGuitarNoteValue,
    }

    public static class NoteTypeEnumExtensions
    {
        public static Type GetTypeValue(this NoteTypes enumValue)
        {
            var typeInfo = enumValue.GetType().GetField(enumValue.ToString());
            var attribute = typeInfo.GetCustomAttributes(typeof(TypeValueAttribute), false).FirstOrDefault() as TypeValueAttribute;

            return attribute.Value;
        }
    }
}