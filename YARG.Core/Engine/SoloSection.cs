namespace YARG.Core.Engine
{
    public class SoloSection
    {

        public int NoteCount { get; }

        public int NotesHit { get; set; }
        
        public int SoloBonus { get; set; }

        public double StartTime { get; set; }
        public double EndTime { get; set; }

        public SoloSection(int noteCount, double startTime, double endTime)
        {
            NoteCount = noteCount;
            StartTime = startTime;
            EndTime = endTime;
        }

    }
}