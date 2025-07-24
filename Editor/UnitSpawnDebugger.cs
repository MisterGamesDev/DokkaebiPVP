using UnityEngine;
using UnityEditor;
using Dokkaebi.Core.Data;
using Dokkaebi.Core;

/// <summary>
/// Editor utility to debug and fix unit spawning issues
/// </summary>
public class UnitSpawnDebugger : EditorWindow
{
    [MenuItem("Dokkaebi/Debug Unit Spawning")]
    public static void ShowWindow()
    {
        GetWindow<UnitSpawnDebugger>("Unit Spawn Debugger");
    }

    private void OnGUI()
    {
        GUILayout.Label("Unit Spawn Configuration Debugger", EditorStyles.boldLabel);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Check DataManager Configuration", GUILayout.Height(30)))
        {
            CheckDataManagerConfiguration();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Check UnitSpawnData Configuration", GUILayout.Height(30)))
        {
            CheckUnitSpawnDataConfiguration();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Auto-Fix Unit Spawn Configuration", GUILayout.Height(30)))
        {
            AutoFixConfiguration();
        }

        EditorGUILayout.Space();
        
        if (GUILayout.Button("Test Unit Spawning (Play Mode)", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                TestUnitSpawning();
            }
            else
            {
                Debug.LogWarning("Must be in Play Mode to test spawning!");
            }
        }
    }

    private void CheckDataManagerConfiguration()
    {
        Debug.Log("=== CHECKING DATAMANAGER CONFIGURATION ===");
        
        // Find DataManager in scene
        DataManager dataManager = FindObjectOfType<DataManager>();
        if (dataManager == null)
        {
            Debug.LogError("❌ DataManager not found in scene!");
            return;
        }
        
        Debug.Log("✅ DataManager found in scene");
        
        // Check if UnitSpawnData is assigned
        var unitSpawnData = dataManager.GetUnitSpawnData();
        if (unitSpawnData == null)
        {
            Debug.LogError("❌ UnitSpawnData is not assigned to DataManager!");
            
            // Try to find and auto-assign it
            string[] guids = AssetDatabase.FindAssets("t:UnitSpawnData");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                UnitSpawnData foundData = AssetDatabase.LoadAssetAtPath<UnitSpawnData>(path);
                Debug.Log($"📁 Found UnitSpawnData at: {path}");
                Debug.Log("💡 Use 'Auto-Fix Configuration' to assign it automatically");
            }
        }
        else
        {
            Debug.Log("✅ UnitSpawnData is assigned to DataManager");
            Debug.Log($"📊 Player spawns: {unitSpawnData.playerUnitSpawns.Count}");
            Debug.Log($"📊 Enemy spawns: {unitSpawnData.enemyUnitSpawns.Count}");
        }
    }

    private void CheckUnitSpawnDataConfiguration()
    {
        Debug.Log("=== CHECKING UNITSPAWNDATA CONFIGURATION ===");
        
        // Load the UnitSpawnData directly
        string[] guids = AssetDatabase.FindAssets("t:UnitSpawnData");
        if (guids.Length == 0)
        {
            Debug.LogError("❌ No UnitSpawnData assets found in project!");
            return;
        }
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnitSpawnData spawnData = AssetDatabase.LoadAssetAtPath<UnitSpawnData>(path);
            
            Debug.Log($"📁 Checking UnitSpawnData: {path}");
            
            // Check player spawns
            Debug.Log($"🔵 Player Unit Spawns: {spawnData.playerUnitSpawns.Count}");
            for (int i = 0; i < spawnData.playerUnitSpawns.Count; i++)
            {
                var spawn = spawnData.playerUnitSpawns[i];
                if (spawn.unitDefinition == null)
                {
                    Debug.LogError($"❌ Player spawn {i} has null unit definition!");
                }
                else
                {
                    Debug.Log($"✅ Player spawn {i}: {spawn.unitDefinition.displayName} at {spawn.spawnPosition}");
                }
            }
            
            // Check enemy spawns
            Debug.Log($"🔴 Enemy Unit Spawns: {spawnData.enemyUnitSpawns.Count}");
            for (int i = 0; i < spawnData.enemyUnitSpawns.Count; i++)
            {
                var spawn = spawnData.enemyUnitSpawns[i];
                if (spawn.unitDefinition == null)
                {
                    Debug.LogError($"❌ Enemy spawn {i} has null unit definition!");
                }
                else
                {
                    Debug.Log($"✅ Enemy spawn {i}: {spawn.unitDefinition.displayName} at {spawn.spawnPosition}");
                }
            }
        }
    }

