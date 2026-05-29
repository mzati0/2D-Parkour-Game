using UnityEngine;

namespace ActiveRagoll
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class ActiveRootAnchor2D : MonoBehaviour
    {
        [Header("Targeting")]
        [Tooltip("Drag the exact corresponding root bone from the Ghost Rig here.")]
        public Transform ghostRoot; 

        [Header("Positional Pull")]
        [Tooltip("How strongly the Active Rig's torso tracks the Ghost Rig's torso.")]
        public float positionalStrength = 50f;

        private Rigidbody2D rb;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        void FixedUpdate()
        {
            if (ghostRoot == null) return;

            // Calculate the physical distance between the Active Root and Ghost Root
            Vector2 targetPos = ghostRoot.position;
            Vector2 currentPos = rb.position;

            // Pull the Active Rig's root to the exact position of the Ghost Rig
            rb.linearVelocity = (targetPos - currentPos) * positionalStrength;
        }
    }
}