using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class ObjectiveUIController : MonoBehaviour
{
    private CitadelHealth _citadelA;
    private CitadelHealth _citadelB;
    private ThroneCapture _throneA;
    private ThroneCapture _throneB;
    private MatchManager _match;
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

        UnbindMatch();
        UnbindCitadelThrone();
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
        BindCitadelThrone(gm.citadelA, gm.citadelB, gm.throneA, gm.throneB);

        RefreshCitadelThroneUI();
        UpdateMatchTimer();
        return _matchBound;
    }

    private void BindCitadelThrone(CitadelHealth citadelA, CitadelHealth citadelB, ThroneCapture throneA, ThroneCapture throneB)
    {
        UnbindCitadelThrone();

        _citadelA = citadelA;
        _citadelB = citadelB;
        _throneA = throneA;
        _throneB = throneB;

        if (_citadelA != null) SubscribeCitadel(_citadelA);
        if (_citadelB != null) SubscribeCitadel(_citadelB);
        if (_throneA != null) SubscribeThrone(_throneA);
        if (_throneB != null) SubscribeThrone(_throneB);
    }

    private void UnbindCitadelThrone()
    {
        if (_citadelA != null) UnsubscribeCitadel(_citadelA);
        if (_citadelB != null) UnsubscribeCitadel(_citadelB);
        if (_throneA != null) UnsubscribeThrone(_throneA);
        if (_throneB != null) UnsubscribeThrone(_throneB);

        _citadelA = null;
        _citadelB = null;
        _throneA = null;
        _throneB = null;
    }

    private void SubscribeCitadel(CitadelHealth citadel)
    {
        citadel.health.OnValueChanged += HandleCitadelChanged;
        citadel.destroyed.OnValueChanged += HandleCitadelDestroyed;
        citadel.ownerClientId.OnValueChanged += HandleCitadelOwnerChanged;
    }

    private void UnsubscribeCitadel(CitadelHealth citadel)
    {
        citadel.health.OnValueChanged -= HandleCitadelChanged;
        citadel.destroyed.OnValueChanged -= HandleCitadelDestroyed;
        citadel.ownerClientId.OnValueChanged -= HandleCitadelOwnerChanged;
    }

    private void SubscribeThrone(ThroneCapture throne)
    {
        throne.progress01.OnValueChanged += HandleThroneProgressChanged;
        throne.ownerClientId.OnValueChanged += HandleThroneOwnerChanged;
    }

    private void UnsubscribeThrone(ThroneCapture throne)
    {
        throne.progress01.OnValueChanged -= HandleThroneProgressChanged;
        throne.ownerClientId.OnValueChanged -= HandleThroneOwnerChanged;
    }

    private void HandleCitadelChanged(int oldValue, int newValue)
    {
        RefreshCitadelThroneUI();
    }

    private void HandleCitadelDestroyed(bool oldValue, bool newValue)
    {
        RefreshCitadelThroneUI();
    }

    private void HandleCitadelOwnerChanged(ulong oldValue, ulong newValue)
    {
        RefreshCitadelThroneUI();
    }

    private void HandleThroneProgressChanged(float oldValue, float newValue)
    {
        RefreshCitadelThroneUI();
    }

    private void HandleThroneOwnerChanged(ulong oldValue, ulong newValue)
    {
        RefreshCitadelThroneUI();
    }

    private void BindMatch()
    {
        if (_matchBound) return;
        if (MatchManager.Instance == null) return;

        _match = MatchManager.Instance;
        _match.OnPhaseChanged += HandlePhaseChanged;
        _match.MatchRemaining.OnValueChanged += HandleMatchRemainingChanged;
        _match.OvertimeRemaining.OnValueChanged += HandleOvertimeRemainingChanged;
        _match.LoadoutEndsAtServerTime.OnValueChanged += HandleLoadoutEndChanged;
        _matchBound = true;
    }

    private void UnbindMatch()
    {
        if (!_matchBound || _match == null) return;

        _match.OnPhaseChanged -= HandlePhaseChanged;
        _match.MatchRemaining.OnValueChanged -= HandleMatchRemainingChanged;
        _match.OvertimeRemaining.OnValueChanged -= HandleOvertimeRemainingChanged;
        _match.LoadoutEndsAtServerTime.OnValueChanged -= HandleLoadoutEndChanged;
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

    private void HandleOvertimeRemainingChanged(float oldValue, float newValue)
    {
        UpdateMatchTimer();
    }

    private void HandleLoadoutEndChanged(double oldValue, double newValue)
    {
        UpdateMatchTimer();
    }

    private void Update()
    {
        if (_match == null) return;

        var phase = (MatchManager.MatchPhase)_match.Phase.Value;
        if (phase == MatchManager.MatchPhase.LoadoutSelect)
            UpdateMatchTimer();
    }

    private void RefreshCitadelThroneUI()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (NetworkManager.Singleton == null) return;

        ulong myId = NetworkManager.Singleton.LocalClientId;

        CitadelHealth enemyCitadel = null;
        if (_citadelA != null && _citadelA.ownerClientId.Value != myId) enemyCitadel = _citadelA;
        if (_citadelB != null && _citadelB.ownerClientId.Value != myId) enemyCitadel = _citadelB;

        if (enemyCitadel != null && enemyCitadel.ownerClientId.Value != ulong.MaxValue)
        {
            float max = Mathf.Max(1, enemyCitadel.MaxHealth);
            float value = enemyCitadel.destroyed.Value ? 0 : enemyCitadel.health.Value;

            if (gm.enemyCitadelBar != null)
            {
                gm.enemyCitadelBar.maxValue = max;
                gm.enemyCitadelBar.value = value;
                gm.enemyCitadelBar.gameObject.SetActive(true);
            }

            if (gm.enemyCitadelText != null)
            {
                gm.enemyCitadelText.text = enemyCitadel.destroyed.Value
                    ? "ENEMY CITADEL DESTROYED"
                    : $"ENEMY CITADEL {value:0}/{max:0}";
                gm.enemyCitadelText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (gm.enemyCitadelBar != null) gm.enemyCitadelBar.gameObject.SetActive(false);
            if (gm.enemyCitadelText != null) gm.enemyCitadelText.gameObject.SetActive(false);
        }

        ThroneCapture activeThrone = null;
        if (_throneA != null && _throneA.progress01.Value > 0f) activeThrone = _throneA;
        if (_throneB != null && _throneB.progress01.Value > 0f) activeThrone = _throneB;

        if (activeThrone != null)
        {
            float value = activeThrone.progress01.Value;
            if (gm.throneCaptureBar != null)
            {
                gm.throneCaptureBar.value = value;
                gm.throneCaptureBar.gameObject.SetActive(true);
            }

            if (gm.throneCaptureText != null)
            {
                bool enemyThrone = activeThrone.ownerClientId.Value != myId;
                gm.throneCaptureText.text = enemyThrone ? "CAPTURING ENEMY THRONE" : "ENEMY CAPTURING";
                gm.throneCaptureText.gameObject.SetActive(true);
            }
        }
        else
        {
            if (gm.throneCaptureBar != null) gm.throneCaptureBar.gameObject.SetActive(false);
            if (gm.throneCaptureText != null) gm.throneCaptureText.gameObject.SetActive(false);
        }
    }

    private void UpdateMatchTimer()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.matchTimerText == null) return;
        if (_match == null) return;

        if (_match.Phase.Value == (int)MatchManager.MatchPhase.LoadoutSelect)
        {
            double endTime = _match.LoadoutEndsAtServerTime.Value;
            double now = NetworkManager.Singleton != null
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.timeAsDouble;

            float remaining = endTime > 0d ? (float)(endTime - now) : _match.LoadoutSelectSeconds;
            remaining = Mathf.Max(0f, remaining);

            int seconds = Mathf.CeilToInt(remaining);
            int mins = seconds / 60;
            int secs = seconds % 60;
            gm.matchTimerText.text = $"READY {mins:00}:{secs:00}";
            return;
        }

        if (_match.Phase.Value == (int)MatchManager.MatchPhase.Overtime)
        {
            float t = Mathf.Max(0f, _match.OvertimeRemaining.Value);
            int seconds = Mathf.CeilToInt(t);
            int mins = seconds / 60;
            int secs = seconds % 60;
            gm.matchTimerText.text = $"OT {mins:00}:{secs:00}";
            return;
        }
        else
        {

            float t = Mathf.Max(0f, _match.MatchRemaining.Value);
            int seconds = Mathf.CeilToInt(t);
            int mins = seconds / 60;
            int secs = seconds % 60;
            gm.matchTimerText.text = $"{mins:00}:{secs:00}";
        }
    }
}
