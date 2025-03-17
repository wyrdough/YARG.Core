using YARG.Core.Logging;

namespace AutoIntensityCli
{
    public static class Program
    {
        public static void Main()
        {
            var runner = new IntensityRunner();
            runner.IntensityTest();
            // ConsoleUtilities.WaitForKey("Press any key to exit...");
            YargLogger.KillLogger();
        }
    }
}