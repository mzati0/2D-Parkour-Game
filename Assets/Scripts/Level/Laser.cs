using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro; 
using Player; 
using Camera; 

namespace Level
{
    public class RedLaserDeathWall : MonoBehaviour
    {
        [Header("References")]
        public Transform player; 
        public ParkourController playerController; 
        public CinemachineSpeedZoom camZoom;
        public TextMeshProUGUI distanceUI; 

        [Header("Rubberband Settings")]
        public float baseSpeed = 7.5f;       
        public float catchUpSpeed = 15f;     
        public float catchUpDistance = 25f;  

        [Header("Last Stand (Juice)")]
        public float lastStandDistance = 6f; 
        public float slowMoScale = 0.5f;
        public float emergencyZoomLevel = 4f; 

        private void Update()
        {
            if (player == null) return;

            float distanceToPlayer = player.position.x - transform.position.x;

            // THE FIX: If the laser mathematically passes the player, instantly trigger Game Over.
            if (distanceToPlayer < 0f)
            {
                TriggerLoss();
                return;
            }

            // 1. UI Feedback
            if (distanceUI != null)
            {
                distanceUI.text = distanceToPlayer.ToString("F1") + "m";
                distanceUI.color = distanceToPlayer < lastStandDistance ? Color.red : Color.white;
            }

            // 2. Last Stand Mechanic
            if (distanceToPlayer < lastStandDistance && playerController != null && playerController.currentFlowMeter > 5f)
            {
                Time.timeScale = slowMoScale;
                Time.fixedDeltaTime = 0.02f * Time.timeScale;
                playerController.currentFlowMeter -= 20f * Time.unscaledDeltaTime;
                if (camZoom != null) camZoom.SetEmergencyZoom(emergencyZoomLevel);
                playerController.isLastStand = true; 
            }
            else
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = 0.02f;
                if (camZoom != null) camZoom.ClearEmergencyZoom();
                if (playerController != null) playerController.isLastStand = false;
            }

            // 3. Move the wall
            float speedRatio = Mathf.Clamp01(distanceToPlayer / catchUpDistance);
            float currentSpeed = Mathf.Lerp(baseSpeed, catchUpSpeed, speedRatio);
            
            // RESTORED: Vector3.down as requested!
            transform.Translate(Vector3.down * (currentSpeed * Time.deltaTime));
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                TriggerLoss();
            }
        }

        // Centralized the Game Over logic so both triggers reset the timescale correctly
        private void TriggerLoss()
        {
            Time.timeScale = 1f; 
            Time.fixedDeltaTime = 0.02f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex -1);
        }
    }
}