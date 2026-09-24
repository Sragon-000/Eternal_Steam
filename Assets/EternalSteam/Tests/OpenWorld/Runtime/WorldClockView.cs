using System;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    public sealed class WorldClockView
    {
        readonly Label date,remaining;
        readonly Button pause;
        readonly DayNightDial dial;
        long lastDay=-1,lastSeconds=-1;
        DayPhase lastPhase;
        bool lastPaused,lastEditing;
        public WorldClockView(VisualElement root)
        {date=root.Q<Label>("clock-date");remaining=root.Q<Label>("clock-remaining");pause=root.Q<Button>("clock-pause");dial=root.Q<DayNightDial>("day-night-dial")??throw new InvalidOperationException("Clock dial missing from HUD UXML.");}
        public void Refresh(GameClock clock,bool editing)
        {
            dial.Turns=(float)((clock.Phase==DayPhase.Day?0:.5)+clock.PhaseProgress*.5);
            long seconds=(long)Math.Ceiling(clock.RemainingSeconds);
            if(lastDay==clock.Day&&lastSeconds==seconds&&lastPhase==clock.Phase&&lastPaused==clock.Paused&&lastEditing==editing)return;
            lastDay=clock.Day;lastSeconds=seconds;lastPhase=clock.Phase;lastPaused=clock.Paused;lastEditing=editing;
            date.text=$"{clock.Day}일차 · {(clock.Phase==DayPhase.Day?"낮":"밤")}";
            remaining.text=$"전환까지 {seconds/60:00}:{seconds%60:00}"+(editing?" · 수정 중 정지":clock.Paused?" · 시간 정지":"");
            pause.text=clock.Paused?"시간 재개":"시간 정지";
        }
    }
}
