using UnityEngine;

namespace ActiveRagoll
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ActiveBone2D : MonoBehaviour
    {
        [Header("Hierarchy & Grouping")]
        public BodyPartGroup partGroup;
        public Transform targetBone; 

        [Header("Physics Tuning")]
        [Tooltip("How hard it tries to match the animation. Lowered because we now scale by mass.")]
        public float forceMultiplier = 50f; 
        public float damping = 5f;          
        [Tooltip("The anti-explosion safety net. Stops the script from ripping hinges apart.")]
        public float maxTorque = 500f;      

        [Header("Current State")]
        [Range(0f, 1f)]
        public float blendWeight = 1f;       
        
        [Header("Debug - READ ONLY")]
        public float debugAngleDiff;
        public float debugAppliedTorque;

        private Rigidbody2D rb;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (blendWeight <= 0f || targetBone == null) return;

            // 1. Calculate the shortest angle difference
            float angleDiff = Mathf.DeltaAngle(rb.rotation, targetBone.eulerAngles.z);

            // 2. Calculate the raw torque needed
            float desiredTorque = (angleDiff * forceMultiplier) - (rb.angularVelocity * damping);

            // 3. THE MAGIC SAUCE: Scale the torque by this specific bone's inertia
            // This makes it automatically apply huge force to the Torso (10) and tiny force to the Foot (0.78)
            desiredTorque *= rb.inertia;
            
            // 4. THE SAFETY NET: Clamp the torque so it can never exceed physics limits and explode
            desiredTorque = Mathf.Clamp(desiredTorque, -maxTorque, maxTorque);

            // 5. Apply the safe, mass-adjusted torque
            rb.AddTorque(desiredTorque * blendWeight);
            debugAngleDiff = angleDiff;
            debugAppliedTorque = desiredTorque * blendWeight;
        }
    }
}