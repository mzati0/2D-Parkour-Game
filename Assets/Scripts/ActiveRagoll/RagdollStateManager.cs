using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace ActiveRagoll
{
    public class RagdollStateManager : MonoBehaviour
    {
        [Header("Core References")]
        [FormerlySerializedAs("ghostAnimator")] public Animator animator;
        public VisualSkeletonMapper visualMapper;
        [FormerlySerializedAs("activePelvis")] public Transform activeRagdollRoot;
        
        [Header("Player Root Sync")]
        public Transform playerRoot; 
        public Rigidbody2D playerRootRb; 
        public Collider2D playerCollider;
        public Player.ParkourController parkourController;
        
        [Header("Settings")]
        public float recoveryTime = 0.5f;
        public Vector3 standUpOffset = new Vector3(0, 1f, 0); 

        private List<Rigidbody2D> _coreRigidbodies = new List<Rigidbody2D>();
        private bool _isRagdolling = false;

        void Start()
        {
            Rigidbody2D[] allRigidbodies = activeRagdollRoot.GetComponentsInChildren<Rigidbody2D>();
            
            foreach (Rigidbody2D rb in allRigidbodies)
            {
                if (rb.gameObject.name.Contains("Dread"))
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    // Ensure these don't fall asleep
                    rb.sleepMode = RigidbodySleepMode2D.NeverSleep; 
                }
                else
                {
                    _coreRigidbodies.Add(rb);
                }
            }

            EnableAnimation();
        }

        void Update()
        {
            // NEW: Constantly drag the player/ghost root along with the falling ragdoll
            // This ensures they are perfectly aligned when you press T.
            if (_isRagdolling && playerRoot != null)
            {
                playerRoot.position = activeRagdollRoot.position + standUpOffset;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.rKey.wasPressedThisFrame && !_isRagdolling) TriggerRagdollDrop();
                if (Keyboard.current.tKey.wasPressedThisFrame && _isRagdolling) TriggerRecovery();
            }
        }

        public void TriggerRagdollDrop()
        {
            _isRagdolling = true;
            animator.enabled = false;
            visualMapper.globalBlend = 0f; 

            // Tell the parkour script to stop reading input
            if (parkourController) parkourController.isRagdolling = true; 

            Vector2 currentMomentum = playerRootRb ? playerRootRb.linearVelocity : Vector2.zero;

            if (playerRootRb) playerRootRb.simulated = false;
            if (playerCollider) playerCollider.enabled = false;

            foreach (Rigidbody2D rb in _coreRigidbodies)
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.linearVelocity = currentMomentum; 
            }
        }

        public void TriggerRecovery()
        {
            _isRagdolling = false;

            // Wake the parkour script back up
            if (parkourController) parkourController.isRagdolling = false; 
    
            if (playerRootRb) 
            {
                playerRootRb.simulated = true;
                // THE KILL-SWITCH: Force velocity to 0 the exact frame physics turns back on
                playerRootRb.linearVelocity = Vector2.zero; 
            }
    
            if (playerCollider) playerCollider.enabled = true;
            foreach (Rigidbody2D rb in _coreRigidbodies)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // Your future plan goes here: 
            // trigger "Lying Back" animation on ghostAnimator, then blend.
            animator.enabled = true;
            StartCoroutine(RecoveryBlendCoroutine());
        }

        private IEnumerator RecoveryBlendCoroutine()
        {
            float elapsedTime = 0f;
            while (elapsedTime < recoveryTime)
            {
                elapsedTime += Time.deltaTime;
                visualMapper.globalBlend = Mathf.SmoothStep(0f, 1f, elapsedTime / recoveryTime);
                yield return null;
            }
            visualMapper.globalBlend = 1f;
        }

        private void EnableAnimation()
        {
            _isRagdolling = false;
            visualMapper.globalBlend = 1f; 
            animator.enabled = true;

            foreach (Rigidbody2D rb in _coreRigidbodies)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
            }
        }
    }
}