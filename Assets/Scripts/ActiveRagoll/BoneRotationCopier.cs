using UnityEngine;

namespace ActiveRagoll
{
    public class BoneRotationCopier : MonoBehaviour
    {
        [Header("Target to Copy")]
        public Transform targetBone;

        [Header("Settings")]
        public bool maintainInitialOffset = true;
        public Vector3 manualEulerOffset;

        private Quaternion _initialOffset;

        void Start()
        {
            if (targetBone != null && maintainInitialOffset)
            {
                // Calculate the local rotational difference instead of global
                _initialOffset = Quaternion.Inverse(targetBone.localRotation) * transform.localRotation;
            }
            else
            {
                _initialOffset = Quaternion.Euler(manualEulerOffset);
            }
        }

        void LateUpdate()
        {
            if (targetBone == null) return;

            // Apply the local rotation, completely immune to parent-child execution order fighting
            transform.localRotation = targetBone.localRotation * _initialOffset;
        }
    }
}