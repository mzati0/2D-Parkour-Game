using UnityEngine;
using UnityEngine.InputSystem;

public class ParkourController : MonoBehaviour
{
    [Header("Visuals & States")]
    public SpriteRenderer cubeSprite;
    public Color normalColor = Color.blue;
    public Color stumbleColor = Color.red;
    public bool isVaulting = false;
    public bool isGrounded = false;
    public bool isStumbling = false;
    private float stumbleTimer = 1f;
    private float lockedFacingDirection = 1f;

    [Header("Physics")]
    public Rigidbody2D rb;
    public float moveSpeed = 10f;
    public float jumpForce = 20f;
    public float springboardForce = 25f;
    public float facingDirection = 1f;
    public float playerWidth = 1f;

    [Header("Raycast Data")]
    public Transform footPosition;
    public float rayDistance = 2f;
    public float groundCheckDistance = 0.2f;
    public LayerMask obstacleLayer;
    public LayerMask groundLayer;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference jumpAction;
    public InputActionReference parkourAction;
    public InputActionReference trickAction;

    void Start()
    {
        // Auto-assign the SpriteRenderer if you forgot to drag it in the inspector
        if (cubeSprite == null) cubeSprite = GetComponent<SpriteRenderer>(); 
        if (cubeSprite != null) cubeSprite.color = normalColor;
    }

    void OnEnable()
    {
        moveAction.action.Enable();
        jumpAction.action.Enable();
        parkourAction.action.Enable();
        trickAction.action.Enable();

        jumpAction.action.performed += OnJump;
        trickAction.action.performed += OnTrickTap;
    }

    void OnDisable()
    {
        moveAction.action.Disable();
        jumpAction.action.Disable();
        parkourAction.action.Disable();
        trickAction.action.Disable();

        jumpAction.action.performed -= OnJump;
        trickAction.action.performed -= OnTrickTap;
    }

    void Update()
    {
        CheckGrounded();

        if (isStumbling && !isVaulting)
        {
            HandleStumbleDeceleration();
            return;
        }

        HandleMovement();
        HandlePassiveStumble();
        HandleParkourHold();
    }

