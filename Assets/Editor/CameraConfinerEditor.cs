using UnityEditor;
using UnityEngine;
using CaveDweller.Core;

namespace CaveDweller.EditorTools
{
    [CustomEditor(typeof(CameraController))]
    public class CameraControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var camCtrl = (CameraController)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Quick Camera Confiner Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Fit / Create CameraBounds from Level Geometry", GUILayout.Height(28)))
            {
                CameraConfinerEditorUtility.SetupBoundsForActiveScene(camCtrl);
            }

            if (GUILayout.Button("Create New Camera Zone (Room / Arena)", GUILayout.Height(26)))
            {
                CameraConfinerEditorUtility.CreateNewCameraZone();
            }
        }
    }

    public static class CameraConfinerEditorUtility
    {
        [MenuItem("Tools/Camera/Setup Camera Bounds for Active Scene", false, 10)]
        public static void SetupBoundsMenu()
        {
            var camCtrl = Object.FindAnyObjectByType<CameraController>();
            SetupBoundsForActiveScene(camCtrl);
        }

        [MenuItem("Tools/Camera/Create New Camera Zone (Room)", false, 11)]
        public static void CreateNewCameraZoneMenu()
        {
            CreateNewCameraZone();
        }

        public static void SetupBoundsForActiveScene(CameraController camCtrl)
        {
            if (camCtrl == null)
            {
                camCtrl = Object.FindAnyObjectByType<CameraController>();
                if (camCtrl == null)
                {
                    Debug.LogError("[CameraConfiner] No CameraController found in active scene!");
                    return;
                }
            }

            // Find or calculate level bounds from all environment colliders
            Bounds levelBounds = new Bounds();
            bool hasBounds = false;

            var allColliders = Object.FindObjectsByType<Collider2D>();
            foreach (var col in allColliders)
            {
                // Only static level geometry: skip triggers, camera volumes, and anything
                // driven by a dynamic body (player, enemies, projectiles) regardless of tag.
                if (col.isTrigger) continue;
                if (CameraZone.IsCameraVolume(col)) continue;
                if (col.attachedRigidbody != null && col.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                if (col.CompareTag("Player")) continue;
                if (col.name.Contains("Bullet") || col.name.Contains("Tracer")) continue;

                if (!hasBounds)
                {
                    levelBounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    levelBounds.Encapsulate(col.bounds);
                }
            }

            if (!hasBounds)
            {
                Debug.LogWarning("[CameraConfiner] No level colliders found to calculate bounds from. Using default size.");
                levelBounds = new Bounds(Vector3.zero, new Vector3(40f, 20f, 0f));
            }

            // Expand margin so camera can see the borders comfortably
            float marginX = 2.5f;
            float marginY = 2.0f;
            levelBounds.Expand(new Vector3(marginX * 2f, marginY * 2f, 0f));

            // Find or create CameraBounds GameObject
            var boundsObj = GameObject.Find("CameraBounds");
            if (boundsObj == null)
            {
                boundsObj = new GameObject("CameraBounds");
                Undo.RegisterCreatedObjectUndo(boundsObj, "Create CameraBounds");
            }
            else
            {
                Undo.RecordObject(boundsObj, "Update CameraBounds");
            }

            boundsObj.transform.position = new Vector3(levelBounds.center.x, levelBounds.center.y, 0f);
            int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreLayer >= 0) boundsObj.layer = ignoreLayer;

            var boxCol = boundsObj.GetComponent<BoxCollider2D>();
            if (boxCol == null)
            {
                boxCol = Undo.AddComponent<BoxCollider2D>(boundsObj);
            }
            else
            {
                Undo.RecordObject(boxCol, "Update CameraBounds Collider");
            }

            boxCol.isTrigger = true;
            boxCol.offset = Vector2.zero;
            boxCol.size = new Vector2(levelBounds.size.x, levelBounds.size.y);

            // Wire to CameraController
            Undo.RecordObject(camCtrl, "Assign Camera Bounds");
            camCtrl.SetBounds(boxCol);

            Selection.activeGameObject = boundsObj;
            Debug.Log($"[CameraConfiner] Camera bounds successfully created at center {levelBounds.center} with size {levelBounds.size} and assigned to CameraController!");
        }

        public static void CreateNewCameraZone()
        {
            var zoneObj = new GameObject("CameraZone_Room");
            Undo.RegisterCreatedObjectUndo(zoneObj, "Create CameraZone");

            int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreLayer >= 0) zoneObj.layer = ignoreLayer;

            // Position at scene view center if available
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                Vector3 viewPos = sceneView.pivot;
                viewPos.z = 0f;
                zoneObj.transform.position = viewPos;
            }

            var box = Undo.AddComponent<BoxCollider2D>(zoneObj);
            box.isTrigger = true;
            box.size = new Vector2(25f, 15f);

            Undo.AddComponent<CameraZone>(zoneObj);

            Selection.activeGameObject = zoneObj;
            Debug.Log("[CameraConfiner] Created new CameraZone GameObject. Size and position it to cover your room.");
        }
    }
}
