using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class ObjectiveUIController : MonoBehaviour
{
    private ObjectiveZone _zoneA;
    private ObjectiveZone _zoneB;
    private ObjectiveZone _myZone;
    private ObjectiveZone _enemyZone;
    private MatchManager _match;
    private bool _zonesBound;
    private bool _matchBound;
    private Coroutine _bindRoutine;

    private void OnEnable()
    {
        _bindRoutine = StartCoroutine(BindWhenReady());
    }

    private void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        UnbindZones();
        UnbindMatch();
    }

    private IEnumerator BindWhenReady()
    {
        while (!TryBind())
            yield return new WaitForSeconds(0.25f);
    }

    private bool TryBind()
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        if (NetworkManager.Singleton == null) return false;

        BindMatch();
        BindZones(gm.zoneA, gm.zoneB);

        RefreshZoneUI();
        UpdateMatchTimer();
        return _zonesBound && _matchBound;
    }

    private void BindZones(ObjectiveZone zoneA, ObjectiveZone zoneB)
    {
        if (_zonesBound && _zoneA == zoneA && _zoneB == zoneB) return;

        UnbindZones();

        _zoneA = zoneA;
        _zoneB = zoneB;

        if (_zoneA != null) SubscribeZone(_zoneA);
        if (_zoneB != null) SubscribeZone(_zoneB);

        _zonesBound = _zoneA != null && _zoneB != null;
    }

    private void UnbindZones()
    {
        if (_zoneA != null) UnsubscribeZone(_zoneA);
        if (_zoneB != null) UnsubscribeZone(_zoneB);

        _zoneA = null;
        _zoneB = null;
        _myZone = null;
        _enemyZone = null;
        _zonesBound = false;
    }

    private void SubscribeZone(ObjectiveZone zone)
    {
        zone.ownerClientId.OnValueChanged += HandleOwnerChanged;
        zone.progress01.OnValueChanged += HandleProgressChanged;
        zone.contested.OnValueChanged += HandleContestedChanged;
    }

    private void UnsubscribeZone(ObjectiveZone zone)
    {
        zone.ownerClientId.OnValueChanged -= HandleOwnerChanged;
        zone.progress01.OnValueChanged -= HandleProgressChanged;
        zone.contested.OnValueChanged -= HandleContestedChanged;
    }

    private void HandleOwnerChanged(ulong oldValue, ulong newValue)
    {
        RefreshZoneUI();
    }

    private void HandleProgressChanged(float oldValue, float newValue)
    {
        RefreshZoneUI();
    }

    private void HandleContestedChanged(bool oldValue, bool newValue)
    {
        RefreshZoneUI();
    }

    private void BindMatch()
    {
        if (_matchBound) return;
        if (MatchManager.Instance == null) return;

        _match = MatchManager.Instance;
        _match.OnPhaseChanged += HandlePhaseChanged;
        _match.MatchRemaining.OnValueChanged += HandleMatchRemainingChanged;
        _matchBound = true;
    }

    private void UnbindMatch()
    {
        if (!_matchBound || _match == null) return;

        _match.OnPhaseChanged -= HandlePhaseChanged;
        _match.MatchRemaining.OnValueChanged -= HandleMatchRemainingChanged;
        _match = null;
        _matchBound = false;
    }

    private void HandlePhaseChanged(MatchManager.MatchPhase phase)
    {
        UpdateMatchTimer();
    }

    private void HandleMatchRemainingChanged(float oldValue, float newValue)
    {
        UpdateMatchTimer();
    }

    private void RefreshZoneUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (NetworkManager.Singleton == null) return;

        ulong myId = NetworkManager.Singleton.LocalClientId;
        _myZone = null;
        _enemyZone = null;

        if (_zoneA != null && _zoneA.OwnerClientId != ulong.MaxValue)
        {
            if (_zoneA.OwnerClientId == myId) _myZone = _zoneA;
            else _enemyZone = _zoneA;
        }

        if (_zoneB != null && _zoneB.OwnerClientId != ulong.MaxValue)
        {
            if (_zoneB.OwnerClientId == myId) _myZone = _zoneB;
            else _enemyZone = _zoneB;
        }

        if (_myZone == null || _enemyZone == null)
        {
            if (gm.dangerCaptureBar != null) gm.dangerCaptureBar.gameObject.SetActive(false);
            if (gm.myCaptureBar != null) gm.myCaptureBar.gameObject.SetActive(false);
            if (gm.captureStateText != null) gm.captureStateText.gameObject.SetActive(false);
            return;
        }

        float danger = _myZone.progress01.Value;
        float myCap = _enemyZone.progress01.Value;

        if (gm.dangerCaptureBar != null)
        {
            gm.dangerCaptureBar.value = danger;
            gm.dangerCaptureBar.gameObject.SetActive(danger > 0f);
        }

        if (gm.myCaptureBar != null)
        {
            gm.myCaptureBar.value = myCap;
            gm.myCaptureBar.gameObject.SetActive(myCap > 0f);
        }

        if (gm.captureStateText != null)
        {
            bool race = danger > 0f && myCap > 0f;
            bool contested = _myZone.contested.Value || _enemyZone.contested.Value;

            if (race)
                gm.captureStateText.text = "RACE";
            else if (contested)
                gm.captureStateText.text = "CONTESTED";
            else
                gm.captureStateText.text = "";

            gm.captureStateText.gameObject.SetActive(race || contested);
        }
    }

    private void UpdateMatchTimer()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.matchTimerText == null) return;
        if (_match == null) return;

        if (_match.Phase.Value == (int)MatchManager.MatchPhase.Overtime)
        {
            gm.matchTimerText.text = "OVERTIME";
            return;
        }

        float t = Mathf.Max(0f, _match.MatchRemaining.Value);
        int seconds = Mathf.CeilToInt(t);
        int mins = seconds / 60;
        int secs = seconds % 60;

        gm.matchTimerText.text = $"{mins:00}:{secs:00}";
    }
}
