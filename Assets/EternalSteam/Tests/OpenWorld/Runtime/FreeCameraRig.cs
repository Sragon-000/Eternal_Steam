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
            Focus+=new Vector3(input.x,0,input.y)*(Zoom*1.2f*seconds);
            var p=Ground.transform.position;var size=Ground.terrainData.size;
            Focus.x=Mathf.Clamp(Focus.x,p.x+8,p.x+size.x-8);Focus.z=Mathf.Clamp(Focus.z,p.z+8,p.z+size.z-8);
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
