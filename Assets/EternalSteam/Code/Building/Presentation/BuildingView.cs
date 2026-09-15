using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public sealed class BuildingView : MonoBehaviour
    {
        BuildingInstance instance;
        Renderer[] surfaces;
        MaterialPropertyBlock properties;
        Transform attackPivot;
        LineRenderer outline, sector, shot;
        float shotTime;
        public int Id => instance?.Id ?? 0;
        public void Configure(BuildingInstance building, float cellSize, Material lineMaterial)
        {
            instance = building;
            properties = new MaterialPropertyBlock();
            attackPivot = transform.Find("AttackPivot");
            surfaces = GetComponentsInChildren<Renderer>();
            outline = MakeLine("Footprint", transform, lineMaterial, 0.06f);
            outline.positionCount = 5;
            var half = new Vector3(building.Footprint.x, 0, building.Footprint.y) * cellSize * 0.5f;
            var p = building.Position + Vector3.up * 0.1f;
            outline.SetPositions(new[] { p + new Vector3(-half.x,0,-half.z), p + new Vector3(-half.x,0,half.z), p + new Vector3(half.x,0,half.z), p + new Vector3(half.x,0,-half.z), p + new Vector3(-half.x,0,-half.z) });
            sector = MakeLine("Attack sector", transform, lineMaterial, 0.06f);
            shot = MakeLine("Shot", transform, lineMaterial, 0.07f);
            shot.positionCount = 2;
            shot.enabled = false;
            building.Shot += ShowShot;
            building.Projectile += ShowProjectile;
            SetState(false, false);
        }
        void ShowProjectile(Vector3 position)
        {
            shot.SetPosition(0, position-Vector3.forward*0.2f);
            shot.SetPosition(1, position+Vector3.forward*0.2f);
            shotTime=0.05f; shot.enabled=true;
        }
        void ShowShot(Vector3 position)
        {
            shot.SetPosition(0, instance.Position + Vector3.up);
            shot.SetPosition(1, position);
            shotTime = 0.09f;
            shot.enabled = true;
        }
        void Update()
        {
            if (shot == null) return;
            shotTime -= Time.deltaTime;
            shot.enabled = shotTime > 0;
        }
        public void SetState(bool selected, bool recovering)
        {
            Color tint = recovering ? new Color(1, 0.55f, 0.16f, 0.4f) : new Color(0.35f, 0.78f, 0.85f, 1);
            foreach (var surface in surfaces)
            {
                surface.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", tint);
                surface.SetPropertyBlock(properties);
            }
            outline.startColor = outline.endColor = recovering ? new Color(1, 0.5f, 0.1f) : selected ? Color.white : new Color(0.2f, 0.5f, 0.55f);
            outline.widthMultiplier = selected || recovering ? 0.1f : 0.035f;
        }
        public void ShowRange(bool visible, Vector3 direction)
        {
            if (attackPivot != null) attackPivot.rotation = Quaternion.LookRotation(instance.Direction);
            var attack = instance.Module<IAttackControl>();
            sector.enabled = visible && attack != null;
            if (!sector.enabled) return;
            sector.positionCount = 35;
            var origin = instance.Position + Vector3.up * 0.15f;
            sector.SetPosition(0, origin);
            for (int i = 0; i <= 32; i++)
                sector.SetPosition(i + 1, origin + Quaternion.AngleAxis(Mathf.Lerp(-attack.Angle * 0.5f, attack.Angle * 0.5f, i / 32f), Vector3.up) * direction * attack.Range);
            sector.SetPosition(34, origin);
        }
        void OnDestroy() { if (instance != null) { instance.Shot -= ShowShot; instance.Projectile -= ShowProjectile; } }
        public static LineRenderer MakeLine(string name, Transform parent, Material material, float width)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.widthMultiplier = width;
            line.startColor = line.endColor = Color.cyan;
            return line;
        }
    }

    public sealed class UnityBuildingFactory : IBuildingFactory
    {
        readonly Transform parent;
        readonly BuildingServices services;
        readonly Material lineMaterial;
        readonly float cellSize;
        readonly Transform staging;
        public readonly Dictionary<int, BuildingView> Views = new();
        public UnityBuildingFactory(Transform parent, BuildingServices services, float cellSize, Material lineMaterial)
        {
            this.parent = parent; this.services = services; this.cellSize = cellSize; this.lineMaterial = lineMaterial;
            var root = new GameObject("Inactive staging"); root.transform.SetParent(parent); root.SetActive(false); staging = root.transform;
        }
        public BuildingInstance Stage(int id, PlacementRequest request, Vector3 position)
        {
            BuildingInstance building = null; GameObject view = null;
            try
            {
                building = new BuildingInstance(id, request.Definition, request.Cell, position, services);
                view = Object.Instantiate(request.Definition.ViewPrefab, staging);
                view.SetActive(false);
                view.transform.position = position;
                var component = view.AddComponent<BuildingView>();
                component.Configure(building, cellSize, lineMaterial);
                Views.Add(id, component);
                return building;
            }
            catch
            {
                building?.Dispose();
                if (view != null) Object.Destroy(view);
                throw;
            }
        }
        public void Activate(BuildingInstance building)
        {
            var view = Views[building.Id];
            view.transform.SetParent(parent, true);
            view.gameObject.SetActive(true);
        }
        public void Remove(BuildingInstance building)
        {
            if (!Views.Remove(building.Id, out var view)) return;
            view.gameObject.SetActive(false);
            Object.Destroy(view.gameObject);
        }
    }
}
