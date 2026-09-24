using System;
using System.Collections.Generic;
using UnityEngine;
using static EternalSteam.Demo.HordeVisualPrimitives;
namespace EternalSteam.Demo
{
    public interface IHordeTowerFactory : IBuildingFactory
    {
        GameObject Template { get; }
        void ConfigureRequest(int requestId,float range,HordeTowerKind kind);
        void ForgetRequest(int requestId);
    }
    public sealed class HordeTowerFactory : IHordeTowerFactory, IDisposable
    {
        readonly Transform parent,staging;
        readonly List<HordeTower> towers;
        readonly Func<HordeMapKind> map;
        readonly Material barrelMaterial,tracerMaterial,validMaterial;
        readonly Material[] typeMaterials,effectMaterials;
        readonly Dictionary<int,(float range,HordeTowerKind kind)> requests=new();
        readonly Dictionary<int,HordeTower> staged=new();
        readonly Dictionary<int,HordeTower> existing=new();
        public void ConfigureExistingRequest(int id,HordeTower view)=>existing.Add(id,view);
        static void DestroyView(GameObject view) { if(Application.isPlaying) UnityEngine.Object.Destroy(view); else UnityEngine.Object.DestroyImmediate(view); }
        public HordeTower CreateAuthoredView(Vector3 position,Vector3 direction,float range,HordeTowerKind kind)
        { var view=Create(position,direction,range,kind,map());view.root.transform.SetParent(parent,true);view.root.SetActive(true);return view; }
        public GameObject ViewOf(BuildingInstance building) => staged.TryGetValue(building.Id,out var tower) ? tower.root : null;
        public GameObject Template { get; }
        public HordeTowerFactory(Transform parent,List<HordeTower> towers,Func<HordeMapKind> map,Material barrel,Material tracer,Material valid,Material[] types,Material[] effects)
        {
            this.parent=parent;this.towers=towers;this.map=map;barrelMaterial=barrel;tracerMaterial=tracer;validMaterial=valid;typeMaterials=types;effectMaterials=effects;
            var container=new GameObject("Legacy building staging");container.transform.SetParent(parent,false);container.SetActive(false);staging=container.transform;
            Template=new GameObject("Procedural tower template");Template.transform.SetParent(staging,false);Template.SetActive(false);
        }
        public void ConfigureRequest(int requestId,float range,HordeTowerKind kind)=>requests.Add(requestId,(range,kind));
        public void ForgetRequest(int requestId) { requests.Remove(requestId);existing.Remove(requestId); }
        public BuildingInstance Stage(int id,PlacementRequest request,Vector3 position)
        {
            var settings=requests[request.Id];
            BuildingInstance building=null;HordeTower tower=null;
            try
            {
                building=new BuildingInstance(id,request.Definition,request.Cell,position,new BuildingServices(null));
                tower=existing.TryGetValue(request.Id,out var authored) ? authored : Create(position,request.Direction,settings.range,settings.kind,map());
                tower.building=building;staged.Add(id,tower);return building;
            }
            catch { building?.Dispose();if(tower?.root!=null) DestroyView(tower.root);throw; }
        }
        HordeTower Create(Vector3 position,Vector3 direction,float distance,HordeTowerKind kind,HordeMapKind mapKind)
        {
            GameObject created=null;
            try
            {
            var root = UnityEngine.Object.Instantiate(Template, staging);
            created=root;
            root.name = HordeTowerStats.Name(kind) + " " + (towers.Count + 1);
            root.transform.position = position + Vector3.up * HordeMapLayout.SurfaceHeight(mapKind);
            root.transform.localScale = Vector3.one * HordeMapLayout.TowerScale;
            MakePart("Base", root.transform, new Vector3(0, 0.25f, 0), new Vector3(1.35f, 0.5f, 1.35f), barrelMaterial);
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0, 0.9f, 0);
            head.rotation = Quaternion.LookRotation(direction.normalized);
            MakePart("Cube", head, Vector3.zero, new Vector3(1, 0.8f, 1), typeMaterials[(int)kind]);
            var barrelScale = kind == HordeTowerKind.Cannon ? new Vector3(0.55f, 0.55f, 1.3f) : kind == HordeTowerKind.Frost ? new Vector3(0.65f, 0.2f, 0.7f) : new Vector3(0.28f, 0.28f, 1.1f);
            MakePart("Barrel", head, new Vector3(0, 0, 0.8f), barrelScale, barrelMaterial);
            var tracer = MakeLine("Weapon tracer", root.transform, kind == HordeTowerKind.MachineGun ? tracerMaterial : effectMaterials[(int)kind], kind == HordeTowerKind.MachineGun ? 0.065f : 0.14f, 2);
            tracer.startColor = tracer.endColor = HordeTowerStats.Color(kind);
            tracer.enabled = false;
            LineRenderer impact = null;
            if (kind == HordeTowerKind.Cannon || kind == HordeTowerKind.Frost)
            {
                impact = MakeLine("Area effect", root.transform, effectMaterials[(int)kind], 0.12f, kind == HordeTowerKind.Frost ? 35 : 33);
                impact.startColor = impact.endColor = HordeTowerStats.Color(kind);
                impact.enabled = false;
            }
            var sight = MakeLine("Fixed direction", root.transform, validMaterial, 0.045f, 4);
            Vector3 forward = head.forward;
            Vector3 right = head.right;
            Vector3 tip = position + forward * 3 + Vector3.up * 0.08f;
            sight.SetPosition(0, tip - forward * 0.65f - right * 0.4f);
            sight.SetPosition(1, tip);
            sight.SetPosition(2, tip - forward * 0.65f + right * 0.4f);
            sight.SetPosition(3, tip);
            var coverage = MakeLine("Deployment firing range", root.transform, effectMaterials[(int)kind], 0.14f, 35);
            Vector3 coverageOrigin = position + Vector3.up * (HordeMapLayout.SurfaceHeight(mapKind) + 0.09f);
            float halfAngle = HordeDeploymentGeometry.SpreadHalfAngle(distance);
            coverage.SetPosition(0, coverageOrigin);
            for (int i = 0; i <= 32; i++)
                coverage.SetPosition(i + 1, coverageOrigin + Quaternion.AngleAxis(Mathf.Lerp(-halfAngle, halfAngle, i / 32f), Vector3.up) * head.forward * distance);
            coverage.SetPosition(34, coverageOrigin);
            coverage.enabled = true;

            return new HordeTower { root=root,head=head,tracer=tracer,impact=impact,coverage=coverage,kind=kind,range=distance,halfAngle=halfAngle,cooldown=towers.Count*0.013f };
            }
            catch { if(created!=null) DestroyView(created);throw; }
        }
        public void Activate(BuildingInstance building)
        {
            var tower=staged[building.Id];tower.root.transform.SetParent(parent,true);tower.root.SetActive(true);towers.Add(tower);
        }
        public void Remove(BuildingInstance building)
        {
            if(!staged.Remove(building.Id,out var tower))return;
            towers.Remove(tower);tower.root.SetActive(false);DestroyView(tower.root);
        }
        public void Dispose()
        {
            requests.Clear();existing.Clear();
            foreach(var tower in staged.Values) if(tower.root!=null) DestroyView(tower.root);
            staged.Clear();towers.Clear();if(staging!=null) DestroyView(staging.gameObject);
        }
    }
}
