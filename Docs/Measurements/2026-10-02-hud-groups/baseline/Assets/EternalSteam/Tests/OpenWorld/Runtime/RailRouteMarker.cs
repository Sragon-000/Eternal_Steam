using TMPro;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // One authored gameplay marker instance per station on the selected route.
    public sealed class RailRouteMarker:MonoBehaviour
    {
        public TMP_Text Number;public Transform Arrow;
        public void Present(int number,Vector3 station,Vector3 departure,Vector3 direction)
        {transform.position=station+Vector3.up*3;Number.text=number.ToString();if(Camera.main!=null)Number.transform.rotation=Camera.main.transform.rotation;Arrow.position=departure+Vector3.up*.6f;direction.y=0;if(direction.sqrMagnitude>.001f)Arrow.rotation=Quaternion.LookRotation(direction);}
    }
}
