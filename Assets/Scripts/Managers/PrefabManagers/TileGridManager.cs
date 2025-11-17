using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class TileGridManager : NetworkBehaviour
{
    public static TileGridManager Instance { get; private set; }

    [Header("Existing Tiles")]
    [SerializeField] private Transform tilesRoot;   // assign in Inspector (parent containing all tiles)
    [SerializeField] private float tileSize = 2f;   // optional, informational

    [Header("Shrinking arena")]
    [SerializeField] private float collapseInterval = 10f;       // seconds between edge collapses
    [SerializeField] private float edgeToleranceFactor = 0.5f;   // how close to min/max to count as edge

    private readonly List<TileBehaviour> allTiles = new List<TileBehaviour>();
    private float collapseTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (tilesRoot == null)
        {
            Debug.LogError("TileGridManager: tilesRoot not assigned!");
            return;
        }

        RegisterExistingTiles();
    }

    private void RegisterExistingTiles()
    {
        allTiles.Clear();

        foreach (Transform child in tilesRoot)
        {
            TileBehaviour tile = child.GetComponent<TileBehaviour>();
            if (tile == null)
            {
                Debug.LogWarning($"TileGridManager: Child {child.name} missing TileBehaviour, skipping.");
                continue;
            }

            allTiles.Add(tile);
        }

        Debug.Log($"TileGridManager registered {allTiles.Count} tiles under {tilesRoot.name}");

        if (IsServer)
        {
            foreach (var tile in allTiles)
            {
                var no = tile.GetComponent<NetworkObject>();
                if (no != null && !no.IsSpawned)
                {
                    no.Spawn(true);
                }
            }
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        if (allTiles.Count == 0)
            return;

        collapseTimer += Time.deltaTime;
        if (collapseTimer >= collapseInterval)
        {
            collapseTimer = 0f;
            CollapseOuterRing();
        }
    }

    private void CollapseOuterRing()
    {
        List<TileBehaviour> aliveTiles = new List<TileBehaviour>();
        foreach (var t in allTiles)
        {
            if (t != null && t.IsAlive && !t.IsFalling)
                aliveTiles.Add(t);
        }

        if (aliveTiles.Count == 0)
            return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var t in aliveTiles)
        {
            Vector3 p = t.transform.position;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.z < minZ) minZ = p.z;
            if (p.z > maxZ) maxZ = p.z;
        }

        float tol = tileSize * edgeToleranceFactor;

        foreach (var t in aliveTiles)
        {
            Vector3 p = t.transform.position;

            bool onLeft   = Mathf.Abs(p.x - minX) <= tol;
            bool onRight  = Mathf.Abs(p.x - maxX) <= tol;
            bool onBottom = Mathf.Abs(p.z - minZ) <= tol;
            bool onTop    = Mathf.Abs(p.z - maxZ) <= tol;

            if (onLeft || onRight || onBottom || onTop)
            {
                t.ForceFall();
            }
        }
    }

    public TileBehaviour GetRandomSafeTile(TileBehaviour exclude = null)
    {
        List<TileBehaviour> candidates = new List<TileBehaviour>();

        foreach (var t in allTiles)
        {
            if (t == null)
                continue;
            if (!t.IsAlive)
                continue;
            if (t.IsFalling)
                continue;
            if (exclude != null && t == exclude)
                continue;

            candidates.Add(t);
        }

        if (candidates.Count == 0)
            return null;

        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
    }

    // hard-remove a player from every tile's occupants set
    public void ClearPlayerFromAllTiles(ulong playerId)
    {
        foreach (var t in allTiles)
        {
            if (t == null) continue;
            t.RemoveOccupant(playerId);
        }
    }

    public TileBehaviour GetTileAt(int index)
    {
        if (index < 0 || index >= allTiles.Count) return null;
        return allTiles[index];
    }

    public IEnumerable<TileBehaviour> GetAllTiles() => allTiles;

    public TileBehaviour GetNearestTile(Vector3 position)
    {
        TileBehaviour nearest = null;
        float bestDist = float.MaxValue;

        foreach (var t in allTiles)
        {
            if (t == null) continue;
            float dist = Vector3.SqrMagnitude(position - t.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = t;
            }
        }
        return nearest;
    }
}