    private void AutoFixConfiguration()
    {
        Debug.Log("=== AUTO-FIXING CONFIGURATION ===");
        
        // 1. Find or create DataManager
        DataManager dataManager = FindObjectOfType<DataManager>();
        if (dataManager == null)
        {
            Debug.LogError("❌ Cannot auto-fix: DataManager not found in scene!");
            return;
        }
        
        // 2. Find UnitSpawnData
        string[] spawnDataGuids = AssetDatabase.FindAssets("t:UnitSpawnData");
        if (spawnDataGuids.Length == 0)
        {
            Debug.LogError("❌ Cannot auto-fix: No UnitSpawnData found in project!");
            return;
        }
        
        string spawnDataPath = AssetDatabase.GUIDToAssetPath(spawnDataGuids[0]);
        UnitSpawnData spawnData = AssetDatabase.LoadAssetAtPath<UnitSpawnData>(spawnDataPath);
        
        // 3. Assign UnitSpawnData to DataManager using reflection (since it's private)
        var field = typeof(DataManager).GetField("unitSpawnData", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(dataManager, spawnData);
            EditorUtility.SetDirty(dataManager);
            Debug.Log($"✅ Assigned UnitSpawnData to DataManager: {spawnDataPath}");
        }
        
        // 4. Fix missing unit definitions
        string[] unitDefGuids = AssetDatabase.FindAssets("t:UnitDefinitionData");
        if (unitDefGuids.Length >= 2)
        {
            var playerUnit = AssetDatabase.LoadAssetAtPath<UnitDefinitionData>(AssetDatabase.GUIDToAssetPath(unitDefGuids[0]));
            var enemyUnit = AssetDatabase.LoadAssetAtPath<UnitDefinitionData>(AssetDatabase.GUIDToAssetPath(unitDefGuids[1]));
            
            // Clear existing spawn configs
            spawnData.playerUnitSpawns.Clear();
            spawnData.enemyUnitSpawns.Clear();
            
            // Add player spawn
            spawnData.playerUnitSpawns.Add(new UnitSpawnConfig
            {
                unitDefinition = playerUnit,
                spawnPosition = new Vector2Int(2, 1)
            });
            
            // Add enemy spawn
            spawnData.enemyUnitSpawns.Add(new UnitSpawnConfig
            {
                unitDefinition = enemyUnit,
                spawnPosition = new Vector2Int(7, 8)
            });
            
            EditorUtility.SetDirty(spawnData);
            AssetDatabase.SaveAssets();
            
            Debug.Log("✅ Fixed UnitSpawnData with proper unit definitions");
            Debug.Log($"🔵 Player Unit: {playerUnit.displayName} at (2,1)");
            Debug.Log($"🔴 Enemy Unit: {enemyUnit.displayName} at (7,8)");
        }
        
        Debug.Log("🎉 Auto-fix complete! Try playing the scene now.");
    }

    private void TestUnitSpawning()
    {
        Debug.Log("=== TESTING UNIT SPAWNING ===");
        
        if (UnitManager.Instance == null)
        {
            Debug.LogError("❌ UnitManager.Instance is null!");
            return;
        }
        
        if (DataManager.Instance == null)
        {
            Debug.LogError("❌ DataManager.Instance is null!");
            return;
        }
        
        var spawnData = DataManager.Instance.GetUnitSpawnData();
        if (spawnData == null)
        {
            Debug.LogError("❌ UnitSpawnData is null!");
            return;
        }
        
        Debug.Log("✅ All managers available, attempting spawn test...");
        UnitManager.Instance.SpawnUnitsFromConfiguration();
        
        var allUnits = UnitManager.Instance.GetAliveUnits();
        Debug.Log($"📊 Total units spawned: {allUnits.Count}");
    }
} 