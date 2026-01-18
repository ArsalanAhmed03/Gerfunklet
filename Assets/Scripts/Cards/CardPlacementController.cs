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

    [Header("Ghost Preview (optional)")]
    [SerializeField] private bool showGhostPreview = true;
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Vector3 ghostOffset = Vector3.zero;
    [SerializeField] private float ghostScale = 1f;
    [SerializeField] private Color ghostValidColor = new Color(0.2f, 1f, 0.2f, 0.55f);
    [SerializeField] private Color ghostInvalidColor = new Color(1f, 0.2f, 0.2f, 0.55f);

    private CardHand _hand;
    private DeploymentRules _rules;
    private int _handIndex = -1;
    private bool _active;
    private GameObject _ghostInstance;
    private Renderer[] _ghostRenderers;
    private MaterialPropertyBlock _ghostProps;

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
        bool valid = hasPoint && _rules != null && _rules.IsPlacementValid(point, out _);
        UpdateIndicator(hasPoint, point, valid);
        UpdateGhost(hasPoint, point, valid);

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
        CreateGhost(def);
        _rules = hand.GetComponent<DeploymentRules>();
        _active = true;
        UpdateIndicator(false, Vector3.zero, false);
        UpdateGhost(false, Vector3.zero, false);
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
        DestroyGhost();
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

    private void UpdateIndicator(bool hasPoint, Vector3 point, bool valid)
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

    private void CreateGhost(CardDefinition def)
    {
        DestroyGhost();
        if (!showGhostPreview) return;
        if (def == null || def.spawnPrefab == null) return;

        _ghostInstance = Instantiate(def.spawnPrefab);
        _ghostInstance.name = $"{def.spawnPrefab.name}_Ghost";
        _ghostInstance.transform.localScale *= ghostScale;
        _ghostInstance.SetActive(false);

        DisableGhostComponents();
        CacheGhostRenderers();
    }

    private void DisableGhostComponents()
    {
        if (_ghostInstance == null) return;

        var behaviours = _ghostInstance.GetComponentsInChildren<Behaviour>(true);
        foreach (var behaviour in behaviours)
        {
            if (behaviour == null) continue;
            if (behaviour is Renderer) continue;
            behaviour.enabled = false;
        }

        var colliders = _ghostInstance.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
            col.enabled = false;

        var rigidbodies = _ghostInstance.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }
    }

    private void CacheGhostRenderers()
    {
        if (_ghostInstance == null) return;
        _ghostRenderers = _ghostInstance.GetComponentsInChildren<Renderer>(true);
        _ghostProps = new MaterialPropertyBlock();

        if (ghostMaterial != null)
        {
            foreach (var renderer in _ghostRenderers)
            {
                if (renderer == null) continue;
                renderer.sharedMaterial = ghostMaterial;
            }
        }
    }

    private void UpdateGhost(bool hasPoint, Vector3 point, bool valid)
    {
        if (_ghostInstance == null) return;

        if (!_active || !hasPoint)
        {
            _ghostInstance.SetActive(false);
            return;
        }

        _ghostInstance.SetActive(true);
        _ghostInstance.transform.position = point + ghostOffset;

        if (_ghostRenderers == null || _ghostRenderers.Length == 0) return;

        Color color = valid ? ghostValidColor : ghostInvalidColor;
        _ghostProps.SetColor("_Color", color);
        _ghostProps.SetColor("_BaseColor", color);

        foreach (var renderer in _ghostRenderers)
        {
            if (renderer == null) continue;
            renderer.SetPropertyBlock(_ghostProps);
        }
    }

    private void DestroyGhost()
    {
        if (_ghostInstance == null) return;
        Destroy(_ghostInstance);
        _ghostInstance = null;
        _ghostRenderers = null;
        _ghostProps = null;
    }

}
