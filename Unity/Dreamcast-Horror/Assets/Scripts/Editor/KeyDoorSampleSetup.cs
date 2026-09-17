using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// One-time graybox authoring command. Production art remains a Blender workflow.
public static class KeyDoorSampleSetup
{
    private static GameObject Proxy(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().sharedMaterial = material;
        return obj;
    }

    private static Material Material(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (result != null) return result;
        result = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_TestRoom_Crate.mat"));
        result.name = name;
        result.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(result, path);
        return result;
    }

    public static void Build()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            if (GameObject.Find("KeyDoorSlice") != null)
                throw new InvalidOperationException("The key-door slice already exists. Use Export sample room after editing it.");
            Transform root = new GameObject("KeyDoorSlice").transform;
            Material keyMaterial = Material("M_Slice_Key", new Color(0.95f, 0.66f, 0.12f));
            Material doorMaterial = Material("M_Slice_Door", new Color(0.30f, 0.14f, 0.10f));
            Transform key = new GameObject("BrassKey").transform;
            key.SetParent(root, false);
            key.position = new Vector3(0, 0.65f, 2.8f);
            Proxy("KeyHead", PrimitiveType.Cylinder, key, Vector3.zero, new Vector3(0.3f, 0.035f, 0.3f), keyMaterial);
            Proxy("KeyShaft", PrimitiveType.Cube, key, new Vector3(0, 0, -0.24f), new Vector3(0.07f, 0.06f, 0.42f), keyMaterial);
            Proxy("KeyTooth01", PrimitiveType.Cube, key, new Vector3(0.07f, 0, -0.32f), new Vector3(0.16f, 0.06f, 0.065f), keyMaterial);
            Proxy("KeyTooth02", PrimitiveType.Cube, key, new Vector3(0.07f, 0, -0.43f), new Vector3(0.16f, 0.06f, 0.065f), keyMaterial);

            Transform door = new GameObject("SliceDoor").transform;
            door.SetParent(root, false);
            door.position = new Vector3(0, 1.1f, -5);
            Proxy("DoorSlab", PrimitiveType.Cube, door, Vector3.zero, new Vector3(3, 2.2f, 0.16f), doorMaterial);
            Proxy("DoorHandle", PrimitiveType.Sphere, door, new Vector3(1.1f, 0, 0.12f), Vector3.one * 0.12f, keyMaterial);
            CppCollision collision = door.gameObject.AddComponent<CppCollision>();
            var shapeFields = new SerializedObject(collision);
            shapeFields.FindProperty("mode").enumValueIndex = (int)CppCollision.CollisionMode.Manual;
            shapeFields.FindProperty("manualShape").enumValueIndex = (int)CppCollision.ManualShape.Box;
            shapeFields.FindProperty("manualSize").vector3Value = new Vector3(3, 2.2f, 0.16f);
            shapeFields.ApplyModifiedPropertiesWithoutUndo();
            collision.Recalculate();

            Transform exit = new GameObject("SliceExit").transform;
            exit.SetParent(root, false);
            exit.position = new Vector3(0, 0, -5.9f);
            Proxy("ExitLanding", PrimitiveType.Cube, root, new Vector3(0, -0.1f, -6), new Vector3(3, 0.2f, 2.2f),
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_TestRoom_Floor.mat"));
            KeyDoorPresentation presentation = root.gameObject.AddComponent<KeyDoorPresentation>();
            var fields = new SerializedObject(presentation);
            fields.FindProperty("keyVisual").objectReferenceValue = key;
            fields.FindProperty("doorVisual").objectReferenceValue = door;
            fields.FindProperty("doorCollision").objectReferenceValue = collision;
            fields.FindProperty("exitMarker").objectReferenceValue = exit;
            fields.ApplyModifiedPropertiesWithoutUndo();

            // Keep the south door and landing in view while approaching the exit.
            Transform camera = GameObject.Find("GameplayCamera_01").transform;
            camera.position = new Vector3(0, 6.33f, -5.14f);
            camera.rotation = Quaternion.Euler(70, 0, 0);
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))
                throw new InvalidOperationException("Unable to save key-door sample scene.");
            AssetDatabase.SaveAssets();
            RoomExporter.Export();
            Debug.Log("KEY-DOOR SAMPLE CREATED AND EXPORTED");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void FrameDoor()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Transform camera = GameObject.Find("GameplayCamera_01").transform;
            camera.position = new Vector3(0, 6.33f, -5.14f);
            camera.rotation = Quaternion.Euler(70, 0, 0);
            if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()))
                throw new InvalidOperationException("Unable to save sample camera.");
            RoomExporter.Export();
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
