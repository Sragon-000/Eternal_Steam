using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public sealed class SandboxPresentation : MonoBehaviour
    {
        public FoundationSandbox Sample;
        public SandboxInput Input;
        readonly Dictionary<int, GameObject> previews = new();
        readonly List<int> stale = new();
        MaterialPropertyBlock properties;
        void Awake() => properties = new MaterialPropertyBlock();
        void LateUpdate()
        {
            if (Sample.Session == null) return;
            var session = Sample.Session;
            foreach (var pair in Sample.Factory.Views)
            {
                if (!session.World.TryGet(pair.Key, out var building)) continue;
                bool recovering = false;
                foreach (var id in session.Recovery.Selected) if (id == pair.Key) recovering = true;
                pair.Value.SetState(Input.SelectedId == pair.Key, recovering);
                pair.Value.ShowRange(session.CanEdit && (Input.SelectedId == pair.Key || session.DirectionBuildingId == pair.Key),
                    session.DirectionBuildingId == pair.Key ? session.PreviewDirection : building.Direction);
            }
            stale.Clear(); stale.AddRange(previews.Keys);
            foreach (var request in session.Placement.Pending)
            {
                stale.Remove(request.Id);
                if (!previews.TryGetValue(request.Id, out var preview))
                {
                    // Geometry-only ghost: it never instantiates gameplay scripts from the view prefab.
                    preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    preview.name = "Reserved building " + request.Id;
                    preview.transform.SetParent(transform);
                    preview.GetComponent<Renderer>().sharedMaterial = Sample.SurfaceMaterial;
                    Destroy(preview.GetComponent<Collider>());
                    var outline = BuildingView.MakeLine("Reservation outline", preview.transform, Sample.LineMaterial, 0.09f);
                    outline.useWorldSpace = false; outline.positionCount = 5;
                    outline.SetPositions(new[] { new Vector3(-0.5f,0.55f,-0.5f), new Vector3(-0.5f,0.55f,0.5f), new Vector3(0.5f,0.55f,0.5f), new Vector3(0.5f,0.55f,-0.5f), new Vector3(-0.5f,0.55f,-0.5f) });
                    previews.Add(request.Id, preview);
                }
                preview.transform.position = session.World.Grid.Center(request.Cell, request.Footprint) + Vector3.up * 0.6f;
                preview.transform.localScale = new Vector3(request.Footprint.x * session.World.Grid.CellSize - 0.15f, 1, request.Footprint.y * session.World.Grid.CellSize - 0.15f);
                bool valid = session.Placement.Validate(request).Success;
                properties.SetColor("_BaseColor", valid ? new Color(0.3f,0.65f,1,0.4f) : new Color(1,0.15f,0.15f,0.4f));
                preview.GetComponent<Renderer>().SetPropertyBlock(properties);
                var line = preview.GetComponentInChildren<LineRenderer>();
                line.startColor = line.endColor = !valid ? Color.red : Input.SelectedPendingId == request.Id ? Color.white : Color.cyan;
            }
            foreach (int id in stale) { Destroy(previews[id]); previews.Remove(id); }
        }
        void OnDisable()
        { foreach (var preview in previews.Values) if (preview != null) Destroy(preview); previews.Clear(); }
    }
}
