using System.Collections.Generic;
using YARG.Core.Engine.Vocals;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {

        public class Band
        {
            private List<EngineContainer> Engines { get; set; }         = new();
            public  int                   Score   { get; private set; } = 0;
            private int                   _codaParticipants = 0;
            private int                   _codaSuccesses    = 0;
            private int                   _starpowerCount   = 0;

            public void AddEngine(EngineContainer engine)
            {
                Engines.Add(engine);

                engine.Engine.OnCodaEnd += OnCodaEnd;
                engine.Engine.OnStarPowerStatus += OnStarPowerStatus;
                if (engine.Engine is not VocalsEngine)
                {
                    _codaParticipants++;
                }
            }

            private void AwardCodaBonus()
            {
                foreach (var engine in Engines)
                {
                    engine.AwardCodaBonus();
                }
            }

            private void OnStarPowerStatus(bool active)
            {
                if (active)
                {
                    _starpowerCount++;
                }
                else
                {
                    _starpowerCount--;
                }

                UpdateBandMultiplier(_starpowerCount * 2);
            }

            private void UpdateBandMultiplier(int multiplier)
            {
                foreach (var engine in Engines)
                {
                    engine.UpdateBandMultiplier(multiplier);
                }
            }

            private void OnCodaEnd(CodaSection coda)
            {
                if (coda.Success)
                {
                    _codaSuccesses++;
                }

                if (_codaParticipants == _codaSuccesses)
                {
                    AwardCodaBonus();
                }
            }
        }
    }

    public partial class EngineContainer
    {
        void OnCodaEnd(CodaSection coda)
        {

        }
    }
}