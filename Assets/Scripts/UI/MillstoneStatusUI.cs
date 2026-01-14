using Unity.Netcode;
using UnityEngine;

public class MillstoneStatusUI : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private GameObject carriedIcon;

    [Header("Arrow Ping (World)")]
    [SerializeField] private Transform arrowIndicator;
    [SerializeField] private Vector3 arrowOffset = new Vector3(0f, 0.05f, 0f);
    [SerializeField] private float arrowDistance = 0.8f;

    [Header("Altar Halo")]
    [SerializeField] private float altarRangeEpsilon = 0.05f;

    private MillstoneCarrier _localCarrier;
    private ulong _localClientId = ulong.MaxValue;
    private MillstoneAltar _enemyAltar;
    private Collider _enemyAltarCollider;
    private MillstoneAltarHalo _enemyAltarHalo;
    private MatchManager _match;

    private void OnDisable()
    {
        SetActive(carriedIcon, false);
        SetActive(arrowIndicator != null ? arrowIndicator.gameObject : null, false);
        if (_enemyAltarHalo != null) _enemyAltarHalo.SetActive(false);
    }

    private void Update()
    {
        BindIfNeeded();
        Refresh();
    }

    private void BindIfNeeded()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsClient) return;

        if (_localCarrier == null || _localClientId != nm.LocalClientId)
        {
            var playerObj = nm.SpawnManager.GetLocalPlayerObject();
            if (playerObj == null) return;

            _localClientId = nm.LocalClientId;
            _localCarrier = playerObj.GetComponent<MillstoneCarrier>();
        }

        if (_match == null && MatchManager.Instance != null)
            _match = MatchManager.Instance;

        if (_enemyAltar == null || _enemyAltar.ownerClientId.Value == _localClientId || _enemyAltar.ownerClientId.Value == ulong.MaxValue)
            TryBindEnemyAltar();
    }

    private void TryBindEnemyAltar()
    {
        if (_localClientId == ulong.MaxValue) return;

        var altars = FindObjectsOfType<MillstoneAltar>(true);
        foreach (var altar in altars)
        {
            if (altar == null) continue;
            ulong owner = altar.ownerClientId.Value;
            if (owner == ulong.MaxValue) continue;
            if (owner == _localClientId) continue;

            _enemyAltar = altar;
            _enemyAltarCollider = altar.GetComponent<Collider>();
            _enemyAltarHalo = altar.GetComponent<MillstoneAltarHalo>();
            return;
        }
    }

    private void Refresh()
    {
        bool phaseAllows = true;
        if (_match != null)
        {
            var phase = (MatchManager.MatchPhase)_match.Phase.Value;
            phaseAllows = phase == MatchManager.MatchPhase.Playing || phase == MatchManager.MatchPhase.Overtime;
        }

        bool carrying = phaseAllows && _localCarrier != null && _localCarrier.IsCarrying.Value;
        SetActive(carriedIcon, carrying);

        bool showArrow = carrying && _enemyAltar != null && arrowIndicator != null;
        SetActive(arrowIndicator != null ? arrowIndicator.gameObject : null, showArrow);

        if (showArrow)
        {
            Vector3 origin = _localCarrier.transform.position;
            Vector3 target = _enemyAltar.transform.position;
            Vector3 dir = target - origin;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = _localCarrier.transform.forward;

            Vector3 arrowPos = origin + dir.normalized * arrowDistance + arrowOffset;
            arrowIndicator.position = arrowPos;
            arrowIndicator.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        bool inRange = false;
        if (carrying && _enemyAltarCollider != null)
        {
            Vector3 pos = _localCarrier.transform.position;
            Vector3 closest = _enemyAltarCollider.ClosestPoint(pos);
            float sqr = (pos - closest).sqrMagnitude;
            inRange = sqr <= altarRangeEpsilon * altarRangeEpsilon;
        }

        if (_enemyAltarHalo != null)
            _enemyAltarHalo.SetActive(carrying && inRange);
    }

    private void SetActive(GameObject obj, bool active)
    {
        if (obj == null) return;
        if (obj.activeSelf == active) return;
        obj.SetActive(active);
    }
}
