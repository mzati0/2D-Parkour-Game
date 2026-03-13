using UnityEngine;

public class AdvancedParkourController : MonoBehaviour
{
    [Header("Raycast Origins")]
    public Transform footPos;
    public Transform kneePos;
    public Transform waistPos;
    public Transform headPos;
    
    public float forwardDistance = 2f;
    public LayerMask obstacleLayer;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            AnalyzeGeometry(1f); // 1f represents facing right
        }
    }

    void AnalyzeGeometry(float facingDir)
    {
        Vector2 dir = new Vector2(facingDir, 0);

        // Tiered Horizontal Checks
        bool footHit = Physics2D.Raycast(footPos.position, dir, forwardDistance, obstacleLayer);
        bool kneeHit = Physics2D.Raycast(kneePos.position, dir, forwardDistance, obstacleLayer);
        bool waistHit = Physics2D.Raycast(waistPos.position, dir, forwardDistance, obstacleLayer);
        bool headHit = Physics2D.Raycast(headPos.position, dir, forwardDistance, obstacleLayer);

        if (footHit && !kneeHit) Debug.Log("Detected: Tripping Hazard / Small Step");
        else if (footHit && kneeHit && !waistHit) Debug.Log("Detected: Low Vault");
        else if (footHit && kneeHit && waistHit && !headHit)
        {
            Debug.Log("Detected: High Vault. Calculating Clearance...");
            CalculateDynamicClearance(facingDir);
        }
        else if (headHit) Debug.Log("Detected: Full Wall (Climb/Wallrun)");
    }

    void CalculateDynamicClearance(float facingDir)
    {
        // Fire rays downward from above the obstacle at increasing depths
        float checkHeight = headPos.position.y + 2f; // Safely above the obstacle
        float startX = transform.position.x + (forwardDistance * facingDir);
        
        float maxClearanceCheck = 5f;
        float clearanceFound = -1f;

        for (float i = 0.5f; i <= maxClearanceCheck; i += 0.5f)
        {
            Vector2 checkPos = new Vector2(startX + (i * facingDir), checkHeight);
            RaycastHit2D downHit = Physics2D.Raycast(checkPos, Vector2.down, 5f, obstacleLayer);
            
            Debug.DrawRay(checkPos, Vector2.down * 5f, Color.blue, 2f);

            if (downHit.collider == null)
            {
                clearanceFound = i;
                break; // We found the empty space on the other side
            }
        }

        Debug.Log($"Dynamic Clearance calculated at: {clearanceFound} units.");
    }
}