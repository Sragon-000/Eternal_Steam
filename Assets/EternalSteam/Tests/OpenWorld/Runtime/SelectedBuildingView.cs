using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    public sealed class SelectedBuildingView
    {
        readonly VisualElement panel,upgradeControls;
        readonly Label title,operation,health,damage,range,interval,rotation,area,owner,region;
        readonly OpenWorldSandbox sandbox;
        public SelectedBuildingView(VisualElement root,OpenWorldSandbox sandbox)
        {
            this.sandbox=sandbox;panel=root.Q("selection-panel");title=root.Q<Label>("content-stats");upgradeControls=root.Q("upgrade-controls");
            var rows=root.Q("selection-stats");
            Label Row(string name){var label=new Label{name=name};label.AddToClassList("stat-row");rows.Add(label);return label;}
            operation=Row("selection-operation");health=Row("selection-health");damage=Row("selection-damage");range=Row("selection-range");
            interval=Row("selection-interval");rotation=Row("selection-rotation");area=Row("selection-area");owner=Row("selection-owner");region=Row("selection-region");
        }
        static void Show(Label row,string value){row.EnableInClassList("is-hidden",value==null);if(value!=null)row.text=value;}
        public void Refresh(BuildingInstance building,bool editing)
        {
            bool visible=!editing&&building!=null&&building.Active&&!building.Disposed;
            panel.EnableInClassList("is-hidden",!visible);if(!visible)return;
            var upgrade=building.Module<IUpgradeControl>();
            title.text=building.DisplayName+(upgrade==null?"":$" · Lv.{upgrade.Level} / {upgrade.MaximumLevel}");
            upgradeControls.EnableInClassList("is-hidden",upgrade==null);
            Show(operation,building.Operational?"가동 중":building.OperationBlock.HasFlag(OperationBlock.BaseLost)?"기지 없음 / 상실 · 비작동":"유효 범위 밖 · 비작동");
            var hp=building.Module<HealthModule>();Show(health,hp==null?null:$"체력 {hp.Current:0.#} / {hp.Maximum:0.#}");
            var weapon=building.Module<WeaponRuntime>();
            Show(damage,weapon==null?null:$"피해 {weapon.Damage:0.#}");Show(range,weapon==null?null:$"사거리 {weapon.Range:0.#}m");Show(interval,weapon==null?null:$"공격 간격 {weapon.Interval:0.##}초");
            var turn=building.Module<ITurretRotation>();Show(rotation,turn==null?null:$"회전 {turn.DegreesPerSecond:0.#}°/초");
            var coverage=building.Module<IBuildArea>();Show(area,coverage==null?null:coverage.Shape==BuildAreaShape.Square?$"가동 영역 {coverage.Radius:0.#} × {coverage.Radius:0.#}칸":$"가동 영역 반경 {coverage.Radius:0.#}m");
            string id=building.OwnerBaseId;Show(owner,sandbox.MeetingConstructionRules?"소속 기지 "+(id==null?"미지정":id.Substring(0,System.Math.Min(6,id.Length))):null);
            string location=null;
            if(building.Module<IBaseIdentity>()!=null&&sandbox.Regions!=null)foreach(var r in sandbox.Regions)if(r!=null&&r.Contains(building.Position)){location=r.DisplayName+" · 예정 자원 "+r.ResourceId;break;}
            Show(region,location);
        }
    }
}
