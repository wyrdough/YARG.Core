using System;
using Cysharp.Text;
using YARG.Core.Extensions;

namespace YARG.Core.Engine
{
    public struct EngineTimer
    {
        private const double NOT_STARTED = double.MaxValue;

        private double _startTime;
        private double _speed;
        private double _extraTime;

        public readonly double TimeThreshold;

        public double SpeedAdjustedThreshold => TimeThreshold * _speed;

        public readonly double StartTime => _startTime;
        public readonly double EndTime => _startTime + _extraTime + TimeThreshold * _speed;

        public bool IsActive { get; private set; }

        static EngineTimer()
        {
            Utf16ValueStringBuilder.RegisterTryFormat<EngineTimer>(TryFormat);
        }

        public EngineTimer(double threshold)
        {
            _startTime = NOT_STARTED;
            _speed = 1.0;
            _extraTime = 0;

            TimeThreshold = threshold;

            IsActive = false;
        }

        public void Start(double currentTime)
        {
            Start(ref _startTime, currentTime);
            IsActive = true;
        }

        public void StartWithOffset(double currentTime, double offset)
        {
            StartWithOffset(ref _startTime, currentTime, TimeThreshold * _speed, offset);
            IsActive = true;
        }

        public void StartWithMinimum(double currentTime, double minimum)
        {
            StartWithMinimum(ref _startTime, ref _extraTime, currentTime, TimeThreshold * _speed, minimum);
            IsActive = true;
        }

        public void Disable()
        {
            IsActive = false;
        }

        public readonly bool IsExpired(double currentTime)
        {
            return currentTime >= EndTime;
        }

        public void SetSpeed(double speed)
        {
            _speed = speed;
        }

        public static void Start(ref double startTime, double currentTime)
        {
            startTime = currentTime;
        }

        public static void StartWithOffset(ref double startTime, double currentTime, double threshold, double offset)
        {
            double diff = Math.Abs(threshold - offset);
            startTime = currentTime - diff;
        }

        /// <summary>
        /// Starts a timer with a minimum duration that may be longer than the active engine preset would normally allow
        /// </summary>
        /// <param name="startTime"></param>
        /// <param name="extraTime"></param>
        /// <param name="currentTime"></param>
        /// <param name="threshold"></param>
        /// <param name="minimumDuration"></param>
        public static void StartWithMinimum(ref double startTime, ref double extraTime, double currentTime, double threshold, double minimumDuration)
        {
            startTime = currentTime;
            extraTime = Math.Max((startTime + minimumDuration) - threshold, 0);
        }

        public static void Reset(ref double startTime)
        {
            startTime = NOT_STARTED;
        }

        public readonly override string ToString()
        {
            if (StartTime == NOT_STARTED)
                return "Not started";

            return $"{StartTime:0.000000} - {EndTime:0.000000}";
        }

        public readonly bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        {
            return TryFormat(this, destination, out charsWritten, format);
        }

        private static bool TryFormat(EngineTimer timer, Span<char> dest, out int written, ReadOnlySpan<char> format)
        {
            written = 0;

            if (timer.StartTime == NOT_STARTED)
                return dest.TryWriteAndAdvance("Not started", ref written);

            if (!dest.TryWriteAndAdvance(timer.StartTime, ref written, "0.000000"))
                return false;

            if (!dest.TryWriteAndAdvance(" - ", ref written))
                return false;

            if (!dest.TryWriteAndAdvance(timer.EndTime, ref written, "0.000000"))
                return false;

            return true;
        }
    }
}
