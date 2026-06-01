using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using ActiveRagoll; 

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
        
        [Header("External Systems")]
        public RagdollStateManager ragdollManager; 

        [Header("State Flags")]
        public bool isVaulting;
        public bool isDoingATricking;
        public bool isClimbing; 
        public bool isGrounded;
        public bool isSliding;
        public bool isCrouching;
        public bool isHanging;
        public bool isRagdolling;
        public bool isScrambling;
        public bool isWallSliding;
        public bool isFlushWall;
        public bool isVulnerable; 
        
        private bool hasScrambled; 
        private Collider2D currentLedge;
        private float defaultGravity; 
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
        public float wallScrambleSpeed = 6f; 
        public float wallScrambleDuration = 0.35f;
        public float wallSlideSpeed = 4f;
    
        [Header("Spatial Data")]
        public float facingDirection = 1f; 
        private float lockedFacingDirection = 1f;
        private float animationDirection = 1f;
        public float playerWidth = 1f;
        #endregion

        #region 3. VARIABLES: Flow System & Vault Library
        [Header("Flow System")]
        public float maxFlowMeter = 100f;
        public float currentFlowMeter = 100f; 
        public float burstSpeedBonus = 8f; 
        public float burstCost = 30f;
        public float maxBurstActivationSpeed = 8f; 
        
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

        [Header("Climb Customization")]
        public AnimationCurve climbYCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); 
        public AnimationCurve climbXCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); 
        public float climbAnimationDuration = 1.1f;
        
        [Header("Vault Library Arrays")]
        public VaultData[] speedVaults;
        public VaultData[] trickVaults;
        public VaultData[] speedKongs;
        public VaultData[] trickKongs;
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
        
        public float sensorCastDistance = 10f; 
        public float wallContactThreshold = 1f; 
        public float groundCheckDistance = 0.2f;
        
        public LayerMask obstacleLayer;
        public LayerMask groundLayer;

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
            
            facingDirection = 1f; 
            defaultGravity = rb.gravityScale; 
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
                CheckGrounded(); 
                HandleUI(); 
                return; 
            }
            
            ScanFrontObstacles(); 
            isFlushWall = (chestHit.collider && chestHit.distance <= wallContactThreshold) &&
                          (waistHit.collider && waistHit.distance <= wallContactThreshold) &&
                          (shinHit.collider && shinHit.distance <= wallContactThreshold) &&
                          (toeHit.collider && toeHit.distance <= wallContactThreshold);
            
            CheckGrounded();
            
            if (!isGrounded && (isSliding || isCrouching)) StandUp();
            
            if (isHanging)
            {
                HandleHangingState();
                return; 
            }

            CheckWallScramble();
            HandleWallSlide();
            CheckAirborneLedgeGrab();

    
        
            HandleFlowEconomy();
            HandleMovement();
            HandleCrouchAndSlide();
            HandleParkourHold();
            HandleAnimation();
            HandleUI(); 
        }
        #endregion

        #region INPUT HANDLERS
        private void OnJump(InputAction.CallbackContext context)
        {
            if (isVaulting && !isClimbing && !isDoingATricking) 
            {
                Springboard();
                return;
            }
            if (isSliding || isCrouching || isHanging || isVulnerable || isRagdolling) return;

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

        private Coroutine trickBufferCoroutine;

        private void OnTrickTap(InputAction.CallbackContext context)
        {
            if (isVaulting || !isGrounded || isRagdolling || isVulnerable || isClimbing) return; 
            if (!isCollidingFront) return; 

            // THE FIX: Start a 200ms input buffer instead of an instant pass/fail check.
            if (trickBufferCoroutine != null) StopCoroutine(trickBufferCoroutine);
            trickBufferCoroutine = StartCoroutine(TrickBufferRoutine());
        }

        private IEnumerator TrickBufferRoutine()
        {
            float bufferTime = 0.25f; // 250ms grace period 
            
            while (bufferTime > 0f)
            {
                if (isVaulting || isRagdolling || isVulnerable || isClimbing) yield break;

                // Try to perform the trick every frame for 0.25 seconds
                if (FireParkourAction(true))
                {
                    currentFlowMeter = Mathf.Clamp(currentFlowMeter + 10f, 0, maxFlowMeter);
                    yield break; // Success! Exit the coroutine.
                }
                
                bufferTime -= Time.deltaTime;
                yield return null;
            }

            // THE FIX: Only penalize if the buffer completely expired AND you were actually in faceplant range
            if (waistHit.collider && waistHit.distance <= 2f)
            {
                StartCoroutine(VulnerableRoutine());
            }
        }

        private void OnFlowBurst(InputAction.CallbackContext context)
        {
            if (!isGrounded || isVaulting || isSliding || isCrouching || isRagdolling) return;

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
            if (isVaulting || isHanging || isScrambling || isWallSliding || isClimbing) return; 

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
                animationDirection = inputDirection; 
                
                bool isMovingBackward = !Mathf.Approximately(inputDirection, facingDirection);
                
                float activeSpeedLimit = isMovingBackward ? (topSpeed * 0.4f) : topSpeed;
                
                targetSpeed = inputDirection * (isCrouching ? activeSpeedLimit * crouchSpeedMultiplier : activeSpeedLimit);
                
                float accelToUse = accelerationRate;
                if (!isGrounded) accelToUse *= airControlMultiplier; 

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
            if (!isGrounded || isVaulting || isClimbing) return;

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

            if (currentAbsSpeed < flowDecaySpeedThreshold && isGrounded && !isVaulting && !isClimbing)
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
            if (isGrounded || isScrambling || isVaulting || isHanging || hasScrambled || isClimbing) return;

            if (!isFlushWall) return;

            Vector2 moveInput = moveAction.action.ReadValue<Vector2>();
            bool holdingForward = Mathf.Abs(moveInput.x) > 0.1f && Mathf.Approximately(Mathf.Sign(moveInput.x), facingDirection);
            bool holdingUp = moveInput.y > 0.5f;

            if (holdingForward && holdingUp)
            {
                hasScrambled = true; 
                StartCoroutine(WallScrambleRoutine());
                ghostAnimator.SetTrigger("Scramble");
            }
        }

        private void HandleWallSlide()
        {
            if (isGrounded || isScrambling || isVaulting || isHanging || isClimbing) 
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
            if (isGrounded || isVaulting || isClimbing || (!isScrambling && rb.linearVelocity.y > 0.1f)) return; 

            float ledgeCatchRadius = wallContactThreshold + 0.2f; 
            
            bool isChestHit = chestHit.collider && chestHit.distance <= ledgeCatchRadius;
            bool headClear = !headHit.collider || headHit.distance > ledgeCatchRadius;

            if (isChestHit && headClear)
            {
                Collider2D wallCollider = chestHit.collider;
                Vector2 hitPoint = chestHit.point;
                
                Vector2 rayOrigin = new Vector2(hitPoint.x + (facingDirection * 0.1f), hitPoint.y + 1.5f);
                RaycastHit2D downHit = Physics2D.Raycast(rayOrigin, Vector2.down, 2.5f, obstacleLayer);

                if (downHit.collider)
                {
                    SnapToLedge(hitPoint, downHit.point.y, wallCollider);
                }
            }
        }

        private void SnapToLedge(Vector2 wallHitPoint, float trueTopY, Collider2D ledgeCollider)
        {
            isHanging = true;
            isScrambling = false; 
            currentLedge = ledgeCollider;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;

            float targetX = Mathf.Approximately(facingDirection, 1f) 
                ? wallHitPoint.x - (playerWidth / 2f) 
                : wallHitPoint.x + (playerWidth / 2f);
            
            float targetY = trueTopY - 2f;
            transform.position = new Vector2(targetX, targetY);
            
            DisplayAction("LEDGE GRAB!", Color.white);
            ghostAnimator.SetTrigger("Ledge");
        }

        private void HandleHangingState()
        {
            float inputY = moveAction.action.ReadValue<Vector2>().y;

            if (inputY > 0.5f) 
            {
                isHanging = false;
                if (currentLedge) StartCoroutine(ClimbRoutine(currentLedge));
                ghostAnimator.SetTrigger("Climb");
            }
            else if (inputY < -0.5f) 
            {
                isHanging = false;
                rb.bodyType = RigidbodyType2D.Dynamic;
                currentLedge = null;
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
                float currentDistance = waistHit.distance;

                // 1. Thin Objects (Like your 1x1 cube) -> STRICTLY VAULTS ONLY
                if (obstacleDepth <= 1.25f)
                {
                    VaultData selectedVault = GetHighestPriorityVault(isTricking ? trickVaults : speedVaults, currentDistance);
                    
                    // THE FIX: The Kong fallback has been completely deleted.
                    // If you aren't at the right distance for a Vault, it will NOT swap to a Kong.

                    if (selectedVault != null)
                    {
                        DisplayAction(isTricking ? "TRICK VAULT!" : "SPEED VAULT!", isTricking ? Color.cyan : Color.white);
                        StartCoroutine(VaultRoutine(waistHit.collider, exactHeightToClear, selectedVault));
                        return true;
                    }
                }
                // 2. Thick Objects -> STRICTLY KONGS ONLY
                else if (obstacleDepth <= 3f)
                {
                    VaultData selectedVault = GetHighestPriorityVault(isTricking ? trickKongs : speedKongs, currentDistance);
                    if (selectedVault != null)
                    {
                        DisplayAction(isTricking ? "TRICK KONG!" : "SPEED KONG!", isTricking ? Color.cyan : Color.white);
                        StartCoroutine(VaultRoutine(waistHit.collider, exactHeightToClear, selectedVault));
                        return true;
                    }
                }
            }

            return false; 
        }
        
        private VaultData GetHighestPriorityVault(VaultData[] vaultArray, float currentDistance = -1f)
        {
            if (vaultArray == null || vaultArray.Length == 0) return null;
            
            VaultData bestVault = null;
            
            for (int i = 0; i < vaultArray.Length; i++)
            {
                if (currentDistance >= 0f)
                {
                    if (currentDistance < vaultArray[i].minDistance || currentDistance > vaultArray[i].maxDistance) continue;
                }
                
                if (bestVault == null || vaultArray[i].priorityScore > bestVault.priorityScore)
                {
                    bestVault = vaultArray[i];
                }
            }
            return bestVault;
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

        private void InitiateWipeout()
        {
            // STOP the other coroutines safely without killing ourselves
            StopAllCoroutines();
            
            isVaulting = false;
            isClimbing = false;
            isScrambling = false;
            isVulnerable = false;
            isDoingATricking = false;
            
            StartCoroutine(RagdollWipeoutRoutine());
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (isRagdolling) return;

            // Vulnerable Smash Check: Slamming a wall while vulnerable triggers instant communication with the Ragdoll Manager.
            if (isVulnerable && ((1 << collision.gameObject.layer) & obstacleLayer) != 0)
            {
                ContactPoint2D contact = collision.GetContact(0);
                if (Mathf.Abs(contact.normal.x) > 0.5f) 
                {
                    InitiateWipeout();
                }
            }
        }
        #endregion

        #region ACTION STATES
        private void Springboard()
        {
            StopAllCoroutines(); 
            isVaulting = false;
            isDoingATricking = false;
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
            ApplyDownVisuals(crouchColor);
        }

        private void TransitionToCrouch()
        {
            isSliding = false;
            isCrouching = true;
            ApplyDownVisuals(crouchColor);
        }

        private void StandUp()
        {
            isSliding = false;
            isCrouching = false;
            capsuleCollider2D.offset = new Vector2(0, 1f);
            capsuleCollider2D.size = new Vector2(0.5f, 2f);
            sprites.transform.localPosition = Vector3.zero;
            sprites.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            if (cubeSprite) cubeSprite.color = normalColor;
        }

        private void HandleParkourHold()
        {
            if (isVaulting || !isGrounded || isVulnerable || isClimbing || isRagdolling) return; 
            if (parkourAction.action.IsPressed()) FireParkourAction(false);
        }

        
        #endregion

        #region COROUTINES
        private IEnumerator RagdollWipeoutRoutine()
        {
            DisplayAction("WIPEOUT!", Color.red);
            if (cubeSprite) cubeSprite.color = Color.red;

            rb.linearVelocity = new Vector2(-facingDirection * 5f, 5f);
            
            if (ragdollManager != null) 
            {
                ragdollManager.TriggerRagdollDrop();
            }
            else 
            {
                isRagdolling = true;
                ghostAnimator.enabled = false;
            }

            yield return new WaitForSeconds(0.5f);

            // THE FIX: Wait for the ragdoll's physical momentum to settle instead of relying on the foot raycast.
            Rigidbody2D pelvisRb = ragdollManager != null ? ragdollManager.activeRagdollRoot.GetComponent<Rigidbody2D>() : rb;
            
            while (isRagdolling && pelvisRb != null && pelvisRb.linearVelocity.magnitude > 0.5f)
            {
                yield return null;
            }

            // THE FIX: If the player manually pressed T to recover during the fall, abort the automated timer.
            if (!isRagdolling) yield break;

            yield return new WaitForSeconds(2f);

            // One last check in case they pressed T during the exact 2-second wait
            if (!isRagdolling) yield break;

            if (ragdollManager != null) 
            {
                ragdollManager.TriggerRecovery();
            }
            else 
            {
                isRagdolling = false;
                ghostAnimator.enabled = true; 
            }

            ghostAnimator.Play("Still Land"); 
            if (cubeSprite) cubeSprite.color = normalColor;
            DisplayAction("RECOVERED", Color.white);
        }

        private IEnumerator VulnerableRoutine()
        {
            isVulnerable = true;
            DisplayAction("MISSED TRICK!", Color.yellow);
            yield return new WaitForSeconds(1.5f);
            isVulnerable = false;
        }

        private IEnumerator WallScrambleRoutine()
        {
            isScrambling = true;
            
            float timePassed = 0f;
            
            while (timePassed < wallScrambleDuration && isCollidingFront)
            {
                if (isRagdolling) yield break; 
                
                timePassed += Time.deltaTime;
                rb.linearVelocity = new Vector2(0f, wallScrambleSpeed); 
                
                if (isHanging || isVaulting || isClimbing) 
                {
                    isScrambling = false;
                    yield break; 
                }
                
                yield return new WaitForFixedUpdate();
            }
            
            isScrambling = false;
        }
        
        private IEnumerator VaultRoutine(Collider2D obstacle, float extraHeight, VaultData vaultData)
        {
            isVaulting = true;
            float entryVelocityX = rb.linearVelocity.x;

            if (vaultData.isTrick)
            {
                //isVulnerable = true;
                isDoingATricking = true;
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f; 
                capsuleCollider2D.enabled = true; 
                capsuleCollider2D.size = new Vector2(0.5f, 1f);    
                capsuleCollider2D.offset = new Vector2(0f, 1.5f);  
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Kinematic; 
                capsuleCollider2D.enabled = false; 
            }

            float speedRatio = Mathf.InverseLerp(minTopSpeed, maxTopSpeed, Mathf.Abs(entryVelocityX));
            float vaultSpeedMultiplier = Mathf.Lerp(1f, 1.25f, speedRatio); 
            
            ghostAnimator.SetFloat("VaultSpeed", vaultSpeedMultiplier);
            ghostAnimator.SetTrigger(vaultData.animationStateName);
            
            float rawClipLength = GetClipLength(vaultData.animationStateName);
            float dynamicDuration = rawClipLength / vaultSpeedMultiplier; 

            Vector2 startPos = transform.position;
            
            float landX = Mathf.Approximately(facingDirection, 1) 
                ? obstacle.bounds.max.x + (playerWidth / 2f) + vaultData.clearancePadding 
                : obstacle.bounds.min.x - (playerWidth / 2f) - vaultData.clearancePadding;

            float heightToClear = (obstacle.bounds.max.y - startPos.y) + extraHeight;
            float timePassed = 0f;

            while (timePassed < dynamicDuration)
            {
                if (isRagdolling) yield break; 

                timePassed += Time.deltaTime;
                float t = timePassed / dynamicDuration;
                
                float curveY = vaultData.yCurve.Evaluate(t);
                float curveX = vaultData.xCurve.Evaluate(t);
                
                float currentX = Mathf.Lerp(startPos.x, landX, curveX);
                float currentY = startPos.y + (heightToClear * curveY);
                
                rb.MovePosition(new Vector2(currentX, currentY));
                yield return new WaitForFixedUpdate();
            }

            if (vaultData.isTrick)
            {
                isVulnerable = false;
                rb.gravityScale = defaultGravity;
                capsuleCollider2D.size = new Vector2(0.5f, 2f);
                capsuleCollider2D.offset = new Vector2(0f, 1f); 
            }

            capsuleCollider2D.enabled = true; 
            rb.bodyType = RigidbodyType2D.Dynamic; 
            
            rb.linearVelocity = new Vector2(entryVelocityX, rb.linearVelocity.y);
            
            isVaulting = false;
            isDoingATricking = false;
            if (cubeSprite) cubeSprite.color = normalColor;
        }

        private float GetClipLength(string clipName)
        {
            RuntimeAnimatorController ac = ghostAnimator.runtimeAnimatorController;
            foreach (AnimationClip clip in ac.animationClips)
            {
                if (clip.name == clipName) return clip.length;
            }
            Debug.LogWarning($"Clip '{clipName}' not found! Defaulting to 1.1s.");
            return 1.1f; 
        }
        
       private IEnumerator MantleRoutine(Collider2D obstacle)
        {
            isClimbing = true;
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
                if (isRagdolling) yield break; 
                
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
                if (isRagdolling) yield break; 

                timePassed += Time.deltaTime;
                float t = timePassed / pullDuration;
                rb.MovePosition(new Vector2(Mathf.Lerp(topPos.x, edgeX, t), topY));
                yield return new WaitForFixedUpdate();
            }

            capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic; 
            
            // THE FIX: Hard reset momentum before returning control to the player
            rb.linearVelocity = Vector2.zero; 
            
            isClimbing = false;
        }

        private IEnumerator ClimbRoutine(Collider2D obstacle)
        {
            isClimbing = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            capsuleCollider2D.enabled = false;

            Vector2 startPos = transform.position;
            float edgeX = Mathf.Approximately(facingDirection, 1) ? obstacle.bounds.min.x + (playerWidth / 2f) : obstacle.bounds.max.x - (playerWidth / 2f);
            float topY = obstacle.bounds.max.y; 

            float timePassed = 0f;

            while (timePassed < climbAnimationDuration)
            {
                if (isRagdolling) yield break; 

                timePassed += Time.deltaTime;
        
                float t = timePassed / climbAnimationDuration; 

                float curveY = climbYCurve.Evaluate(t);
                float curveX = climbXCurve.Evaluate(t);

                float currentY = Mathf.Lerp(startPos.y, topY, curveY);
                float currentX = Mathf.Lerp(startPos.x, edgeX, curveX);

                rb.MovePosition(new Vector2(currentX, currentY));
                yield return new WaitForFixedUpdate();
            }

            capsuleCollider2D.enabled = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
            
            // THE FIX: Hard reset momentum before returning control to the player
            rb.linearVelocity = Vector2.zero;
            
            isClimbing = false;
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

            ghostAnimator.SetFloat("Direction", animationDirection); 
            ghostAnimator.SetBool("HasInput", hasInput); 
            ghostAnimator.SetBool("IsSliding", isSliding);
            ghostAnimator.SetBool("IsCrouching", isCrouching);
            ghostAnimator.SetBool("IsGrounded", isGrounded);
            ghostAnimator.SetBool("IsFlushWall", isFlushWall);
            ghostAnimator.SetBool("isHanging", isHanging);

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