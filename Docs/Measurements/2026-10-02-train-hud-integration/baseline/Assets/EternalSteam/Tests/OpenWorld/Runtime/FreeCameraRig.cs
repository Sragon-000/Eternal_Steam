using UnityEngine;
using UnityEngine.InputSystem;
namespace EternalSteam.OpenWorld
{
    public sealed class FreeCameraRig : MonoBehaviour
    {
        public Terrain Ground;
        public Camera View;
        public Vector3 Focus;
        public float Zoom=26;
        public bool BlockKeyboard, BlockPointer;
        public void Pan(Vector2 input,float seconds)
        {
            MoveFocus(Focus+new Vector3(input.x,0,input.y)*(Zoom*1.2f*seconds));
        }
        public void MoveFocus(Vector3 point)
        {
            if(!float.IsFinite(point.x)||!float.IsFinite(point.z)||Ground==null)return;
            if(Ground.TryGetComponent<TileWorldGround>(out var tiles)&&tiles.GridRoot!=null&&tiles.Width>0&&tiles.Height>0&&tiles.CellSize>0)
            {
                // Clamp in the authored map's local frame. Its 45-degree diamond does not
                // fill the axis-aligned Terrain height-cache rectangle.
                var local=tiles.GridRoot.InverseTransformPoint(point);
                float edge=Mathf.Min(8f,Mathf.Min(tiles.Width,tiles.Height)*tiles.CellSize*.25f);
                local.x=Mathf.Clamp(local.x,edge,tiles.Width*tiles.CellSize-edge);
                local.z=Mathf.Clamp(local.z,edge,tiles.Height*tiles.CellSize-edge);
                point=tiles.GridRoot.TransformPoint(local);
            }
            else
            {
                var p=Ground.transform.position;var size=Ground.terrainData.size;
                point.x=Mathf.Clamp(point.x,p.x+8,p.x+size.x-8);
                point.z=Mathf.Clamp(point.z,p.z+8,p.z+size.z-8);
            }
            point.y=Ground.SampleHeight(point)+Ground.transform.position.y;
            Focus=point;
        }
        public void ChangeZoom(float steps) => Zoom=Mathf.Clamp(Zoom*Mathf.Exp(-steps*.12f),10,70);
        void LateUpdate()
        {
            var k=Keyboard.current;
            if(!BlockKeyboard && k!=null) {
                var move=new Vector2((k.dKey.isPressed?1:0)-(k.aKey.isPressed?1:0),(k.wKey.isPressed?1:0)-(k.sKey.isPressed?1:0));
                Pan(Vector2.ClampMagnitude(move,1),Time.unscaledDeltaTime);
            }
            if(!BlockPointer && Mouse.current!=null) {
                // Input System defaults to normalized wheel steps (one notch = 1).
                float steps=Mouse.current.scroll.ReadValue().y;
                if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                    (Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.WindowsEditor))steps/=120f;
                ChangeZoom(steps);
            }
            Focus.y=Ground.SampleHeight(Focus)+Ground.transform.position.y;
            View.orthographicSize=Zoom;View.transform.rotation=Quaternion.Euler(60,0,0);
            View.transform.position=Focus-View.transform.forward*110;
        }
    }
}
