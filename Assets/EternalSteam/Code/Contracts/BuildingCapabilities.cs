using System;
using UnityEngine;
namespace EternalSteam
{
    public interface ICampaignMainLevel {int MainLevel {get;} bool TryUpgrade(int expectedLevel);}
    public interface ICombatPermission {bool AllowsCombat {get;}}
    public static class CombatPermission {public static bool Allows(BuildingInstance building)=>building.Module<ICombatPermission>()?.AllowsCombat??true;}
    public interface IDamageModifier { float DamageMultiplier { get; } }
    public interface IHealthScaling { void SetMaximumMultiplier(float multiplier); }
    public interface ILevelProvider { int Level { get; } }
    public interface IUpgradeControl : ILevelProvider
    {
        int MaximumLevel { get; }
        bool TryUpgrade(out string reason);
    }
    public interface IAttackControl
    {
        float Range { get; }
        float Angle { get; }
        TargetKind Kinds { get; set; }
    }
    // Capability of a building that establishes the faction's level limit.
    public interface ILevelAuthority { }
    public interface IDefeatCondition
    {
        bool Defeated { get; }
        event Action Lost;
    }
    public interface ILevelLimit
    {
        int LevelCap { get; }
        bool IsExempt(BuildingInstance building);
    }
    public interface IBaseObjective : IDefeatCondition, ILevelLimit
    {
        void Register(BuildingInstance building);
        void Unregister(BuildingInstance building);
        void ReportDestroyed();
    }
    public interface IResourceBank
    {
        double Amount(string id);
        double Capacity(string id);
        void AddCapacity(string id,double value);
        bool Exchange(string input,double cost,string output,double gain);
    }
    public interface IMovementObstacles
    {
        void Register(BuildingInstance building);
        void Unregister(BuildingInstance building);
        BuildingInstance At(Vector3 point);
    }
}
