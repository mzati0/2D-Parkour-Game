using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Player
{
    public class ParkourController : MonoBehaviour
    {
        #region 1. VARIABLES: Core Components & State
        [Header("Core Components")]
        public Rigidbody2D rb;
        public SpriteRenderer cubeSprite;
        private CapsuleCollider2D capsuleCollider2D;
        [SerializeField] private GameObject sprites;
        public Animator ghostAnimator;

        [Header("State Flags")]
        public bool isVaulting;
        public bool isGrounded;
        public bool isStumbling;
        public bool isSliding;
        public bool isCrouching;
        public bool isHanging;
        public bool isRagdolling;
        public bool isScrambling;
        public bool isWallSliding;
        private bool hasScrambled; 
        private Collider2D currentLedge;
        private float stumbleTimer = 1f;
        #endregion

        #region 2. VARIABLES: Physics & Movement
        [Header("Input Actions")]
        public InputActionReference moveAction;
        public InputActionReference jumpAction;
        public InputActionReference parkourAction;
        public InputActionReference trickAction;
        public InputActionReference flowAction;

        [Header("Movement Mechanics")]
        public float accelerationRate = 12f; 
        public float decelerationRate = 15f; 
        public float turnaroundMultiplier = 3f; 
        public float crouchSpeedMultiplier = 0.5f; 
        public float slideDecelerationRate = 6f; 
        public float jumpForce = 20f;
        public float airControlMultiplier = 0.5f; 
        public float springboardForce = 25f;

        [Header("Wall Movement")]
        public float wallScrambleSpeed = 12f;
        public float wallScrambleDuration = 0.35f;
        public float wallSlideSpeed = 4f;
    
        [Header("Spatial Data")]
        [Tooltip("Locked to 1f so raycasts do not flip when walking backwards.")]
        public float facingDirection = 1f; 
        private float lockedFacingDirection = 1f;
        private float animationDirection = 1f;
        public float playerWidth = 1f;
        #endregion

        #region 3. VARIABLES: Flow System
        [Header("Flow System")]
        public float maxFlowMeter = 100f;
        public float currentFlowMeter = 100f; 
        public float burstSpeedBonus = 8f; 
        public float burstCost = 30f;
        public float maxBurstActivationSpeed = 8f; 
        
        [Tooltip("Current active top speed, dynamically scaled by Flow.")]
        public float topSpeed = 12f;
        public float minTopSpeed = 36f; 
        public float maxTopSpeed = 45f; 
        public float minAcceleration = 8f;
        public float maxAcceleration = 14f;
        
        public float minFlowRegen = 1f; 
        public float maxFlowRegen = 5f; 
        public float flowDecayRate = 50f; 
        public float flowDecaySpeedThreshold = 5f;
        private float currentFlowRegenRate;
        #endregion

        #region 4. VARIABLES: Multi-Sensor Array
        [Header("Multi-Sensor Array")]
        public Transform headPosition; 
        public Transform chestPosition;
        public Transform waistPosition; 
        public Transform shinPosition;  
        public Transform footPosition;  

        [Header("Sensor Dimensions")]
        public Vector2 headBoxSize = new(0.2f, 0.2f);
        public Vector2 chestBoxSize = new(0.2f, 0.3f);
        public Vector2 waistBoxSize = new(0.2f, 0.3f);
        public Vector2 shinBoxSize = new(0.2f, 0.3f);
        public Vector2 toeBoxSize = new(0.2f, 0.1f);
        public float sensorCastDistance = 2f;
        public float wallContactThreshold = 1f; 
        public float groundCheckDistance = 0.2f;
        
        public LayerMask obstacleLayer;
        public LayerMask groundLayer;

        // Internal Sensor Memory
        private bool isCollidingFront;
        private RaycastHit2D headHit;
        private RaycastHit2D chestHit;
        private RaycastHit2D waistHit;
        private RaycastHit2D shinHit;
        private RaycastHit2D toeHit;
        #endregion
    
        #region 5. VARIABLES: UI & Visuals
        [Header("State Colors")]
        public Color normalColor = Color.blue;
        public Color stumbleColor = Color.red;
        public Color slideColor = Color.yellow; 
        public Color crouchColor = new(1f, 0.5f, 0f); 

        [Header("UI & Feedback")]
        public TextMeshProUGUI actionText;
        public TextMeshProUGUI speedKmhText;
        public TextMeshProUGUI speedUsText;
        public TextMeshProUGUI distanceText;
        public TextMeshProUGUI difficultyText;
        public TextMeshProUGUI flowCapacityText;
        public TextMeshProUGUI flowRateText;
        public Image flowMeterFill;
        public float uiLerpSpeed = 10f; 
        private Coroutine currentTextCoroutine;

        [Header("Debug")]
        [SerializeField] private bool showUnits;
        #endregion

        #region UNITY LIFECYCLE
        private void Start()
        {
            capsuleCollider2D = GetComponent<CapsuleCollider2D>();
            if (cubeSprite == null) cubeSprite = GetComponent<SpriteRenderer>(); 
            if (cubeSprite) cubeSprite.color = normalColor;
            
            facingDirection = 1f; // Hard-lock facing direction on startup
        }

        private void OnEnable()
        {
            moveAction.action.Enable();
            jumpAction.action.Enable();
            parkourAction.action.Enable();
            trickAction.action.Enable();
            flowAction.action.Enable(); 

            jumpAction.action.performed += OnJump;
            trickAction.action.performed += OnTrickTap;
            flowAction.action.performed += OnFlowBurst; 
        }

        private void OnDisable()
        {
            moveAction.action.Disable();
            jumpAction.action.Disable();
            parkourAction.action.Disable();
            trickAction.action.Disable();
            flowAction.action.Disable();

            jumpAction.action.performed -= OnJump;
            trickAction.action.performed -= OnTrickTap;
            flowAction.action.performed -= OnFlowBurst;
        }

        private void Update()
        {
            if (isRagdolling)
            {
                rb.linearVelocity = Vector2.zero; 
                HandleUI(); 
                return; 
            }

            CheckGrounded();
            ScanFrontObstacles(); 
            
            if (!isGrounded && (isSliding || isCrouching)) StandUp();
            
            if (isHanging)
            {
                HandleHangingState();
                return; 
            }

            CheckWallScramble();
            HandleWallSlide();
            CheckAirborneLedgeGrab();

            if (isStumbling && !isVaulting)
            {
                HandleStumbleDeceleration();
                return;
            }
        
            HandleFlowEconomy();
            HandleMovement();
            HandleCrouchAndSlide();
            HandleParkourHold();
            HandlePassiveStumble();
            HandleAnimation();
            HandleUI(); 
        }
        #endregion

        #region INPUT HANDLERS
        private void OnJump(InputAction.CallbackContext context)
        {
            if (isVaulting) 
            {
                Springboard();
                return;
            }
            if (isStumbling || isSliding || isCrouching || isHanging) return;

            bool isFlushWall = (chestHit.collider && chestHit.distance <= wallContactThreshold) &&
                               (waistHit.collider && waistHit.distance <= wallContactThreshold) &&
                               (shinHit.collider && shinHit.distance <= wallContactThreshold) &&
                               (toeHit.collider && toeHit.distance <= wallContactThreshold);

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            bool holdingForward = Mathf.Abs(moveInput.x) > 0.1f && Mathf.Approximately(Mathf.Sign(moveInput.x), facingDirection);
            bool holdingUp = moveInput.y > 0.5f;

            if (isGrounded && isFlushWall && holdingForward && holdingUp)
            {
                hasScrambled = true; 
                StartCoroutine(WallScrambleRoutine());
                return;
            }

            if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                if (FireParkourAction(false)) return; 
            }

            if (!isGrounded) return; 

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            ghostAnimator.SetTrigger("Jump");
        }

        private void OnTrickTap(InputAction.CallbackContext context)
        {
            if (isVaulting || !isGrounded || isStumbling) return; 
            currentFlowMeter = Mathf.Clamp(currentFlowMeter + 10f, 0, maxFlowMeter);
            FireParkourAction(true);
        }

        private void OnFlowBurst(InputAction.CallbackContext context)
        {
            if (!isGrounded || isVaulting || isStumbling || isSliding || isCrouching) return;

            if (Mathf.Abs(rb.linearVelocity.x) > maxBurstActivationSpeed)
            {
                DisplayAction("MAX SPEED!", Color.gray); 
                return; 
            }

            if (currentFlowMeter >= burstCost)
            {
                currentFlowMeter -= burstCost;
                DisplayAction("FLOW BURST!", Color.white);
            
                float newSpeed = Mathf.Abs(rb.linearVelocity.x) + burstSpeedBonus;
                rb.linearVelocity = new Vector2(facingDirection * newSpeed, rb.linearVelocity.y);
            }
        }
        #endregion

        #region CORE MOVEMENT
        private void HandleMovement()
        {
            if (isVaulting || isHanging || isScrambling || isWallSliding) return; 

            if (isSliding)
            {
                float slideVelocity = Mathf.MoveTowards(rb.linearVelocity.x, 0f, slideDecelerationRate * Time.deltaTime);
                rb.linearVelocity = new Vector2(slideVelocity, rb.linearVelocity.y);
                return; 
            }

            Vector2 moveVector = moveAction.action.ReadValue<Vector2>();
            float targetSpeed;
            float currentVelocityX = rb.linearVelocity.x;

            if (Mathf.Abs(moveVector.x) > 0.1f) 
            {
                float inputDirection = Mathf.Sign(moveVector.x); 
                animationDirection = inputDirection; // Send input intention to the animator
                
                bool isMovingBackward = !Mathf.Approximately(inputDirection, facingDirection);
                
                // Halve the active top speed if moving backwards
                float activeSpeedLimit = isMovingBackward ? (topSpeed * 0.4f) : topSpeed;
                
                targetSpeed = inputDirection * (isCrouching ? activeSpeedLimit * crouchSpeedMultiplier : activeSpeedLimit);
                
                float accelToUse = accelerationRate;
                if (!isGrounded) accelToUse *= airControlMultiplier; 

                // Turnaround penalty
                if (!Mathf.Approximately(inputDirection, Mathf.Sign(currentVelocityX)) && Mathf.Abs(currentVelocityX) > 0.5f)
                {
                    accelToUse *= turnaroundMultiplier; 
                }
            
                currentVelocityX = Mathf.MoveTowards(currentVelocityX, targetSpeed, accelToUse * Time.deltaTime);
            }
            else
            {
                float decelToUse = isGrounded ? decelerationRate : 0f; 
                currentVelocityX = Mathf.MoveTowards(currentVelocityX, 0f, decelToUse * Time.deltaTime);
            }

            rb.linearVelocity = new Vector2(currentVelocityX, rb.linearVelocity.y);
        }

        private void HandleCrouchAndSlide()
        {
            if (!isGrounded || isVaulting || isStumbling) return;

            float inputY = moveAction.action.ReadValue<Vector2>().y;
            bool wantsToGoDown = inputY < -0.5f; 
            bool momentumDead = Mathf.Abs(rb.linearVelocity.x) < 1f; 

            if (wantsToGoDown)
            {
                if (!isSliding && !isCrouching)
                {
                    if (!momentumDead) StartSlide();
                    else StartCrouch();
                }
                else if (isSliding && momentumDead)
                {
                    TransitionToCrouch();
                }
            }
            else if (isSliding || isCrouching)
            {
                RaycastHit2D ceilingCheck = Physics2D.Raycast(footPosition.position, Vector2.up, 1.9f, obstacleLayer);
                if (!ceilingCheck.collider) StandUp(); 
            }
        }
        
        private void HandleFlowEconomy()
        {
            float currentAbsSpeed = Mathf.Abs(rb.linearVelocity.x);
            float currentFlowRatio = currentFlowMeter / maxFlowMeter;

            topSpeed = Mathf.Lerp(minTopSpeed, maxTopSpeed, currentFlowRatio);
            accelerationRate = Mathf.Lerp(minAcceleration, maxAcceleration, currentFlowRatio);

            if (currentAbsSpeed < flowDecaySpeedThreshold && isGrounded && !isVaulting)
            {
                currentFlowRegenRate = -flowDecayRate;
                currentFlowMeter += currentFlowRegenRate * Time.deltaTime;
            }
            else if (currentAbsSpeed >= flowDecaySpeedThreshold)
            {
                float speedRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, currentAbsSpeed);
                currentFlowRegenRate = Mathf.Lerp(minFlowRegen, maxFlowRegen, speedRatio);
                currentFlowMeter += currentFlowRegenRate * Time.deltaTime;
            }
            else 
            {
                currentFlowRegenRate = 0f;
            }

            currentFlowMeter = Mathf.Clamp(currentFlowMeter, 0f, maxFlowMeter);
        }
        #endregion

        #region PARKOUR & SENSORS
        private void CheckGrounded()
        {
           RaycastHit2D hit = Physics2D.Raycast(footPosition.position, Vector2.down, groundCheckDistance, groundLayer);
           isGrounded = hit.collider;

           if (isGrounded) hasScrambled = false; 
        }

        private void ScanFrontObstacles()
        {
            if (!headPosition || !chestPosition || !waistPosition || !shinPosition || !footPosition) return;

            Vector2 fireDirection = new Vector2(facingDirection, 0f);

            headHit = Physics2D.BoxCast(headPosition.position, headBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            chestHit = Physics2D.BoxCast(chestPosition.position, chestBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            waistHit = Physics2D.BoxCast(waistPosition.position, waistBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            shinHit = Physics2D.BoxCast(shinPosition.position, shinBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);
            toeHit = Physics2D.BoxCast(footPosition.position, toeBoxSize, 0f, fireDirection, sensorCastDistance, obstacleLayer);

            isCollidingFront = headHit.collider || chestHit.collider || waistHit.collider || shinHit.collider || toeHit.collider;
        }

        private void CheckWallScramble()
        {
            if (isGrounded || isScrambling || isVaulting || isHanging || hasScrambled) return;

            bool isFlushWall = (chestHit.collider && chestHit.distance <= wallContactThreshold) &&
                               (waistHit.collider && waistHit.distance <= wallContactThreshold) &&
                               (shinHit.collider && shinHit.distance <= wallContactThreshold) &&
                               (toeHit.collider && toeHit.distance <= wallContactThreshold);

            if (!isFlushWall) return;

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            bool holdingForward = Mathf.Abs(moveInput.x) > 0.1f && Mathf.Approximately(Mathf.Sign(moveInput.x), facingDirection);
            bool holdingUp = moveInput.y > 0.5f;

            if (holdingForward && holdingUp)
            {
                hasScrambled = true; 
                StartCoroutine(WallScrambleRoutine());
            }
        }

        private void HandleWallSlide()
        {
            if (isGrounded || isScrambling || isVaulting || isHanging) 
            {
                isWallSliding = false;
                return;
            }

            bool touchingWall = (chestHit.collider && chestHit.distance <= wallContactThreshold) || 
                                (waistHit.collider && waistHit.distance <= wallContactThreshold);

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            bool holdingForward = touchingWall && Mathf.Abs(moveInput.x) > 0.1f && Mathf.Approximately(Mathf.Sign(moveInput.x), facingDirection);

            if (holdingForward && rb.linearVelocity.y < 0f)
            {
                isWallSliding = true;
                rb.linearVelocity = new Vector2(0f, -wallSlideSpeed);
            }
            else
            {
                isWallSliding = false;
            }
        }

        private void CheckAirborneLedgeGrab()
        {
            if (isGrounded || isVaulting || isScrambling || rb.linearVelocity.y > 0.1f) return; 

            float ledgeCatchRadius = wallContactThreshold + 0.2f; 
            bool isChestHit = chestHit.collider && chestHit.distance <= ledgeCatchRadius;
            bool isWaistHit = waistHit.collider && waistHit.distance <= ledgeCatchRadius;
            bool headClear = !headHit.collider || headHit.distance > ledgeCatchRadius;

            if ((isChestHit || isWaistHit) && headClear)
            {
                Collider2D wallCollider = isChestHit ? this.chestHit.collider : this.waistHit.collider;
                Vector2 hitPoint = isChestHit ? this.chestHit.point : this.waistHit.point;
                
                Vector2 rayOrigin = new Vector2(hitPoint.x + (facingDirection * 0.1f), hitPoint.y + 1.5f);
                RaycastHit2D downHit = Physics2D.Raycast(rayOrigin, Vector2.down, 2.5f, obstacleLayer);

                if (downHit.collider)
                {
                    Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
                    bool holdingForward = Mathf.Abs(moveInput.x) > 0.1f && Mathf.Approximately(Mathf.Sign(moveInput.x), facingDirection);
                    bool holdingUp = moveInput.y > 0.5f;

                    if (holdingForward || holdingUp)
                    {
                        DisplayAction("AUTO MANTLE!", Color.cyan);
                        StartCoroutine(MantleRoutine(wallCollider));
                    }
                    else
                    {
                        SnapToLedge(hitPoint, downHit.point.y, wallCollider);
                    }
                }
            }
        }

        private void SnapToLedge(Vector2 wallHitPoint, float trueTopY, Collider2D ledgeCollider)
        {
            isHanging = true;
            currentLedge = ledgeCollider;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;

            float targetX = Mathf.Approximately(facingDirection, 1f) 
                ? wallHitPoint.x - (playerWidth / 2f) 
                : wallHitPoint.x + (playerWidth / 2f);
            
            float targetY = trueTopY - 2f;
            transform.position = new Vector2(targetX, targetY);
            
            DisplayAction("LEDGE GRAB!", Color.white);
            if (cubeSprite) cubeSprite.color = normalColor;
        }

        private void HandleHangingState()
        {
            float inputY = moveAction.action.ReadValue<Vector2>().y;

            if (inputY > 0.5f) 
            {
                isHanging = false;
                DisplayAction("MANTLE!", Color.white);
                if (currentLedge) StartCoroutine(MantleRoutine(currentLedge));
            }
            else if (inputY < -0.5f) 
            {
                isHanging = false;
                rb.bodyType = RigidbodyType2D.Dynamic;
                currentLedge = null;
                DisplayAction("DROP", Color.gray);
            }
        }

        private bool FireParkourAction(bool isTricking)
        {
            if (!isCollidingFront) return false; 
            return CalculateParkourMatrix(isTricking); 
        }

        private bool CalculateParkourMatrix(bool isTricking)
        {
            if (waistHit.collider && !chestHit.collider)
            {
                float trueTopY;
                float obstacleDepth = CalculateObstacleDepth(waistHit, out trueTopY);
                float exactHeightToClear = (trueTopY - transform.position.y);

                if (obstacleDepth < 1.0f)
                {
                    float duration = isTricking ? 0.4f : 0.2f;
                    DisplayAction(isTricking ? "TRICK VAULT!" : "SPEED VAULT!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(waistHit.collider, duration, exactHeightToClear));
                }
                else if (obstacleDepth <= 3.5f)
                {
                    float duration = isTricking ? 0.7f : 0.4f;
                    DisplayAction(isTricking ? "TRICK KONG!" : "SPEED KONG!", isTricking ? Color.cyan : Color.white);
                    StartCoroutine(VaultRoutine(waistHit.collider, duration, exactHeightToClear));
                }
                else
                {
                    DisplayAction("Mantle!", Color.white);
                    StartCoroutine(MantleRoutine(waistHit.collider));
                }
                return true; 
            }

            if ((shinHit.collider || toeHit.collider) && !waistHit.collider)
            {
                float duration = isTricking ? 0.4f : 0.2f;
                DisplayAction(isTricking ? "TRICK HOP!" : "SPEED STEP!", isTricking ? Color.cyan : Color.white);
                StartCoroutine(VaultRoutine(shinHit.collider ? shinHit.collider : toeHit.collider, duration, 0.1f));
                return true; 
            }

            return false; 
        }

        private float CalculateObstacleDepth(RaycastHit2D forwardHit, out float trueTopY)
        {
            float maxSearchDistance = 4f; 
            float maxClimbHeight = 2.5f; 
            float scanStep = 0.1f;
            
            trueTopY = forwardHit.point.y; 
            bool foundTop = false;

            Vector2 verticalScanOrigin = new Vector2(forwardHit.point.x - (facingDirection * 0.05f), forwardHit.point.y);

            for (float yOffset = scanStep; yOffset <= maxClimbHeight; yOffset += scanStep)
            {
                Vector2 stepOrigin = new Vector2(verticalScanOrigin.x, verticalScanOrigin.y + yOffset);
                RaycastHit2D hit = Physics2D.Raycast(stepOrigin, new Vector2(facingDirection, 0f), 0.2f, obstacleLayer);

                if (!hit.collider)
                {
                    trueTopY = stepOrigin.y - (scanStep / 2f); 
                    foundTop = true;
                    break;
                }
            }

            if (!foundTop) return maxSearchDistance + 1f; 

            float depthStep = 0.25f;
            float startX = forwardHit.point.x + (facingDirection * 0.1f);

            for (float depth = 0f; depth <= maxSearchDistance; depth += depthStep)
            {
                Vector2 rayOrigin = new Vector2(startX + (facingDirection * depth), trueTopY + 0.2f);
                RaycastHit2D downHit = Physics2D.Raycast(rayOrigin, Vector2.down, 0.5f, obstacleLayer);

                if (!downHit.collider || downHit.point.y < trueTopY - 0.2f) return depth; 
            }
            
            return maxSearchDistance; 
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isVaulting || !isGrounded || isStumbling || parkourAction.action.IsPressed() || isHanging || isScrambling) return;

            if (((1 << collision.gameObject.layer) & obstacleLayer) != 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                
                if (Mathf.Abs(contact.normal.x) > 0.5f)
                {
                    if (waistHit.collider || chestHit.collider || headHit.collider) return;

                    bool isShinHigh = shinHit.collider; 
                    bool isToeHigh = toeHit.collider && !shinHit.collider;

                    if (!isShinHigh && !isToeHigh) return; 

                    float currentSpeed = Mathf.Abs(rb.linearVelocity.x);
                    float speed10 = 3.33f; 
                    float speed24 = 8.0f;  
                    float speed36 = 12.0f; 

                    int severity = 0; 

                    if (isShinHigh)
                    {
                        if (currentSpeed < speed10) severity = 1;
                        else if (currentSpeed < speed24) severity = 2;
                        else severity = 3;
                    }
                    else if (isToeHigh)
                    {
                        if (currentSpeed < speed10) severity = 0;
                        else if (currentSpeed < speed24) severity = 1;
                        else if (currentSpeed < speed36) severity = 2;
                        else severity = 3;
                    }

                    if (severity == 0) return;

                    if (severity < 3 && currentFlowMeter >= 50f)
                    {
                        currentFlowMeter -= 50f;
                        DisplayAction("STUMBLE PROTECTED!", Color.cyan);
                        if (cubeSprite) cubeSprite.color = Color.cyan;
                        
                        StartCoroutine(VaultRoutine(collision.collider, 0.2f, 0.15f));
                        return;
                    }

                    StartCoroutine(DynamicStumbleRoutine(collision.collider, severity));
                }
            }
        }
        #endregion

        #region ACTION STATES
        private void Springboard()
        {
            StopAllCoroutines(); 
            isVaulting = false;
            capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic;

            rb.linearVelocity = new Vector2(facingDirection * topSpeed, springboardForce);
            DisplayAction("SPRINGBOARD!", Color.magenta);
        }

        private void StartSlide()
        {
            isSliding = true;
            DisplayAction("SLIDE!", slideColor);
            ghostAnimator.SetTrigger("Slide");
            ApplyDownVisuals(slideColor);
        }

        private void StartCrouch()
        {
            isCrouching = true;
            DisplayAction("CROUCH!", crouchColor);
            ApplyDownVisuals(crouchColor);
        }

        private void TransitionToCrouch()
        {
            isSliding = false;
            isCrouching = true;
            DisplayAction("CROUCH WALK", crouchColor);
            ApplyDownVisuals(crouchColor);
        }

        private void StandUp()
        {
            isSliding = false;
            isCrouching = false;
            capsuleCollider2D.offset = new Vector2(0, 1f);
            capsuleCollider2D.size = new Vector2(1f, 2f);
            sprites.transform.localPosition = Vector3.zero;
            sprites.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            if (cubeSprite) cubeSprite.color = normalColor;
        }

        private void HandlePassiveStumble()
        {
            if (isVaulting || !isGrounded || isStumbling || parkourAction.action.IsPressed()) return;

            if (toeHit.collider && !waistHit.collider)
            {
                float trueTopY;
                float obstacleDepth = CalculateObstacleDepth(toeHit, out trueTopY);

                bool isVaultableSize = obstacleDepth < 1.5f;
                float armorCost = 50f; 

                if (isVaultableSize && currentFlowMeter >= armorCost)
                {
                    currentFlowMeter -= armorCost;
                    DisplayAction("STUMBLE PROTECTED!", Color.cyan);
                    if (cubeSprite) cubeSprite.color = Color.cyan;
                    StartCoroutine(VaultRoutine(toeHit.collider, 0.2f, 0.1f)); 
                }
                else
                {
                    StartCoroutine(StumbleRoutine(toeHit.collider));
                }
            }
        }

        private void HandleParkourHold()
        {
            if (isVaulting || !isGrounded || isStumbling) return; 
            if (parkourAction.action.IsPressed()) FireParkourAction(false);
        }

        private void HandleStumbleDeceleration()
        {
            stumbleTimer -= Time.deltaTime;
        
            float speedMultiplier = Mathf.Clamp01(stumbleTimer / 3f); 
            rb.linearVelocity = new Vector2(lockedFacingDirection * topSpeed * speedMultiplier, rb.linearVelocity.y);

            if (stumbleTimer <= 0f)
            {
                isStumbling = false;
                if (cubeSprite) cubeSprite.color = normalColor;
                DisplayAction("RECOVERED", Color.white);
            }
        }
        #endregion

        #region COROUTINES
        private IEnumerator WallScrambleRoutine()
        {
            isScrambling = true;
            DisplayAction("SCRAMBLE!", Color.white);
            
            float timePassed = 0f;
            
            while (timePassed < wallScrambleDuration && isCollidingFront)
            {
                timePassed += Time.deltaTime;
                rb.linearVelocity = new Vector2(0f, wallScrambleSpeed); 
                
                if (isHanging || isVaulting) 
                {
                    isScrambling = false;
                    yield break; 
                }
                
                yield return new WaitForFixedUpdate();
            }
            
            isScrambling = false;
        }
        
        private IEnumerator VaultRoutine(Collider2D obstacle, float duration, float extraHeight)
        {
            isVaulting = true;
            float entrySpeedX = rb.linearVelocity.x;
            rb.bodyType = RigidbodyType2D.Kinematic; 
            rb.linearVelocity = Vector2.zero;
            capsuleCollider2D.enabled = false; 

            Vector2 startPos = transform.position;
            float clearancePadding = 0.25f;
            float landX = Mathf.Approximately(facingDirection, 1) 
                ? obstacle.bounds.max.x + (playerWidth / 2f) + clearancePadding 
                : obstacle.bounds.min.x - (playerWidth / 2f) - clearancePadding;
        
            Vector2 endPos = new Vector2(landX, startPos.y + 0.15f); 
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

            capsuleCollider2D.enabled = true; 
            rb.bodyType = RigidbodyType2D.Dynamic; 
            isVaulting = false;
            rb.linearVelocity = new Vector2(entrySpeedX, rb.linearVelocity.y);
            if (cubeSprite) cubeSprite.color = normalColor;
        }

        private IEnumerator MantleRoutine(Collider2D obstacle)
        {
            isVaulting = true;
            rb.bodyType = RigidbodyType2D.Kinematic; 
            rb.linearVelocity = Vector2.zero;
            capsuleCollider2D.enabled = false;

            float obstacleHeight = obstacle.bounds.size.y;
            float heightRatio = Mathf.InverseLerp(1f, 4f, obstacleHeight);
            float totalDuration = Mathf.Lerp(0.3f, 1f, heightRatio);

            Vector2 startPos = transform.position;
            float edgeX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.min.x + (playerWidth / 2f) : obstacle.bounds.max.x - (playerWidth / 2f);
            float topY = obstacle.bounds.max.y + (playerWidth / 2f);

            float climbDuration = totalDuration * 0.7f;
            float pullDuration = totalDuration * 0.3f;

            float timePassed = 0f;
            while (timePassed < climbDuration)
            {
                timePassed += Time.deltaTime;
                float t = timePassed / climbDuration;
                float easeT = Mathf.Sin(t * Mathf.PI / 2f); 
                rb.MovePosition(new Vector2(startPos.x, Mathf.Lerp(startPos.y, topY, easeT)));
                yield return new WaitForFixedUpdate();
            }

            timePassed = 0f;
            Vector2 topPos = new Vector2(startPos.x, topY); 
        
            while (timePassed < pullDuration)
            {
                timePassed += Time.deltaTime;
                float t = timePassed / pullDuration;
                rb.MovePosition(new Vector2(Mathf.Lerp(topPos.x, edgeX, t), topY));
                yield return new WaitForFixedUpdate();
            }

            capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic; 
            isVaulting = false;
        }

        private IEnumerator StumbleRoutine(Collider2D obstacle)
        {
            DisplayAction("STUMBLE!", stumbleColor);
            isVaulting = true;
            isStumbling = true;
            if (cubeSprite) cubeSprite.color = stumbleColor;
        
            rb.bodyType = RigidbodyType2D.Kinematic; 
            rb.linearVelocity = Vector2.zero;
            capsuleCollider2D.enabled = false;

            Vector2 startPos = transform.position;
            float landX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
            Vector2 endPos = new Vector2(landX, startPos.y); 

            float timePassed = 0f;
            const float duration = 0.4f; 

            while (timePassed < duration)
            {
                timePassed += Time.deltaTime;
                float linearT = timePassed / duration;
                float sagModifier = Mathf.Sin(linearT * Mathf.PI) * 0.2f; 
                Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
                currentPos.y += sagModifier;
                rb.MovePosition(currentPos);
                yield return new WaitForFixedUpdate();
            }

            capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic; 
            isVaulting = false;
            lockedFacingDirection = facingDirection;

            float speedRatio = Mathf.Abs(rb.linearVelocity.x) / topSpeed;
            stumbleTimer = Mathf.Clamp(5f * speedRatio, 1f, 5f);
        }
        
        private IEnumerator DynamicStumbleRoutine(Collider2D obstacle, int severity)
        {
            isVaulting = true;
            isStumbling = true;
            
            Color flashColor = Color.red;
            float tripDuration = 0.5f;    
            float recoveryPenalty = 3f;    

            if (severity == 1) 
            {
                flashColor = slideColor; 
                tripDuration = 0.15f;
                recoveryPenalty = 0.5f;
            }
            else if (severity == 2) 
            {
                flashColor = new Color(1f, 0.5f, 0f); 
                tripDuration = 0.3f;
                recoveryPenalty = 1.5f;
            }

            DisplayAction("STUMBLE!", flashColor);
            if (cubeSprite) cubeSprite.color = flashColor;
        
            rb.bodyType = RigidbodyType2D.Kinematic; 
            rb.linearVelocity = Vector2.zero;
            capsuleCollider2D.enabled = false;

            Vector2 startPos = transform.position;
            float landX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.max.x + (playerWidth / 2f) : obstacle.bounds.min.x - (playerWidth / 2f);
            Vector2 endPos = new Vector2(landX, startPos.y); 

            float timePassed = 0f;

            while (timePassed < tripDuration)
            {
                timePassed += Time.deltaTime;
                float linearT = timePassed / tripDuration;
                float sagModifier = Mathf.Sin(linearT * Mathf.PI) * 0.2f; 
                Vector2 currentPos = Vector2.Lerp(startPos, endPos, linearT);
                currentPos.y += sagModifier;
                rb.MovePosition(currentPos);
                yield return new WaitForFixedUpdate();
            }

            capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic; 
            isVaulting = false;
            lockedFacingDirection = facingDirection;

            stumbleTimer = recoveryPenalty;
        }
        #endregion

        #region UI & ANIMATOR
        private void HandleUI()
        {
            if (flowMeterFill)
            {
                float targetFill = currentFlowMeter / maxFlowMeter;
                flowMeterFill.fillAmount = Mathf.Lerp(flowMeterFill.fillAmount, targetFill, Time.deltaTime * uiLerpSpeed);
            }

            if (speedKmhText)
            {
                float trueSpeed = Mathf.Abs(rb.linearVelocity.x);
                speedKmhText.text = (trueSpeed*3).ToString("F0") + " Km/h"; 
                float colorRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, trueSpeed);
                speedKmhText.color = Color.Lerp(Color.white, Color.cyan, colorRatio);
            }

            if (speedUsText && showUnits)
            {
                float trueSpeed = Mathf.Abs(rb.linearVelocity.x);
                speedUsText.text = (trueSpeed).ToString("F0") + " U/s";
                float colorRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, trueSpeed);
                speedUsText.color = Color.Lerp(Color.white, Color.cyan, colorRatio);
            }

            if (distanceText)
            {
                float trueDistance = Mathf.Abs(transform.position.x);
                distanceText.text = (trueDistance*3).ToString("F0") + " m";
            }

            if(difficultyText)
            {
                float distance = Mathf.Abs(transform.position.x);

                if (distance < 200f) difficultyText.text = "EASY";
                else if (distance < 500f) difficultyText.text = "MEDIUM";
                else if (distance < 800f) difficultyText.text = "HARD";
                else difficultyText.text = "HAHAHA"; 
            }

            if (flowCapacityText) flowCapacityText.text = currentFlowMeter.ToString("F0") + " / " + maxFlowMeter.ToString("F0");
        
            if (flowRateText)
            {
                if (currentFlowRegenRate > 0.01f) 
                {
                    flowRateText.gameObject.SetActive(true);
                    flowRateText.text = "+" + currentFlowRegenRate.ToString("F1") + " / sec";
                }
                else
                {
                    flowRateText.gameObject.SetActive(false);
                }
            }
        }

        private void HandleAnimation()
        {
            float currentAbsSpeed = Mathf.Abs(rb.linearVelocity.x);
            float normalizedSpeed = 0f; 

            if (currentAbsSpeed > 0.1f) 
            {
                if (currentAbsSpeed <= minTopSpeed)
                {
                    normalizedSpeed = Mathf.InverseLerp(0f, minTopSpeed, currentAbsSpeed);
                }
                else
                {
                    normalizedSpeed = 1f + Mathf.InverseLerp(minTopSpeed, maxTopSpeed, currentAbsSpeed);
                }
            }

            ghostAnimator.SetFloat("Speed", normalizedSpeed);
            
            float inputX = moveAction.action.ReadValue<Vector2>().x;
            bool hasInput = Mathf.Abs(inputX) > 0.1f;

            ghostAnimator.SetFloat("Direction", animationDirection); // Updated this line!
            ghostAnimator.SetBool("HasInput", hasInput); 
            ghostAnimator.SetBool("IsSliding", isSliding);
            ghostAnimator.SetBool("IsCrouching", isCrouching);
            ghostAnimator.SetBool("IsGrounded", isGrounded);

            bool isBracing = false;

            if (!isGrounded && rb.linearVelocity.y < -0.1f) 
            {
                float t = 0.417f; 
                float v = Mathf.Abs(rb.linearVelocity.y);
                float a = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);

                float predictDistance = (v * t) + (0.5f * a * (t * t));   
                RaycastHit2D braceHit = Physics2D.Raycast(footPosition.position, Vector2.down, predictDistance, groundLayer);
        
                if (braceHit.collider) isBracing = true; 
            }
    
            ghostAnimator.SetBool("IsBracing", isBracing);
        }

        private void ApplyDownVisuals(Color stateColor)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
            capsuleCollider2D.offset = new Vector2(0f, 0.5f);
            capsuleCollider2D.size = new Vector2(0.5f, 1f);
            sprites.transform.localPosition = new Vector3(1f, 0f, 0f);
            sprites.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (cubeSprite) cubeSprite.color = stateColor;
        }

        private void DisplayAction(string text, Color color)
        {
            if (!actionText) return;
            if (currentTextCoroutine != null) StopCoroutine(currentTextCoroutine);
            currentTextCoroutine = StartCoroutine(ClearTextAfterDelay(text, color));
        }

        private IEnumerator ClearTextAfterDelay(string text, Color color)
        {
            actionText.text = text;
            actionText.color = color;
            yield return new WaitForSeconds(1.5f); 
            actionText.text = "";
        }

        private void OnDrawGizmos()
        {
            if (!headPosition || !chestPosition || !waistPosition || !shinPosition || !footPosition) return;

            Vector3 offset = new Vector3(facingDirection * (sensorCastDistance / 2f), 0f, 0f);

            void DrawSweep(Transform t, Vector2 size, RaycastHit2D hit)
            {
                Gizmos.color = hit.collider ? Color.red : Color.green;
                Vector3 sweepSize = new Vector3(size.x + sensorCastDistance, size.y, 0f);
                Gizmos.DrawWireCube(t.position + offset, sweepSize);
            }

            DrawSweep(headPosition, headBoxSize, headHit);
            DrawSweep(chestPosition, chestBoxSize, chestHit);
            DrawSweep(waistPosition, waistBoxSize, waistHit);
            DrawSweep(shinPosition, shinBoxSize, shinHit);
            DrawSweep(footPosition, toeBoxSize, toeHit);
        }
        #endregion
    }
}