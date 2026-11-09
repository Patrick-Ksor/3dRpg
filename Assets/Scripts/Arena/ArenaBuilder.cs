using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Builds a battle arena at runtime from an <see cref="ArenaThemeSO"/>.
///
/// Layout: a tiled floor, two back walls on the +X and +Z sides (the camera looks toward +X/+Z,
/// so the camera side stays open), columns, wall torches with real flickering lights, and props
/// scattered around the edges but never in the fighting area.
///
/// Every prefab is measured from its renderer bounds, so pivots and dimensions of the kit do not
/// matter. The floor surface is placed at y = 0 where the characters stand.
/// </summary>
public static class ArenaBuilder
{
    private const string ThemeResourcePath = "Arenas/DungeonArena";

    /// <summary>Builds the arena under <paramref name="parent"/>. Returns null if no valid theme exists.</summary>
    public static ArenaController Build(Transform parent, Light sun, Camera cam)
    {
        var theme = Resources.Load<ArenaThemeSO>(ThemeResourcePath);
        if (theme == null)
        {
            Debug.LogWarning($"[Arena] No theme at Resources/{ThemeResourcePath}; using the fallback floor.");
            return null;
        }
        if (theme.floorTiles == null || theme.floorTiles.Length == 0 || theme.floorTiles[0] == null || theme.wall == null)
        {
            Debug.LogWarning("[Arena] Theme is missing its floor tile or wall prefab; using the fallback floor.");
            return null;
        }

        if (theme.disableLegacySceneDecor) DisableLegacySceneDecor();

        var root = new GameObject("Arena");
        root.transform.SetParent(parent, false);

        var torchLights = new List<Light>();
        var rng = new System.Random(Random.Range(int.MinValue, int.MaxValue));

        Vector3 c = theme.arenaCenter;
        float hx = theme.floorHalfExtents.x;
        float hz = theme.floorHalfExtents.y;

        BuildFloor(theme, root.transform, c, hx, hz, rng);
        float wallHeight = BuildWalls(theme, root.transform, c, hx, hz);
        BuildColumns(theme, root.transform, c, hx, hz);
        BuildTorches(theme, root.transform, c, hx, hz, wallHeight, torchLights);
        ScatterProps(theme, root.transform, c, hx, hz, rng);

        if (theme.ambientParticles != null)
        {
            var fx = Object.Instantiate(theme.ambientParticles, c + Vector3.up * 2.5f, Quaternion.identity, root.transform);
            fx.name = "AmbientParticles";
        }

        var controller = root.AddComponent<ArenaController>();
        controller.Init(theme, sun, cam, torchLights);
        return controller;
    }

