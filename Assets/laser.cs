using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.InputSystem; 

public class RedLaserDeathWall : MonoBehaviour
{
    [Header("Settings")]
    public float startingSpeed = 1f; 
    public float acceleration = 0.5f; // How much speed it gains per second
    public float maxSpeed = 15f; // The absolute cap
    
    private float currentSpeed;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D boxCollider;
    private bool isChasing = false;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        boxCollider = GetComponent<BoxCollider2D>();
        currentSpeed = startingSpeed;

        if (spriteRenderer != null) spriteRenderer.enabled = false;
        if (boxCollider != null) boxCollider.enabled = false;
    }

    void Update()
    {
        if (!isChasing && Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
        {
            isChasing = true;
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (boxCollider != null) boxCollider.enabled = true;
        }

        if (isChasing)
        {
            // 1. Increase the speed every frame
            currentSpeed += acceleration * Time.deltaTime;
            
            // 2. Clamp it so it doesn't get infinitely fast
            currentSpeed = Mathf.Clamp(currentSpeed, 0, maxSpeed);

            // 3. Move the wall at the new current speed
            transform.Translate(Vector3.right * currentSpeed * Time.deltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}