using Unity.Cinemachine;
using UnityEngine;

namespace Camera
{
    public class CinemachineSpeedZoom : MonoBehaviour
    {
        [Header("References")]
        public Rigidbody2D playerRb;
    
        // In newer Unity versions this is CinemachineCamera. 
        // If it throws an error, change it to CinemachineVirtualCamera.
        private CinemachineCamera _vCam; 

        [Header("Zoom Settings")]
        public float baseZoom = 5f;        // Zoom level when slow/stopped
        public float maxZoom = 8f;         // Zoom level at top speed
        public float zoomSpeed = 3f;       // How fast it zooms in/out
        public float maxPlayerSpeed = 15f; // Speed required for max zoom

        private void Start()
        {
            _vCam = GetComponent<CinemachineCamera>();
        }

        private void Update()
        {
            if (!playerRb || !_vCam) return;

            float currentSpeed = Mathf.Abs(playerRb.linearVelocity.x);
        
            // Calculate speed ratio (0 to 1)
            float speedRatio = Mathf.Clamp01(currentSpeed / maxPlayerSpeed);
        
            // Calculate the target zoom
            float targetZoom = Mathf.Lerp(baseZoom, maxZoom, speedRatio);
        
            // Smoothly transition the Cinemachine Lens Orthographic Size
            _vCam.Lens.OrthographicSize = Mathf.Lerp(_vCam.Lens.OrthographicSize, targetZoom, Time.deltaTime * zoomSpeed);
        }
    }
}