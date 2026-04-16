using UnityEngine;

namespace Level
{
    public class LaserDirector : MonoBehaviour
    {
        [Header("References")]
        public Transform player;
        public GameObject laserObject; // Drag your actual Laser here

        [Header("Settings")]
        public float startThresholdX = 10f; // The invisible line the player must cross

        private bool _laserActivated;

        private void Start()
        {
            // Ensure the laser is off when the game starts
            if (laserObject != null)
            {
                laserObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_laserActivated || !player) return;
        
            // If player runs to the right and crosses the threshold
            if (!(player.position.x > startThresholdX)) return;
            _laserActivated = true;
                
            if (laserObject)
            {
                laserObject.SetActive(true);
            }
        }
    }
}