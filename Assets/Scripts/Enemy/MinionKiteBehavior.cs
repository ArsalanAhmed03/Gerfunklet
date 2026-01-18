using UnityEngine;

public class MinionKiteBehavior : MonoBehaviour
{
    [SerializeField] private float preferredRange = 4f;
    [SerializeField] private float kiteDistance = 1f;
    [SerializeField] private float stopDistance = 0.2f;

    public float StopDistance => stopDistance;

    public bool TryGetKiteDestination(Transform self, Transform target, out Vector3 destination)
    {
        destination = Vector3.zero;
        if (self == null || target == null) return false;

        float dist = Vector3.Distance(self.position, target.position);
        if (dist >= preferredRange)
            return false;

        Vector3 away = (self.position - target.position);
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = Vector3.forward;

        destination = self.position + away.normalized * kiteDistance;
        return true;
    }
}