    void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(footPosition.position, Vector2.down, groundCheckDistance, groundLayer);
        isGrounded = hit.collider != null;
    }

    void HandleMovement()
    {
        if (isVaulting || !isGrounded) return; 

        Vector2 moveVector = moveAction.action.ReadValue<Vector2>();
        rb.linearVelocity = new Vector2(moveVector.x * moveSpeed, rb.linearVelocity.y);

        if (moveVector.x > 0) facingDirection = 1f;
        else if (moveVector.x < 0) facingDirection = -1f;
    }

    void HandleStumbleDeceleration()
    {
        stumbleTimer -= Time.deltaTime;
        
        float speedMultiplier = Mathf.Clamp01(stumbleTimer / 5f); 
        rb.linearVelocity = new Vector2(lockedFacingDirection * moveSpeed * speedMultiplier, rb.linearVelocity.y);

        if (stumbleTimer <= 0f)
        {
            isStumbling = false;
            if (cubeSprite != null) cubeSprite.color = normalColor;
            Debug.Log("Stumble complete. Player regained control.");
        }
    }

    void HandlePassiveStumble()
    {
        if (isVaulting || !isGrounded || isStumbling || parkourAction.action.IsPressed()) return;

        Vector2 fireDirection = new Vector2(facingDirection, 0f);
        Vector2 originPos = new Vector2(footPosition.position.x, footPosition.position.y + 0.1f);
        
        // Increased raycast to 1.0f so it triggers before your physical collider smacks the box
        RaycastHit2D hit = Physics2D.Raycast(originPos, fireDirection, 1f, obstacleLayer);
        
        if (hit.collider != null && hit.collider.bounds.size.y < 0.6f)
        {
            StartCoroutine(StumbleRoutine(hit.collider));
        }
    }

    void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded && !isVaulting && !isStumbling)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }
    }

    void HandleParkourHold()
    {
        if (isVaulting || !isGrounded || isStumbling) return; 
        
        if (parkourAction.action.IsPressed())
        {
            FireRaycast(false);
        }
    }

    void OnTrickTap(InputAction.CallbackContext context)
    {
        if (isVaulting || !isGrounded || isStumbling) return; 
        FireRaycast(true);
    }

    void FireRaycast(bool isTricking)
    {
        if (!isGrounded) return; // Fix: No more air-vaulting

        Vector2 fireDirection = new Vector2(facingDirection, 0f);
        Vector2 originPos = new Vector2(footPosition.position.x, footPosition.position.y + 0.1f);
        
        RaycastHit2D hit = Physics2D.Raycast(originPos, fireDirection, rayDistance, obstacleLayer);

        if (hit.collider != null)
        {
            Calculate(hit.collider, isTricking);
        }
    }

    void Calculate(Collider2D obstacle, bool isTricking)
    {
        float obstacleHeight = obstacle.bounds.size.y;
        float obstacleClearance = obstacle.bounds.size.x;
        float inputY = moveAction.action.ReadValue<Vector2>().y;

        if (inputY > 0.5f && !isTricking)
        {
            Springboard();
            return;
        }

        if (obstacleHeight < 0.6f)
        {
            float duration = isTricking ? 0.3f : 0.2f;
            Debug.Log(isTricking ? "Executing: TRICK HOP" : "Executing: SPEED STEP");
            StartCoroutine(VaultRoutine(obstacle, duration, 0.1f)); 
        }
        else if (obstacleHeight <= 2.0f)
        {
            if (obstacleClearance <= 1.5f)
            {
                float duration = isTricking ? 0.6f : 0.3f;
                Debug.Log(isTricking ? "Executing: TRICK VAULT" : "Executing: SPEED VAULT");
                StartCoroutine(VaultRoutine(obstacle, duration, 1.0f));
            }
            else if (obstacleClearance <= 4.0f)
            {
                float duration = isTricking ? 0.7f : 0.4f;
                Debug.Log(isTricking ? "Executing: TRICK KONG" : "Executing: SPEED KONG");
                StartCoroutine(VaultRoutine(obstacle, duration, 1.5f)); 
            }
            else
            {
                Debug.Log("Executing: MANTLE");
                StartCoroutine(MantleRoutine(obstacle));
            }
        }
    }

    void Springboard()
    {
        Debug.Log("Executing: SPRINGBOARD");
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, springboardForce);
    }

    System.Collections.IEnumerator VaultRoutine(Collider2D obstacle, float duration, float extraHeight)
    {
        isVaulting = true;
        rb.isKinematic = true;
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false; 

        Vector2 startPos = transform.position;
        float landX = facingDirection == 1 ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
        Vector2 endPos = new Vector2(landX, startPos.y); 

        float heightToClear = (obstacle.bounds.max.y - startPos.y) + extraHeight;
        float timePassed = 0f;

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / duration;
            float heightModifier = Mathf.Sin(linearT * Mathf.PI) * heightToClear;
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
            currentPos.y += heightModifier;
            rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Collider2D>().enabled = true; 
        rb.isKinematic = false;
        isVaulting = false;
    }

    System.Collections.IEnumerator MantleRoutine(Collider2D obstacle)
    {
        isVaulting = true;
        rb.isKinematic = true;
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        Vector2 startPos = transform.position;
        float edgeX = facingDirection == 1 ? obstacle.bounds.min.x + (playerWidth / 2f) : obstacle.bounds.max.x - (playerWidth / 2f);
        Vector2 endPos = new Vector2(edgeX, obstacle.bounds.max.y + (playerWidth / 2f));

        float timePassed = 0f;
        float duration = 0.25f; 

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / duration;
            float heightModifier = Mathf.Sin(linearT * Mathf.PI) * 0.5f; 
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
            currentPos.y += heightModifier;
            rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Collider2D>().enabled = true;
        rb.isKinematic = false;
        isVaulting = false;
    }

    System.Collections.IEnumerator StumbleRoutine(Collider2D obstacle)
    {
        Debug.Log("STUMBLE TRIGGERED! Color changed to Red.");
        isVaulting = true;
        isStumbling = true;
        
        if (cubeSprite != null) cubeSprite.color = stumbleColor;
        
        rb.isKinematic = true;
        rb.linearVelocity = Vector2.zero;
        GetComponent<Collider2D>().enabled = false;

        Vector2 startPos = transform.position;
        float landX = facingDirection == 1 ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
        Vector2 endPos = new Vector2(landX, startPos.y); 

        float timePassed = 0f;
        float duration = 0.4f; 

        while (timePassed < duration)
        {
            timePassed += Time.deltaTime;
            float linearT = timePassed / duration;
            
            // Tiny positive bump so you hop the object, no more downward arc
            float sagModifier = Mathf.Sin(linearT * Mathf.PI) * 0.2f; 
            Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
            currentPos.y += sagModifier;
            rb.MovePosition(currentPos);
            yield return new WaitForFixedUpdate();
        }

        GetComponent<Collider2D>().enabled = true;
        rb.isKinematic = false;
        isVaulting = false;
        
        lockedFacingDirection = facingDirection;
        stumbleTimer = 3f;
    }
}