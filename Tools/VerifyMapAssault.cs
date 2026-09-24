using System;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
using UnityEngine;
public static class VerifyMapAssault
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Main()
    {
        var e=new MapEnergyState("a",1000,3,500);
        Check(e.Extract(0,100)==100&&e.Remaining[0]==400,"Extraction debit");
        Check(e.RewardKill(799)==799,"Kill credit");Check(e.Extract(1,500)==1&&e.Remaining[1]==499,"90% clamp debit");
        Check(e.RewardKill(1)==0,"No overpayment");e.Advance(false);Check(e.Stage==MapStage.WaitingForNight,"Day boss warning");
        Check(!e.DefeatBoss(),"No early reward");e.Advance(true);Check(e.DefeatBoss()&&!e.DefeatBoss(),"Exactly once boss reward");
        Check(e.Balance==1000&&e.Earned==1000,"90 + 10");Check(!e.Craft("other")&&e.Craft("a")&&!e.Craft("a")&&e.Balance==0&&e.PerfectOrb,"Atomic map scoped craft");
        var fail=new MapEnergyState("b",1000,1,1000);fail.Fail();Check(fail.RewardKill(1)==0&&!fail.Craft("b"),"Failure freezes state");
        var planner=new NightSpawnPlanner(12,42);var valid=new bool[12];Array.Fill(valid,true);planner.BeginNight(1,100);planner.Reconcile(valid,3);
        int first=planner.Points[0];planner.Reconcile(valid,3);Check(planner.Points[0]==first,"Stable night points");
        planner.BeginNight(1,100);Check(planner.Pending==100,"No repeated developer-night budget");planner.Consume();valid[first]=false;planner.Reconcile(valid,3);
        Check(planner.Pending==99&&!Contains(planner,first),"Lost point preserves budget");Array.Fill(valid,false);planner.Reconcile(valid,8);Check(planner.Points.Count==0&&planner.Pending==99,"No valid cells defer");
        Array.Fill(valid,true);planner.Reconcile(valid,8,new[]{0,1,2,3,4,5,6,7});valid[0]=false;planner.Reconcile(valid,8,new[]{0,1,2,3,4,5,6,7});Check(planner.Points.Count==8&&!Contains(planner,0),"Safe fixed point replacement");valid[0]=true;planner.Reconcile(valid,8,new[]{0,1,2,3,4,5,6,7});Check(Contains(planner,0)&&planner.Points.Count==8,"Fixed point restored");
        var enemies=new HordeEnemyWorld(capacity:10);int rewards=0;enemies.KilledEnemy+=(id,gen)=>rewards++;enemies.TrySpawn(Vector3.zero,Vector3.one,1,1);enemies.ApplyDamage(0,1);enemies.ApplyDamage(0,1);Check(rewards==1,"Duplicate damage kill");enemies.Reset();Check(rewards==1,"Reset is not kill");
        Debug.Log("PASS: energy clamp/depletion, boss/night/craft/failure, point stability/replacement/budget, kill event");
    }
    static bool Contains(NightSpawnPlanner p,int cell){foreach(int i in p.Points)if(i==cell)return true;return false;}
}
