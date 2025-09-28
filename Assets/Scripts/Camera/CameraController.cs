using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    public CinemachineCamera cinemachineCamera;

    public void SetTarget(Transform newTarget)
    {
        Debug.Log("Setting camera target to: " + newTarget.name);
        cinemachineCamera.Target.TrackingTarget = newTarget;
    }

    public bool IsFollowingTarget()
    {
        return cinemachineCamera.Target.TrackingTarget != null;
    }
}

