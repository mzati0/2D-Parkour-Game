using UnityEngine;
using UnityEngine.Serialization;

namespace ActiveRagoll
{
    public class VisualSkeletonMapper : MonoBehaviour
    {
        [System.Serializable]
        public class BoneMap
        {
            public string boneName; 
            public Transform visualBone;
            public Transform animationBone;
            public Transform activeRagdollBone;
            
            [Tooltip("Check this ONLY for the main Pelvis/Torso root bone")]
            public bool isRoot;
            [Tooltip("Check this for Dreads/Hair. It locks to physics and ignores animations.")]
            public bool isPurePhysics; 

            // Hidden cache for the Rigidbody to save performance
            [HideInInspector] public Rigidbody2D activeRb;
        }

        [Header("Auto-Mapper Roots")]
        public Transform visualRigRoot;
        [FormerlySerializedAs("ghostRigRoot")] public Transform animationRigRoot;
        [FormerlySerializedAs("activeRigRoot")] public Transform activeRagdollRigRoot;

        [Header("Global Blend (1 = Animation, 0 = Physics)")]
        [Range(0f, 1f)] public float globalBlend = 1f;

        [Header("The Bone Array")]
        public BoneMap[] bones;

        void Awake()
        {
            if (bones == null || bones.Length == 0)
            {
                AutoMapBones();
            }
        }

        void Start()
        {
            // Cache the rigidbodies on start so we don't use GetComponent every frame
            if (bones != null)
            {
                foreach (var bone in bones)
                {
                    if (bone.activeRagdollBone != null)
                    {
                        bone.activeRb = bone.activeRagdollBone.GetComponent<Rigidbody2D>();
                    }
                }
            }
        }

        [ContextMenu("Auto Map Bones")]
        public void AutoMapBones()
        {
            if (visualRigRoot == null || animationRigRoot == null || activeRagdollRigRoot == null)
            {
                Debug.LogError("Auto-Mapper failed: Assign all three Root transforms first.");
                return;
            }

            Transform[] visualBones = visualRigRoot.GetComponentsInChildren<Transform>();
            bones = new BoneMap[visualBones.Length];

            for (int i = 0; i < visualBones.Length; i++)
            {
                string targetName = visualBones[i].name;
                bones[i] = new BoneMap
                {
                    boneName = targetName,
                    visualBone = visualBones[i],
                    animationBone = FindBoneByName(animationRigRoot, targetName),
                    activeRagdollBone = FindBoneByName(activeRagdollRigRoot, targetName),
                    isRoot = (i == 0),
                    isPurePhysics = targetName.Contains("Dread"),
                };
            }
        }

        private Transform FindBoneByName(Transform root, string name)
        {
            Transform[] allBones = root.GetComponentsInChildren<Transform>();
            foreach (Transform bone in allBones)
            {
                if (bone.name == name) return bone;
            }
            return null;
        }

        // --- THE FIX ---
        // 1. Handle Physics Snapping strictly in FixedUpdate
        void FixedUpdate()
        {
            if (bones == null || globalBlend < 1f) return;

            foreach (var bone in bones)
            {
                // Ignore dreads (they are dynamic) or missing bones
                if (bone.isPurePhysics || bone.activeRagdollBone == null || bone.animationBone == null) continue;

                // If it has a Kinematic Rigidbody, move it using the physics engine properly
                if (bone.activeRb != null && bone.activeRb.bodyType == RigidbodyType2D.Kinematic)
                {
                    bone.activeRb.MovePosition(bone.animationBone.position);
                    bone.activeRb.MoveRotation(bone.animationBone.rotation);
                }
                else
                {
                    // Fallback just in case
                    bone.activeRagdollBone.position = bone.animationBone.position;
                    bone.activeRagdollBone.rotation = bone.animationBone.rotation;
                }
            }
        }

        // 2. Handle Visual Blending strictly in LateUpdate
        void LateUpdate()
        {
            if (bones == null) return;

            foreach (var bone in bones)
            {
                if (bone.visualBone == null || bone.animationBone == null || bone.activeRagdollBone == null) continue;

                float currentBlend = bone.isPurePhysics ? 0f : globalBlend;

                bone.visualBone.rotation = Quaternion.Slerp(bone.activeRagdollBone.rotation, bone.animationBone.rotation, currentBlend);

                if (bone.isRoot)
                {
                    bone.visualBone.position = Vector3.Lerp(bone.activeRagdollBone.position, bone.animationBone.position, currentBlend);
                }
            }
        }
    }
}