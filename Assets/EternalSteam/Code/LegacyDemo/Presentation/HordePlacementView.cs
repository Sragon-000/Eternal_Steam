using System;
using UnityEngine;
using static EternalSteam.Demo.HordeVisualPrimitives;
namespace EternalSteam.Demo
{
    public sealed class HordePlacementView : IDisposable
    {
        readonly Transform transform;
        readonly Material validMaterial,invalidMaterial;
        readonly Material[] typeMaterials;
        Transform preview,previewBarrel;
        Renderer previewRenderer;
        LineRenderer rangeRing,directionArrow,selectedCell;
        public HordePlacementView(Transform parent,Material valid,Material invalid,Material[] types)
        { transform=parent;validMaterial=valid;invalidMaterial=invalid;typeMaterials=types;CreatePreview(); }
        void CreatePreview()
        {
            preview = MakePart("Placement Preview", transform, Vector3.zero, new Vector3(1, 0.8f, 1), validMaterial);
            preview.localScale *= HordeMapLayout.TowerScale;
            previewRenderer = preview.GetComponent<Renderer>();
            previewBarrel = MakePart("Preview Barrel", preview, new Vector3(0, 0, 0.8f), new Vector3(0.28f, 0.28f, 1.1f), validMaterial);
            rangeRing = MakeLine("Firing corridor", transform, validMaterial, 0.065f, 35);
            directionArrow = MakeLine("Aim arrow", transform, validMaterial, 0.09f, 5);
            selectedCell = MakeLine("Selected grid cell", transform, validMaterial, 0.09f, 5);
        }
        public void Render(HordePlacementController state,HordeMapKind mapKind,HordeTowerKind selectedTower)
        {
            bool visible=state.Visible;
            preview.gameObject.SetActive(visible);
            rangeRing.enabled=directionArrow.enabled=selectedCell.enabled=visible && state.ChoosingDirection;
            if(!visible) return;
            bool canPlace=state.CanPlace;
            Vector3 placement=state.Position,placementDirection=state.Direction;
            float placementRange=state.Range;
            Material color = canPlace ? validMaterial : invalidMaterial;
            previewRenderer.sharedMaterial = canPlace ? typeMaterials[(int)selectedTower] : invalidMaterial;
            preview.position = placement + Vector3.up * (0.9f * HordeMapLayout.TowerScale + HordeMapLayout.SurfaceHeight(mapKind));
            preview.rotation = Quaternion.LookRotation(placementDirection);
            previewBarrel.GetComponent<Renderer>().sharedMaterial = color;
            previewBarrel.localScale = selectedTower == HordeTowerKind.Cannon ? new Vector3(0.55f, 0.55f, 1.3f) : selectedTower == HordeTowerKind.Frost ? new Vector3(0.65f, 0.2f, 0.7f) : new Vector3(0.28f, 0.28f, 1.1f);
            rangeRing.sharedMaterial = color;
            directionArrow.sharedMaterial = color;
            selectedCell.sharedMaterial = color;
            Vector3 origin = placement + Vector3.up * (HordeMapLayout.SurfaceHeight(mapKind) + 0.07f);
            float halfCell = HordeMapLayout.BuildCellSize * 0.5f;
            selectedCell.SetPosition(0, origin + new Vector3(-halfCell, 0, -halfCell));
            selectedCell.SetPosition(1, origin + new Vector3(-halfCell, 0, halfCell));
            selectedCell.SetPosition(2, origin + new Vector3(halfCell, 0, halfCell));
            selectedCell.SetPosition(3, origin + new Vector3(halfCell, 0, -halfCell));
            selectedCell.SetPosition(4, origin + new Vector3(-halfCell, 0, -halfCell));
            Vector3 right = Vector3.Cross(Vector3.up, placementDirection);
            Vector3 end = origin + placementDirection * placementRange;
            float spread = HordeDeploymentGeometry.SpreadHalfAngle(placementRange);
            rangeRing.SetPosition(0, origin);
            for (int i = 0; i <= 32; i++)
                rangeRing.SetPosition(i + 1, origin + Quaternion.AngleAxis(Mathf.Lerp(-spread, spread, i / 32f), Vector3.up) * placementDirection * placementRange);
            rangeRing.SetPosition(34, origin);
            directionArrow.SetPosition(0, origin);
            directionArrow.SetPosition(1, end);
            directionArrow.SetPosition(2, end - placementDirection * 1.5f - right);
            directionArrow.SetPosition(3, end);
            directionArrow.SetPosition(4, end - placementDirection * 1.5f + right);

        }
        public void Dispose()
        {
            if(preview!=null) UnityEngine.Object.Destroy(preview.gameObject);
            if(rangeRing!=null) UnityEngine.Object.Destroy(rangeRing.gameObject);
            if(directionArrow!=null) UnityEngine.Object.Destroy(directionArrow.gameObject);
            if(selectedCell!=null) UnityEngine.Object.Destroy(selectedCell.gameObject);
        }
    }
}
