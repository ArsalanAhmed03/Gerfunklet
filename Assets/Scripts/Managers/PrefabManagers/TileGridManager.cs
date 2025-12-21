using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class TileGridManager : NetworkBehaviour
{
    public static TileGridManager Instance { get; private set; }

    [Header("Existing Tiles (scene placed)")]
    [SerializeField] private Transform tilesRoot;   // parent containing all tiles in the scene
    [SerializeField] private float tileSize = 2f;   // informational (used for edge tolerance)

    [Header("Shrinking arena (Overtime only)")]
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

        // IMPORTANT:
        // Tiles are scene-placed NetworkObjects, so DO NOT call Spawn() here.
        // Netcode spawns scene objects automatically when the scene is loaded in a network session.
    }

    private void Update()
    {
        if (!IsServer) return;
        if (allTiles.Count == 0) return;

        if (MatchManager.Instance == null) return;
        if (MatchManager.Instance.Phase.Value != (int)MatchManager.MatchPhase.Overtime) return;

        collapseTimer += Time.deltaTime;
        if (collapseTimer >= collapseInterval)
        {
            collapseTimer = 0f;
            CollapseOuterRing();
        }
    }

    private void CollapseOuterRing()
    {
        // Alive = isActive true (your new TileBehaviour)
        List<TileBehaviour> aliveTiles = new List<TileBehaviour>();
        foreach (var t in allTiles)
        {
            if (t == null) continue;
            if (!t.IsAlive) continue; // uses isActive.Value
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

            bool onLeft = Mathf.Abs(p.x - minX) <= tol;
            bool onRight = Mathf.Abs(p.x - maxX) <= tol;
            bool onBottom = Mathf.Abs(p.z - minZ) <= tol;
            bool onTop = Mathf.Abs(p.z - maxZ) <= tol;

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
            if (t == null) continue;
            if (!t.IsAlive) continue;
            if (exclude != null && t == exclude) continue;

            candidates.Add(t);
        }

        if (candidates.Count == 0)
            return null;

        int idx = Random.Range(0, candidates.Count);
        return candidates[idx];
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
            if (!t.IsAlive) continue;

            float dist = Vector3.SqrMagnitude(position - t.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = t;
            }
        }
        return nearest;
    }

    // Optional: reset all tiles for a new round (server calls this)
    public void ResetAllTilesForNewRoundServer()
    {
        if (!IsServer) return;

        foreach (var t in allTiles)
        {
            if (t == null) continue;
            t.ResetTileForNewRoundServer();
        }

        collapseTimer = 0f;
    }
}
