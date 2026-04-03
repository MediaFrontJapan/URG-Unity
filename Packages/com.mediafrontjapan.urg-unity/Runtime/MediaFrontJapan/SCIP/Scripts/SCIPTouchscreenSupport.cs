#nullable enable
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace MediaFrontJapan.SCIP
{
    public class SCIPTouchscreenSupport : MonoBehaviour
    {
        [SerializeField]  SCIPScanPlane[] scanPlanes = default!;
        SCIPTouchscreen SCIPTouchscreen = default!;
        
        private void Start()
        {
            SCIPTouchscreen = InputSystem.AddDevice<SCIPTouchscreen>();
            SCIPTouchscreen.scanPlanes = scanPlanes;
        }
        protected void OnDisable()
        {
            if (SCIPTouchscreen != null)
            {
                InputSystem.DisableDevice(SCIPTouchscreen);
            }
        }
        private void OnDestroy()
        {
            InputSystem.RemoveDevice(SCIPTouchscreen);
        }
    }
}

