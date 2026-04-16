using Player;
using UnityEngine;

namespace Effects
{
    [RequireComponent(typeof(TrailRenderer))]
    public class DynamicSpeedTrail : MonoBehaviour
    {
        [Header("References")]
        public Rigidbody2D rb;
        private TrailRenderer _trail;

        [Header("Settings")]
        public float minSpeedForTrail = 8f; // The speed where the flow state "kicks in"
        public float maxSpeed = 15f; // Your top speed
    
        private ParkourController _parkourController;

        private void Awake()
        {
            _parkourController = FindAnyObjectByType<ParkourController>();
        }

        private void Start()
        {
            _trail = GetComponent<TrailRenderer>();
            _trail.emitting = false;
        }

        private void Update()
        {
            // Get absolute speed regardless of moving left or right
            float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
            float flowRatio = _parkourController.currentFlowMeter / _parkourController.maxFlowMeter;

            if (currentSpeed >= minSpeedForTrail)
            {
                _trail.emitting = true;
            
                // Communication Design: The faster you go, the longer the trail gets
                float speedRatio = Mathf.Clamp01((currentSpeed - minSpeedForTrail) / (maxSpeed - minSpeedForTrail));
                _trail.time = Mathf.Lerp(0.1f, 0.35f, speedRatio);
                _trail.startColor = Color.Lerp(Color.white, Color.cyan, flowRatio);
                _trail.endColor = new Color(_trail.startColor.r, _trail.startColor.g, _trail.startColor.b, 0f); // Fade out alpha
            }
            else
            {
                _trail.emitting = false;
            }
        }
    }
}