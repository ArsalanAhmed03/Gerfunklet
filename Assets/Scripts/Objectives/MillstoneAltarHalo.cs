using UnityEngine;

public class MillstoneAltarHalo : MonoBehaviour
{
    [SerializeField] private GameObject haloVisual;

    public void SetActive(bool active)
    {
        if (haloVisual == null) return;
        if (haloVisual.activeSelf == active) return;
        haloVisual.SetActive(active);
    }
}
