using System.Collections.Generic;
using UnityEngine;

namespace ActiveRagoll
{
    public class ActiveRagdollMaster : MonoBehaviour
    {
        [Tooltip("Leave empty to auto-populate on Start")]
        public List<ActiveBone2D> allBones = new List<ActiveBone2D>();

        [Header("Group Blend Weights")]
        [Range(0f, 1f)] public float headWeight = 1f;
        [Range(0f, 1f)] public float upperBodyWeight = 1f;
        [Range(0f, 1f)] public float lowerBodyWeight = 1f;
        [Range(0f, 1f)] public float jacketWeight = 1f;

        void Start()
        {
            // Auto-find all bones if the list is empty
            if (allBones.Count == 0)
            {
                allBones = new List<ActiveBone2D>(GetComponentsInChildren<ActiveBone2D>());
            }
        }

        void Update()
        {
            // Continuously push the slider values to the individual bone scripts
            foreach (var bone in allBones)
            {
                switch (bone.partGroup)
                {
                    case BodyPartGroup.Head:
                        bone.blendWeight = headWeight;
                        break;
                    case BodyPartGroup.UpperBody:
                        bone.blendWeight = upperBodyWeight;
                        break;
                    case BodyPartGroup.LowerBody:
                        bone.blendWeight = lowerBodyWeight;
                        break;
                    case BodyPartGroup.Jacket:
                        bone.blendWeight = jacketWeight;
                        break;
                }
            }
        }
    }
}