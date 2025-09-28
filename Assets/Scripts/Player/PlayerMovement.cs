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
        }
    }

    private void OnEnable()
    {
        moveAction?.Enable();
        lookAction?.Enable();
        attackAction?.Enable();
        jumpAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        lookAction?.Disable();
        attackAction?.Disable();
        jumpAction?.Disable();
    }

    private void Update()
    {
        if (!IsOwner) return;
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
            transform.Translate(move.normalized * moveSpeed * Time.deltaTime, Space.World);

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

                Debug.Log("Requesting minion spawn from server...");
                LocalSpawner.Instance.SpawnMinionForClientServerRpc(OwnerClientId);
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
    }
}
