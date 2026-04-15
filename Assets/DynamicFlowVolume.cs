using UnityEngine;
using UnityEngine.Rendering;

public class DynamicFlowVision : MonoBehaviour
{
    [Header("References")]
    public Volume suffocationVolume; 
    public ParkourController playerController; 

    [Header("Settings")]
    public float lerpSpeed = 5f; // How fast the vignette fades in/out

    private float _currentVisualWeight = 1f;

    private void Update()
    {
        if (!suffocationVolume || !playerController) return;

        // 1. Get the INSTANT math ratio
        float targetFlowRatio = playerController.currentFlowMeter / playerController.maxFlowMeter;

        // 2. Calculate what the weight SHOULD be (inverted)
        float targetWeight = 1f - targetFlowRatio; 

        // 3. Smoothly Lerp the current visual weight towards that target
        _currentVisualWeight = Mathf.Lerp(_currentVisualWeight, targetWeight, Time.deltaTime * lerpSpeed);

        // 4. Apply the smoothed value to the volume
        suffocationVolume.weight = _currentVisualWeight; 
    }
}