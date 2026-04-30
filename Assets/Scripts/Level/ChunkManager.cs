using UnityEngine;

namespace Level
{
    public class ChunkManager : MonoBehaviour
    {
        public Transform player;
        public Transform parent;
        
        [Header("Difficulty Tiers")]
        public GameObject[] easyChunks;
        public GameObject[] medChunks;
        public GameObject[] hardChunks;
    
        public float chunkLength = 20f; 
        public float spawnDistanceThreshold = 10f; 
        
        [Header("Progression Distances")]
        public float mediumStartDistance = 200f; // Swap to Medium chunks at 200m
        public float hardStartDistance = 500f;   // Swap to Hard chunks at 500m

        private float _nextRightSpawnX = 20f; 
        private float _nextLeftSpawnX = -20f; 

        private void Update()
        {
            if (player.position.x + spawnDistanceThreshold > _nextRightSpawnX)
            {
                SpawnChunk(_nextRightSpawnX);
                _nextRightSpawnX += chunkLength; 
            }
        
            if (player.position.x - spawnDistanceThreshold < _nextLeftSpawnX)
            {
                SpawnChunk(_nextLeftSpawnX);
                _nextLeftSpawnX -= chunkLength; 
            }
        }

        private void SpawnChunk(float xPos)
        {
            float distanceRun = Mathf.Abs(xPos);
            float roll = Random.value; // Rolls a decimal between 0.0 and 1.0
            GameObject[] poolToUse;

            // Phase 1: 0 - 200m
            if (distanceRun < 200f)
            {
                poolToUse = easyChunks; // 100% Easy
            }
            // Phase 2: 200m - 500m
            else if (distanceRun < 500f)
            {
                // 60% Easy, 40% Medium
                poolToUse = roll < 0.6f ? easyChunks : medChunks;
            }
            // Phase 3: 500m - 800m
            else if (distanceRun < 800f)
            {
                // 20% Easy, 50% Medium, 30% Hard
                if (roll < 0.2f) poolToUse = easyChunks;
                else if (roll < 0.7f) poolToUse = medChunks;
                else poolToUse = hardChunks;
            }
            // Phase 4: 800m+ (Endgame)
            else
            {
                // 10% Medium, 90% Hard. (No more easy chunks).
                poolToUse = roll < 0.1f ? medChunks : hardChunks;
            }

            // Failsafe just in case you haven't assigned prefabs to an array yet
            if (poolToUse.Length == 0) poolToUse = easyChunks;
            if (poolToUse.Length == 0) return;

            int randomIndex = Random.Range(0, poolToUse.Length);
            Instantiate(poolToUse[randomIndex], new Vector3(xPos, 0f, 0f), Quaternion.identity).transform.SetParent(parent);
        }
    }
}