using UnityEngine;
using UnityEngine.SceneManagement; 
    
public class RedLaserDeathWall : MonoBehaviour
{
    [Header("References")]
    public Transform player; // Drag your player here in the inspector

    [Header("Rubberband Settings")]
    public float baseSpeed = 7.5f;       // The slow, terrifying speed when it's right behind you
    public float catchUpSpeed = 15f;     // The fast speed when you leave it far behind
    public float catchUpDistance = 25f;  // If you are this many units ahead, it goes max speed

    private void Update()
    {
        if (player == null) return;

        // 1. How far ahead is the player?
        float distanceToPlayer = player.position.x - transform.position.x;

        // 2. Calculate speed based on distance. 
        // Mathf.Lerp smoothly blends between baseSpeed and catchUpSpeed based on the distance.
        // If distance is 0, speed = 7.5. If distance is 25+, speed = 15.
        float speedRatio = Mathf.Clamp01(distanceToPlayer / catchUpDistance);
        float currentSpeed = Mathf.Lerp(baseSpeed, catchUpSpeed, speedRatio);

        // 3. Move the wall
        transform.Translate(Vector3.right * (currentSpeed * Time.deltaTime));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}