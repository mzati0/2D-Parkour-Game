using Unity.Cinemachine;
using UnityEngine;

namespace Camera
{
    public class CinemachineSpeedZoom : MonoBehaviour
    {
        [Header("References")]
        public Rigidbody2D playerRb;
        private CinemachineCamera _vCam; 

        [Header("Zoom Settings")]
        public float baseZoom = 5f;        
        public float maxZoom = 8f;         
        public float zoomSpeed = 3f;       
        public float maxPlayerSpeed = 15f; 

        private float emergencyZoom = -1f;

        private void Start()
        {
            _vCam = GetComponent<CinemachineCamera>();
        }

        // The Laser will call this!
        public void SetEmergencyZoom(float zoomLevel)
        {
            emergencyZoom = zoomLevel;
        }

        public void ClearEmergencyZoom()
        {
            emergencyZoom = -1f;
        }

        private void Update()
        {
            if (!playerRb || !_vCam) return;

            float targetZoom;

            if (emergencyZoom > 0f)
            {
                targetZoom = emergencyZoom;
            }
            else
            {
                float currentSpeed = Mathf.Abs(playerRb.linearVelocity.x);
                float speedRatio = Mathf.Clamp01(currentSpeed / maxPlayerSpeed);
                targetZoom = Mathf.Lerp(baseZoom, maxZoom, speedRatio);
            }
        
            // Uses unscaledDeltaTime so the camera still moves fast during slow-mo!
            _vCam.Lens.OrthographicSize = Mathf.Lerp(_vCam.Lens.OrthographicSize, targetZoom, Time.unscaledDeltaTime * zoomSpeed * 2f);
        }
    }
}