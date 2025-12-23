using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    [Header("Input Action Asset")]
    public InputActionAsset playerInputActions; // Assign in Inspector

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction attackAction;
    private InputAction jumpAction;

    private InputAction ability1Action;
    private InputAction ability2Action;
    private InputAction ability3Action;
    private InputAction ability4Action;

    [Header("Animation")]
    public PlayerAnimator playerAnimator;

    private void Awake()
    {
        if (playerInputActions != null)
        {
            var playerMap = playerInputActions.FindActionMap("Player", true);
            moveAction = playerMap.FindAction("Move");
            lookAction = playerMap.FindAction("Look");
            attackAction = playerMap.FindAction("Attack");
            jumpAction = playerMap.FindAction("Jump");
            ability1Action = playerMap.FindAction("Ability1");
            ability2Action = playerMap.FindAction("Ability2");
            ability3Action = playerMap.FindAction("Ability3");
            ability4Action = playerMap.FindAction("Ability4");

        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        attackAction?.Enable();
        jumpAction?.Enable();
        ability1Action?.Enable();
        ability2Action?.Enable();
        ability3Action?.Enable();
        ability4Action?.Enable();

    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        attackAction?.Disable();
        jumpAction?.Disable();
        ability1Action?.Disable();
        ability2Action?.Disable();
        ability3Action?.Disable();
        ability4Action?.Disable();

    }

    private void Update()
    {
        if (!IsOwner) return;
        if (GameManager.Instance != null && !GameManager.Instance.GameplayEnabled) return;
        var stun = GetComponent<StunReceiver>();
        if (stun != null && stun.IsStunned)
        {
            playerAnimator?.SetMoving(false);
            return;
        }

        if (moveAction == null) return;
        if (CameraController.Instance != null && !CameraController.Instance.IsFollowingTarget())
        {
            CameraController.Instance.SetTarget(transform);
        }

        // Get joystick / WASD input
        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = new Vector3(input.x, 0f, input.y);

        if (move.sqrMagnitude > 0.01f)
        {
            // Move character
            // transform.Translate(move.normalized * moveSpeed * Time.deltaTime, Space.World);
            var buff = GetComponent<BuffReceiver>();
            float speedMul = buff != null ? buff.MoveSpeedMultiplier : 1f;
            transform.Translate(move.normalized * moveSpeed * speedMul * Time.deltaTime, Space.World);

            // Rotate to face direction
            transform.forward = move;

            // Trigger walk animation
            playerAnimator?.SetMoving(true);
        }
        else
        {
            // Idle
            playerAnimator?.SetMoving(false);
        }

        var statsManager = GetComponent<PlayerStatsManager>();
        // Attack logic
        if (attackAction != null && statsManager != null && statsManager.getStamina() >= 10)
        {
            if (attackAction.WasPressedThisFrame())
            {
                Debug.Log("Attack!");
                // Trigger attack animation
                // playerAnimator?.Attack();
                if (statsManager != null)
                {
                    statsManager.modifyStamina(-10);
                }

                if (LocalSpawner.Instance != null)
                {
                    Debug.Log("Requesting minion spawn from server...");
                    LocalSpawner.Instance.SpawnMinionForClientServerRpc(OwnerClientId);
                }
            }
        }

        // Jump logic
        if (jumpAction != null)
        {
            if (jumpAction.WasPressedThisFrame())
            {
                playerAnimator?.Jump();
            }
        }

        var abilityRunner = GetComponent<AbilityRunner>();
        if (abilityRunner != null)
        {
            if (ability1Action != null && ability1Action.WasPressedThisFrame())
            {
                Debug.Log("Casting ability slot 0");
                abilityRunner.TryCastSlot(0);
            }

            if (ability2Action != null && ability2Action.WasPressedThisFrame())
            {
                Debug.Log("Casting ability slot 1");
                abilityRunner.TryCastSlot(1);
            }

            if (ability3Action != null && ability3Action.WasPressedThisFrame())
            {
                Debug.Log("Casting ability slot 2");
                abilityRunner.TryCastSlot(2);
            }
            if (ability4Action != null && ability4Action.WasPressedThisFrame())
            {
                Debug.Log("Casting ability slot 3");
                abilityRunner.TryCastSlot(3);
            }
        }

    }
}
