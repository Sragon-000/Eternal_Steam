using System;
namespace EternalSteam
{
    public enum DayPhase { Day, Night }

    public sealed class GameClock
    {
        readonly double daySeconds,nightSeconds;
        [Saved(0)] double elapsed;
        [Saved(1)] public long Day {get;private set;}=1;
        public DayPhase Phase=>elapsed<daySeconds?DayPhase.Day:DayPhase.Night;
        public double RemainingSeconds=>Phase==DayPhase.Day?daySeconds-elapsed:daySeconds+nightSeconds-elapsed;
        public double PhaseProgress=>Phase==DayPhase.Day?elapsed/daySeconds:(elapsed-daySeconds)/nightSeconds;
        public bool Paused {get;set;}
        public GameClock(double daySeconds,double nightSeconds)
        {
            if(!double.IsFinite(daySeconds)||!double.IsFinite(nightSeconds)||daySeconds<1||nightSeconds<1||!double.IsFinite(daySeconds+nightSeconds))throw new ArgumentOutOfRangeException(nameof(daySeconds));
            this.daySeconds=daySeconds;this.nightSeconds=nightSeconds;
        }
        public void Tick(double seconds)
        {
            if(Paused||!double.IsFinite(seconds)||seconds<=0)return;
            double cycle=daySeconds+nightSeconds;
            double total=elapsed+seconds;
            if(!double.IsFinite(total))return;
            double days=Math.Floor(total/cycle);
            Day=days>=long.MaxValue-Day?long.MaxValue:Day+(long)days;
            elapsed=total%cycle;
        }
        // Developer phase changes keep the date and restart the selected phase.
        public void SetPhase(DayPhase phase)=>elapsed=phase==DayPhase.Day?0:daySeconds;
    }
}
