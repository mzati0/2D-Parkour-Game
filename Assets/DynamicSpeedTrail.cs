using System;
using UnityEngine;

[RequireComponent(typeof(TrailRenderer))]
public class DynamicSpeedTrail : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D rb;
    private TrailRenderer trail;

    [Header("Settings")]
    public float minSpeedForTrail = 8f; // The speed where the flow state "kicks in"
    public float maxSpeed = 15f; // Your top speed
    
    private ParkourController _parkourController;

    private void Awake()
    {
        _parkourController = FindAnyObjectByType<ParkourController>();
    }

    void Start()
    {
        trail = GetComponent<TrailRenderer>();
        trail.emitting = false;
    }

    void Update()
    {
        // Get absolute speed regardless of moving left or right
        float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
        float flowRatio = _parkourController.currentFlowMeter / _parkourController.maxFlowMeter;

        if (currentSpeed >= minSpeedForTrail)
        {
            trail.emitting = true;
            
            // Communication Design: The faster you go, the longer the trail gets
            float speedRatio = Mathf.Clamp01((currentSpeed - minSpeedForTrail) / (maxSpeed - minSpeedForTrail));
            trail.time = Mathf.Lerp(0.1f, 0.35f, speedRatio);
            trail.startColor = Color.Lerp(Color.white, Color.cyan, flowRatio);
            trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f); // Fade out alpha
        }
        else
        {
            trail.emitting = false;
        }
    }
}