    /// <summary>
    /// BattleScene ships a small hand-placed dungeon ("Dungeon" root) and its own directional light.
    /// Switch those off so they do not fight with the generated arena and its mood lighting.
    /// </summary>
    private static void DisableLegacySceneDecor()
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (go.name == "Dungeon" || go.name == "Directional Light")
            {
                go.SetActive(false);
                Debug.Log($"[Arena] Disabled legacy scene object '{go.name}'.");
            }
        }
    }

    // ── Floor ────────────────────────────────────────────────────────────────

    private static void BuildFloor(ArenaThemeSO theme, Transform root, Vector3 c, float hx, float hz, System.Random rng)
    {
        var floor = new GameObject("Floor").transform;
        floor.SetParent(root, false);

        Vector3 tileSize = MeasureSize(theme.floorTiles[0]);
        if (tileSize.x < 0.1f || tileSize.z < 0.1f)
        {
            Debug.LogWarning("[Arena] Could not measure the floor tile; skipping floor.");
            return;
        }

        // Variants are only used if they match the first tile's footprint.
        var variants = new List<GameObject>();
        foreach (var t in theme.floorTiles)
        {
            if (t == null) continue;
            Vector3 s = MeasureSize(t);
            if (Mathf.Abs(s.x - tileSize.x) < tileSize.x * 0.1f && Mathf.Abs(s.z - tileSize.z) < tileSize.z * 0.1f)
                variants.Add(t);
        }
        bool square = Mathf.Abs(tileSize.x - tileSize.z) < tileSize.x * 0.05f;

        int cols = Mathf.CeilToInt(hx * 2f / tileSize.x);
        int rows = Mathf.CeilToInt(hz * 2f / tileSize.z);
        float startX = c.x - cols * tileSize.x * 0.5f + tileSize.x * 0.5f;
        float startZ = c.z - rows * tileSize.z * 0.5f + tileSize.z * 0.5f;

        for (int ix = 0; ix < cols; ix++)
        {
            for (int iz = 0; iz < rows; iz++)
            {
                var prefab = variants[rng.Next(variants.Count)];
                float yaw = square ? 90f * rng.Next(4) : 0f;
                var tile = Object.Instantiate(prefab, floor);
                tile.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                // Top surface at y = -0.02 so characters standing at y = 0 never sink into the tile.
                PlaceByBounds(tile, new Vector3(startX + ix * tileSize.x, 0f, startZ + iz * tileSize.z), BoundsAnchor.Top, -0.02f);
            }
        }
    }

    // ── Walls and columns ────────────────────────────────────────────────────

    /// <summary>Builds the +X and +Z walls. Returns the measured wall height.</summary>
    private static float BuildWalls(ArenaThemeSO theme, Transform root, Vector3 c, float hx, float hz)
    {
        var walls = new GameObject("Walls").transform;
        walls.SetParent(root, false);

        // Measure a wall rotated to face -X (it will run along Z).
        var probe = Object.Instantiate(theme.wall);
        probe.transform.rotation = Quaternion.Euler(0f, -90f + theme.wallYawOffset, 0f);
        Vector3 size = GetBounds(probe).size;
        Object.Destroy(probe);
        float segLength = size.z;
        float wallHeight = size.y;
        if (segLength < 0.1f) return wallHeight;

        // +X wall: runs along Z at x = c.x + hx, faces -X.
        float spanZ = hz * 2f;
        int countZ = Mathf.CeilToInt(spanZ / segLength);
        float pitchZ = spanZ / countZ;
        for (int i = 0; i < countZ; i++)
        {
            var w = Object.Instantiate(theme.wall, walls);
            w.transform.rotation = Quaternion.Euler(0f, -90f + theme.wallYawOffset, 0f);
            PlaceWall(w, new Vector3(c.x + hx, 0f, c.z - hz + pitchZ * (i + 0.5f)), true);
        }

        // +Z wall: runs along X at z = c.z + hz, faces -Z.
        float spanX = hx * 2f;
        int countX = Mathf.CeilToInt(spanX / segLength);
        float pitchX = spanX / countX;
        for (int i = 0; i < countX; i++)
        {
            var w = Object.Instantiate(theme.wall, walls);
            w.transform.rotation = Quaternion.Euler(0f, 180f + theme.wallYawOffset, 0f);
            PlaceWall(w, new Vector3(c.x - hx + pitchX * (i + 0.5f), 0f, c.z + hz), false);
        }

        return wallHeight;
    }

    /// <summary>Places a wall so its inner (arena-facing) face sits on the floor edge and it stands on the floor.</summary>
    private static void PlaceWall(GameObject wall, Vector3 edgePoint, bool alongZ)
    {
        Bounds b = GetBounds(wall);
        Vector3 delta = Vector3.zero;
        if (alongZ)
        {
            delta.x = edgePoint.x - b.min.x;      // wall extends outward (+X) from the edge
            delta.z = edgePoint.z - b.center.z;
        }
        else
        {
            delta.z = edgePoint.z - b.min.z;      // wall extends outward (+Z) from the edge
            delta.x = edgePoint.x - b.center.x;
        }
        delta.y = -0.05f - b.min.y;               // bottom slightly below floor to hide seams
        wall.transform.position += delta;
    }

    private static void BuildColumns(ArenaThemeSO theme, Transform root, Vector3 c, float hx, float hz)
    {
        if (theme.column == null) return;
        var cols = new GameObject("Columns").transform;
        cols.SetParent(root, false);

        // Corner + every ~7 units along both walls.
        var points = new List<Vector3> { new Vector3(c.x + hx, 0f, c.z + hz) };
        for (float z = c.z - hz; z < c.z + hz - 1f; z += 7f)
            points.Add(new Vector3(c.x + hx, 0f, z));
        for (float x = c.x - hx; x < c.x + hx - 1f; x += 7f)
            points.Add(new Vector3(x, 0f, c.z + hz));

        foreach (var p in points)
        {
            var col = Object.Instantiate(theme.column, cols);
            // Nudge inward so the column stands against the wall's inner face.
            Vector3 size = GetBounds(col).size;
            Vector3 target = p;
            if (Mathf.Approximately(p.x, c.x + hx)) target.x -= size.x * 0.4f;
            if (Mathf.Approximately(p.z, c.z + hz)) target.z -= size.z * 0.4f;
            PlaceByBounds(col, target, BoundsAnchor.Bottom, -0.05f);
        }
    }

    // ── Torches ──────────────────────────────────────────────────────────────

    private static void BuildTorches(ArenaThemeSO theme, Transform root, Vector3 c, float hx, float hz, float wallHeight, List<Light> lights)
    {
        var torches = new GameObject("Torches").transform;
        torches.SetParent(root, false);
        float y = Mathf.Max(1.2f, wallHeight * theme.torchHeightFraction);
        float spacing = Mathf.Max(2f, theme.torchSpacing);

        // +X wall
        for (float z = c.z - hz + spacing * 0.5f; z < c.z + hz; z += spacing)
            AddTorch(theme, torches, new Vector3(c.x + hx, y, z), Vector3.left, lights);

        // +Z wall
        for (float x = c.x - hx + spacing * 0.5f; x < c.x + hx; x += spacing)
            AddTorch(theme, torches, new Vector3(x, y, c.z + hz), Vector3.back, lights);
    }

    private static void AddTorch(ArenaThemeSO theme, Transform parent, Vector3 wallPoint, Vector3 inward, List<Light> lights)
    {
        var holder = new GameObject("Torch");
        holder.transform.SetParent(parent, false);
        holder.transform.position = wallPoint + inward * 0.35f;

        if (theme.torch != null)
        {
            var mesh = Object.Instantiate(theme.torch, holder.transform);
            mesh.transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
            Bounds b = GetBounds(mesh);
            // Back of the torch against the wall, vertically centred on the torch height.
            Vector3 d = Vector3.zero;
            if (inward == Vector3.left) d.x = wallPoint.x - b.max.x;
            else d.z = wallPoint.z - b.max.z;
            d.y = wallPoint.y - b.center.y;
            mesh.transform.position += d;
        }

        var lightGO = new GameObject("TorchLight");
        lightGO.transform.SetParent(holder.transform, false);
        lightGO.transform.position = wallPoint + inward * 0.7f;
        var l = lightGO.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1f, 0.6f, 0.25f);
        l.intensity = 3f;
        l.range = 8f;
        l.shadows = LightShadows.None;
        lightGO.AddComponent<TorchFlicker>();
        lights.Add(l);
    }

    // ── Props ────────────────────────────────────────────────────────────────

    private static void ScatterProps(ArenaThemeSO theme, Transform root, Vector3 c, float hx, float hz, System.Random rng)
    {
        if (theme.props == null || theme.props.Length == 0 || theme.propCount <= 0) return;
        var props = new GameObject("Props").transform;
        props.SetParent(root, false);

        var placed = new List<Vector3>();
        int attempts = 0;
        while (placed.Count < theme.propCount && attempts++ < theme.propCount * 12)
        {
            // Bias toward the two back walls where the camera looks, but allow the open edges too.
            float x = Mathf.Lerp(c.x - hx + 1f, c.x + hx - 1.2f, (float)rng.NextDouble());
            float z = Mathf.Lerp(c.z - hz + 1f, c.z + hz - 1.2f, (float)rng.NextDouble());
            if (rng.NextDouble() < 0.6)
            {
                if (rng.NextDouble() < 0.5) x = c.x + hx - 1.2f - (float)rng.NextDouble() * 1.2f;
                else z = c.z + hz - 1.2f - (float)rng.NextDouble() * 1.2f;
            }

            // Keep the fighting area (and the player-side camera corner) clear.
            if (Mathf.Abs(x - c.x) < theme.clearHalfExtents.x && Mathf.Abs(z - c.z) < theme.clearHalfExtents.y) continue;
            bool tooClose = false;
            foreach (var p in placed)
                if ((p.x - x) * (p.x - x) + (p.z - z) * (p.z - z) < 2.4f * 2.4f) { tooClose = true; break; }
            if (tooClose) continue;

            var prefab = theme.props[rng.Next(theme.props.Length)];
            if (prefab == null) continue;
            var go = Object.Instantiate(prefab, props);
            go.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            PlaceByBounds(go, new Vector3(x, 0f, z), BoundsAnchor.Bottom, 0f);
            placed.Add(new Vector3(x, 0f, z));

            // Dress tables and crates with a small prop on top.
            string n = prefab.name.ToLowerInvariant();
            if ((n.Contains("table") || n.Contains("box")) && theme.smallProps != null && theme.smallProps.Length > 0 && rng.NextDouble() < 0.7)
            {
                var small = theme.smallProps[rng.Next(theme.smallProps.Length)];
                if (small != null)
                {
                    Bounds top = GetBounds(go);
                    var s = Object.Instantiate(small, props);
                    s.transform.rotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                    PlaceByBounds(s, new Vector3(top.center.x, 0f, top.center.z), BoundsAnchor.Bottom, top.max.y);
                }
            }
        }
    }

    // ── Measuring and placing ────────────────────────────────────────────────

    private enum BoundsAnchor { Bottom, Top }

    /// <summary>Combined bounds of all mesh renderers (particles excluded).</summary>
    private static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        bool has = false;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (!has) { b = r.bounds; has = true; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    /// <summary>Instantiates a prefab temporarily at the origin to read its size, then destroys it.</summary>
    private static Vector3 MeasureSize(GameObject prefab)
    {
        var tmp = Object.Instantiate(prefab);
        Vector3 size = GetBounds(tmp).size;
        Object.DestroyImmediate(tmp);   // immediate, so the temporary copy at the origin is never rendered
        return size;
    }

    /// <summary>Moves an object so the XZ centre of its bounds is at <paramref name="xz"/> and the chosen bottom/top edge is at <paramref name="y"/>.</summary>
    private static void PlaceByBounds(GameObject go, Vector3 xz, BoundsAnchor anchor, float y)
    {
        Bounds b = GetBounds(go);
        float edgeY = anchor == BoundsAnchor.Bottom ? b.min.y : b.max.y;
        go.transform.position += new Vector3(xz.x - b.center.x, y - edgeY, xz.z - b.center.z);
    }
}
