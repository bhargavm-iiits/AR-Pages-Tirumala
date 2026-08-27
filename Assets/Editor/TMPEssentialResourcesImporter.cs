using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Automatically imports TMP Essential Resources on project load if they are missing.
/// </summary>
[InitializeOnLoad]
public static class TMPEssentialResourcesImporter
{
    static TMPEssentialResourcesImporter()
    {
        // Delay the check until the editor is fully initialized
        EditorApplication.delayCall += ImportIfMissing;
    }

    private static void ImportIfMissing()
    {
        EditorApplication.delayCall -= ImportIfMissing;

        // Check if TMP Settings asset already exists
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings != null)
        {
            Debug.Log("[TMP Importer] TMP Essential Resources are already present. No action needed.");
            return;
        }

        Debug.Log("[TMP Importer] TMP Essential Resources missing — importing now...");
        ImportEssentialResources();
    }

    [MenuItem("Tools/TMP/Import Essential Resources Now")]
    public static void ImportEssentialResources()
    {
        // This mirrors what Unity does internally when you click
        // Window > TextMeshPro > Import TMP Essential Resources
        TMP_PackageResourceImporter.ImportResources(true, false, false);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TMP Importer] TMP Essential Resources imported successfully!");
    }
}
