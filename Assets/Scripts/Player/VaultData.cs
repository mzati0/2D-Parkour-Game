using UnityEngine;

namespace Player
{
    [CreateAssetMenu(fileName = "New Vault Data", menuName = "Parkour/Vault Data")]
    public class VaultData : ScriptableObject
    {
        [Header("Selection Logic")]
        [Tooltip("Higher number = higher priority to be chosen.")]
        public int priorityScore = 0;
        
        [Tooltip("Minimum distance from the obstacle required to trigger this vault. (Default: 0)")]
        public float minDistance = 0f;
        
        [Tooltip("Maximum distance from the obstacle required to trigger this vault. (Default: 4)")]
        public float maxDistance = 1.5f;

        [Tooltip("Check this if it is a Trick Vault. Enables vulnerability and mid-air collisions.")]
        public bool isTrick = false;

        [Header("Animation Settings")]
        [Tooltip("IMPORTANT: This must perfectly match BOTH the Animator Trigger name AND the exact Animation Clip filename!")]
        public string animationStateName; 
        
        [Header("Trajectory Curves")]
        [Tooltip("X-Axis: 0 to 1 time. Y-Axis: 0 to 1 distance forward.")]
        public AnimationCurve xCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Tooltip("X-Axis: 0 to 1 time. Y-Axis: 0 to 1 height clearance.")]
        public AnimationCurve yCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Clearance")]
        [Tooltip("How far past the obstacle the player lands. Default is 0.5")]
        public float clearancePadding = 0.5f;
    }
}