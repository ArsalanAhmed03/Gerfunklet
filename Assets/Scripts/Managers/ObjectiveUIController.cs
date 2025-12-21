using Unity.Netcode;
using UnityEngine;

public class ObjectiveUIController : MonoBehaviour
{
    private ObjectiveZone GetMyZone()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;

        if (NetworkManager.Singleton == null) return null;
        ulong myId = NetworkManager.Singleton.LocalClientId;

        if (gm.zoneA != null && gm.zoneA.OwnerClientId == myId) return gm.zoneA;
        if (gm.zoneB != null && gm.zoneB.OwnerClientId == myId) return gm.zoneB;

        return null;
    }

    private ObjectiveZone GetEnemyZone()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;

        if (NetworkManager.Singleton == null) return null;
        ulong myId = NetworkManager.Singleton.LocalClientId;

        if (gm.zoneA != null && gm.zoneA.OwnerClientId != myId) return gm.zoneA;
        if (gm.zoneB != null && gm.zoneB.OwnerClientId != myId) return gm.zoneB;

        return null;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (NetworkManager.Singleton == null) return;

        var myZone = GetMyZone();
        var enemyZone = GetEnemyZone();

        // If owner isn't synced yet (e.g., on first frames), don't show misleading UI.
        if (myZone == null || enemyZone == null)
        {
            if (gm.dangerCaptureBar != null) gm.dangerCaptureBar.gameObject.SetActive(false);
            if (gm.myCaptureBar != null) gm.myCaptureBar.gameObject.SetActive(false);
            if (gm.captureStateText != null) gm.captureStateText.gameObject.SetActive(false);

            UpdateMatchTimer(gm);
            return;
        }

        // Red = enemy capturing my zone
        float danger = myZone.progress01.Value;

        // Blue = I am capturing enemy zone
        float myCap = enemyZone.progress01.Value;

        // ---- Sliders ----
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

        // ---- State text ----
        if (gm.captureStateText != null)
        {
            bool race = danger > 0f && myCap > 0f;

            bool contested =
                (myZone != null && myZone.contested.Value) ||
                (enemyZone != null && enemyZone.contested.Value);

            if (race)
                gm.captureStateText.text = "RACE";
            else if (contested)
                gm.captureStateText.text = "CONTESTED";
            else
                gm.captureStateText.text = "";

            gm.captureStateText.gameObject.SetActive(race || contested);
        }

        UpdateMatchTimer(gm);
    }

    private void UpdateMatchTimer(GameManager gm)
    {
        if (gm.matchTimerText == null) return;
        if (MatchManager.Instance == null) return;

        if (MatchManager.Instance.Phase.Value == (int)MatchManager.MatchPhase.Overtime)
        {
            gm.matchTimerText.text = "OVERTIME";
            return;
        }

        float t = Mathf.Max(0f, MatchManager.Instance.MatchRemaining.Value);
        int seconds = Mathf.CeilToInt(t);
        int mins = seconds / 60;
        int secs = seconds % 60;

        gm.matchTimerText.text = $"{mins:00}:{secs:00}";
    }
}
