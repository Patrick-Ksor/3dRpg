using UnityEngine;
using UnityEditor;

public class BattleEnvironmentGenerator : MonoBehaviour
{
    [MenuItem("RPG/Generate Battle Environment")]
    public static void Generate()
    {
        string basePath = "Assets/OccaSoftware/Low Poly Fantasy Village/Meshes/";
        
        // Find or create root
        GameObject root = GameObject.Find("Battle Environment");
        if (root != null)
        {
            DestroyImmediate(root);
        }
        root = new GameObject("Battle Environment");

        // Define clear area for the battle (X: -4 to 4, Z: -2 to 3)
        // We will scatter things around this area.
        
        // 1. Scatter some trees in the background and edges
        for (int i = 0; i < 15; i++)
        {
            float x = Random.Range(-10f, 10f);
            float z = Random.Range(4f, 12f);
            SpawnRandomPrefab(basePath, new string[] { "Tree1.fbx", "Tree2.fbx", "pine tree1.fbx", "pine tree2.fbx" }, 
                new Vector3(x, 0, z), root.transform);
        }
        
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(-10f, 10f);
            float z = Random.Range(-8f, -3f);
            SpawnRandomPrefab(basePath, new string[] { "Tree3.fbx", "Tree4.fbx", "pine tree3.fbx" }, 
                new Vector3(x, 0, z), root.transform);
        }

        // 2. Add some houses in the background
        SpawnRandomPrefab(basePath, new string[] { "House1.fbx", "House2.fbx", "House3.fbx" }, 
            new Vector3(-5, 0, 7), root.transform, Random.Range(160f, 200f));
            
        SpawnRandomPrefab(basePath, new string[] { "House1.fbx", "House2.fbx", "House3.fbx" }, 
            new Vector3(4, 0, 8), root.transform, Random.Range(160f, 200f));

        // 3. Add some rocks at the edges
        for (int i = 0; i < 10; i++)
        {
            float x = Random.Range(0, 2) == 0 ? Random.Range(-8f, -4f) : Random.Range(4f, 8f);
            float z = Random.Range(-2f, 5f);
            SpawnRandomPrefab(basePath, new string[] { "Rock1.fbx", "Rock2.fbx", "Rock3.fbx" }, 
                new Vector3(x, 0, z), root.transform);
        }

        // 4. Scatter some flowers in the foreground / middle
        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(-5f, 5f);
            float z = Random.Range(-4f, 6f);
            SpawnRandomPrefab(basePath, new string[] { "flower1.fbx", "flower2.fbx", "flower3.fbx" }, 
                new Vector3(x, 0, z), root.transform);
        }
        
        // 5. Add a couple of crates near the houses
        SpawnRandomPrefab(basePath, new string[] { "crate.fbx" }, new Vector3(-3.5f, 0, 7.5f), root.transform);
        SpawnRandomPrefab(basePath, new string[] { "crate.fbx" }, new Vector3(-4f, 0, 7f), root.transform);
        
        Debug.Log("Battle Environment successfully generated from OccaSoftware meshes! You can re-run this to randomize again.");
    }

    private static void SpawnRandomPrefab(string basePath, string[] options, Vector3 position, Transform parent, float? yRotation = null)
    {
        string chosen = options[Random.Range(0, options.Length)];
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath + chosen);
        
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = position;
            instance.transform.SetParent(parent);
            
            // Apply randomized Y rotation, or specific one
            float rotY = yRotation.HasValue ? yRotation.Value : Random.Range(0f, 360f);
            
            // We append the Y rotation so we don't break FXB X/Z import orientations (-90 X is common)
            instance.transform.Rotate(0, rotY, 0, Space.World);
            
            // Randomize scale slightly for trees and rocks
            if (chosen.ToLower().Contains("tree") || chosen.ToLower().Contains("rock") || chosen.ToLower().Contains("flower"))
            {
                instance.transform.localScale *= Random.Range(0.8f, 1.2f);
            }
        }
        else
        {
            Debug.LogWarning("Could not find mesh at: " + basePath + chosen);
        }
    }
}
