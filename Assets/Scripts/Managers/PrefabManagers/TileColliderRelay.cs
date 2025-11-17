using UnityEngine;

public class TileColliderRelay : MonoBehaviour
{
    private TileBehaviour tile;

    private void Awake()
    {
        tile = GetComponentInParent<TileBehaviour>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("TileColliderRelay: OnTriggerEnter called");
        if (tile != null)
            tile.HandleTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("TileColliderRelay: OnTriggerExit called");
        if (tile != null)
            tile.HandleTriggerExit(other);
    }
}
