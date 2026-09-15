using UnityEngine;
using UnityEngine.InputSystem;
namespace EternalSteam.Demo
{
    public static class HordePointerInput
    {
        public static HordePlacementInput Read(Camera camera)
        {
            var mouse=Mouse.current; var keyboard=Keyboard.current;
            bool cancel=mouse!=null && mouse.rightButton.wasPressedThisFrame || keyboard!=null && keyboard.escapeKey.wasPressedThisFrame;
            bool confirm=mouse!=null && mouse.leftButton.wasPressedThisFrame;
            if(mouse==null || camera==null) return new HordePlacementInput(false,Vector3.zero,cancel,confirm);
            var ray=camera.ScreenPointToRay(mouse.position.ReadValue());
            bool projected=new Plane(Vector3.up,Vector3.zero).Raycast(ray,out float distance);
            return new HordePlacementInput(projected,projected?ray.GetPoint(distance):Vector3.zero,cancel,confirm);
        }
    }
}
