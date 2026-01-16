using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CardPlacementController : MonoBehaviour
{
    [Header("Raycast")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField] private float maxRayDistance = 200f;

    [Header("Input (optional)")]
    [SerializeField] private InputActionReference placeAction;
    [SerializeField] private InputActionReference cancelAction;

    [Header("Indicator (optional)")]
    [SerializeField] private GameObject placementIndicator;
    [SerializeField] private Renderer placementIndicatorRenderer;
    [SerializeField] private Vector3 indicatorOffset = new Vector3(0f, 0.02f, 0f);
    [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.2f, 0.75f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.75f);

    private CardHand _hand;
    private DeploymentRules _rules;
    private int _handIndex = -1;
    private bool _active;

    public bool IsActive => _active;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        CacheIndicatorRenderer();
    }

    private void OnEnable()
    {
        placeAction?.action.Enable();
        cancelAction?.action.Enable();
    }

    private void OnDisable()
    {
        placeAction?.action.Disable();
        cancelAction?.action.Disable();
        EndPlacement();
    }

    private void Update()
    {
        if (!_active) return;

        if (worldCamera == null)
            worldCamera = Camera.main;

        bool hasPoint = TryGetPlacementPoint(out var point);
        UpdateIndicator(hasPoint, point);

        if (IsCancelPressed())
        {
            EndPlacement();
            return;
        }

        if (!IsPlacePressed())
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!hasPoint)
            return;

        if (_rules != null && !_rules.IsPlacementValid(point, out var reason))
        {
            if (!string.IsNullOrEmpty(reason))
                Debug.Log($"[CardPlacement] Invalid placement: {reason}");
            return;
        }

        _hand.PlayCardAtServerRpc(_handIndex, point);
        EndPlacement();
    }

    public void BeginPlacement(CardHand hand, int handIndex, CardDefinition def)
    {
        if (hand == null) return;

        _hand = hand;
        _handIndex = handIndex;
        _ = def;
        _rules = hand.GetComponent<DeploymentRules>();
        _active = true;
        UpdateIndicator(false, Vector3.zero);
    }

    public void CancelPlacement()
    {
        EndPlacement();
    }

    private void EndPlacement()
    {
        _active = false;
        _hand = null;
        _rules = null;
        _handIndex = -1;
        SetIndicatorActive(false);
    }

    private bool TryGetPlacementPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (worldCamera == null) return false;

        Vector2 screenPos = GetPointerScreenPosition();
        Ray ray = worldCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, maxRayDistance, placementMask, QueryTriggerInteraction.Ignore))
            return false;

        point = hit.point;
        return true;
    }

    private Vector2 GetPointerScreenPosition()
    {
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            return Touchscreen.current.primaryTouch.position.ReadValue();

        if (Mouse.current != null)
            return Mouse.current.position.ReadValue();

        return Vector2.zero;
    }

    private bool IsPlacePressed()
    {
        if (placeAction != null && placeAction.action.WasPressedThisFrame())
            return true;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            return true;

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            return true;

        return false;
    }

    private bool IsCancelPressed()
    {
        if (cancelAction != null && cancelAction.action.WasPressedThisFrame())
            return true;

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            return true;

        return false;
    }

    private void UpdateIndicator(bool hasPoint, Vector3 point)
    {
        if (!_active) return;
        if (placementIndicator == null) return;

        if (!hasPoint)
        {
            SetIndicatorActive(false);
            return;
        }

        SetIndicatorActive(true);
        placementIndicator.transform.position = point + indicatorOffset;

        bool valid = _rules != null && _rules.IsPlacementValid(point, out _);
        if (placementIndicatorRenderer != null)
            placementIndicatorRenderer.material.color = valid ? validColor : invalidColor;
    }

    private void SetIndicatorActive(bool active)
    {
        if (placementIndicator == null) return;
        if (placementIndicator.activeSelf == active) return;
        placementIndicator.SetActive(active);
    }

    private void CacheIndicatorRenderer()
    {
        if (placementIndicatorRenderer != null) return;
        if (placementIndicator == null) return;
        placementIndicatorRenderer = placementIndicator.GetComponentInChildren<Renderer>(true);
    }

}
