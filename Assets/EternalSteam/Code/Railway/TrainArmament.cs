using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.Railway
{
    // Initial integration policy, not final economy balance. Older v2 saves have no mount.
    [Serializable] public sealed class TrainArmament
    {
        public Vector2Int footprint=new(2,2);
        public string definition="";
        public double battery;
        public List<SavedState> modules=new();
        public const double Capacity=200,InstallIron=10,UpgradeIronPerLevel=5;
        public static bool ValidFootprint(Vector2Int size)=>size==new Vector2Int(2,2)||size==new Vector2Int(1,3)||size==new Vector2Int(3,1);
        public static void Validate(TrainArmament a)
        {
            if(a==null)return; // missing pre-P6 field means an empty 2x2 mount
            if(!ValidFootprint(a.footprint)||!double.IsFinite(a.battery)||a.battery<0||a.battery>Capacity||a.modules==null||a.modules.Count>3||a.modules.Exists(m=>m==null)||
               (string.IsNullOrEmpty(a.definition)&&a.modules.Count!=0)||(!string.IsNullOrEmpty(a.definition)&&string.IsNullOrWhiteSpace(a.definition)))throw new ArgumentException("기차 방어칸 저장 오류");
        }
    }
}
