/*
 * Copyright (c) CompuGenius Programs. All Rights Reserved.
 * https://www.cgprograms.com
 */

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CGPrograms
{
    public class CGProgramsUtils : EditorWindow, IHasCustomMenu
    {
        #region Variables

        private List<string> scenes;
        private readonly string[] permanentFolders = { "Scenes", "Scripts", "Prefabs" };
        [SerializeField] private List<string> folders = new();
        [SerializeField] private List<string> gameObjects = new();
        private string newFolderPath = "";
        private string newGameObject = "";
        private bool showAddFolderSection;
        private bool showAddGameObjectSection;
        private string foldersKey;
        private string gameObjectsKey;

        private LayerMask selectedLayerMask;

        private Vector2 scrollPosition;

        #endregion

        #region Unity Methods

        private void OnEnable()
        {
            foldersKey = $"{Application.productName}_CGProgramsUtilsFolders";
            gameObjectsKey = $"{Application.productName}_CGProgramsUtilsGameObjects";

            RefreshScenes();
            LoadFolders();
            LoadGameObjects();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Scenes", EditorStyles.boldLabel);

            var refreshIcon = EditorGUIUtility.IconContent("Refresh");
            if (GUILayout.Button(refreshIcon, GUILayout.Width(position.width * 0.1f))) RefreshScenes();
            GUILayout.EndHorizontal();

            foreach (var scene in scenes.Where(scene =>
                         GUILayout.Button(scene, GUILayout.Width(position.width * 0.9f))))
                OpenScene(scene);

            GUILayout.Space(10);
            GUILayout.Label("Folders", EditorStyles.boldLabel);
            for (var i = 0; i < folders.Count; i++)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(folders[i], GUILayout.Width(position.width * 0.8f)))
                    CoroutineRunner.StartCoroutine(OpenFolder("Assets/" + folders[i]));

                if (!permanentFolders.Contains(folders[i]))
                    if (GUILayout.Button("x", GUILayout.Width(position.width * 0.1f)))
                    {
                        folders.RemoveAt(i);
                        SaveFolders();
                    }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            showAddFolderSection = EditorGUILayout.Foldout(showAddFolderSection, "Add New Folder", true);
            if (showAddFolderSection)
            {
                newFolderPath = EditorGUILayout.TextField(newFolderPath);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Add Folder", GUILayout.Width(position.width * 0.45f)))
                {
                    if (!string.IsNullOrWhiteSpace(newFolderPath) && !folders.Contains(newFolderPath) &&
                        AssetDatabase.IsValidFolder("Assets/" + newFolderPath))
                    {
                        folders.Add(newFolderPath);
                        SaveFolders();
                    }

                    newFolderPath = "";
                }

                if (GUILayout.Button("Add Open Folder", GUILayout.Width(position.width * 0.45f)))
                {
                    var obj = Selection.activeObject;
                    if (obj != null)
                    {
                        newFolderPath = AssetDatabase.GetAssetPath(obj);
                        if (!AssetDatabase.IsValidFolder(newFolderPath))
                            newFolderPath = Path.GetDirectoryName(newFolderPath);

                        newFolderPath = newFolderPath.Replace("Assets/", "");
                    }

                    if (!folders.Contains(newFolderPath))
                    {
                        folders.Add(newFolderPath);
                        SaveFolders();
                    }

                    newFolderPath = "";
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            GUILayout.Label("Game Objects", EditorStyles.boldLabel);
            for (var i = 0; i < gameObjects.Count; i++)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(gameObjects[i], GUILayout.Width(position.width * 0.8f)))
                {
                    var go = GameObject.Find(gameObjects[i]);
                    if (go != null)
                        Selection.activeGameObject = go;
                }

                if (GUILayout.Button("x", GUILayout.Width(position.width * 0.1f)))
                {
                    gameObjects.RemoveAt(i);
                    SaveGameObjects();
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            showAddGameObjectSection = EditorGUILayout.Foldout(showAddGameObjectSection, "Add New Game Object", true);
            if (showAddGameObjectSection)
            {
                newGameObject = EditorGUILayout.TextField(newGameObject);
                if (GUILayout.Button("Add Game Object", GUILayout.Width(position.width * 0.9f)))
                {
                    if (!gameObjects.Contains(newGameObject))
                    {
                        gameObjects.Add(newGameObject);
                        SaveGameObjects();
                    }

                    newGameObject = "";
                }
            }

            GUILayout.Space(10);

            if (GUILayout.Button("Find Unnecessary Meshes", GUILayout.Width(position.width * 0.9f)))
                FindUnnecessaryMeshes();
            if (GUILayout.Button("Remove Missing Materials", GUILayout.Width(position.width * 0.9f)))
                RemoveMissingMaterials();
            if (GUILayout.Button("Remove Missing Scripts", GUILayout.Width(position.width * 0.9f)))
                RemoveMissingScripts();

            if (GUILayout.Button("Clear Scene Lighting", GUILayout.Width(position.width * 0.9f)))
                ClearSceneLighting();

            if (GUILayout.Button("Fix VRChat Screwing Up Default Material", GUILayout.Width(position.width * 0.9f)))
                ResetDefaultMaterial();

            GUILayout.Space(10);
            GUILayout.Label("Select Objects by Layer", EditorStyles.boldLabel);

            selectedLayerMask = EditorGUILayout.MaskField(
                "Layer Mask",
                selectedLayerMask,
                InternalEditorUtility.layers
            );

            if (GUILayout.Button("Select All Objects on Selected Layers", GUILayout.Width(position.width * 0.9f)))
            {
                SelectObjectsOnLayers(selectedLayerMask);
            }

            EditorGUILayout.EndScrollView();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Edit Script"), false, EditScript);
        }

        #endregion

        #region Custom Methods

        [MenuItem("Window/CGProgramsUtils")]
        public static void ShowWindow()
        {
            GetWindow<CGProgramsUtils>("CGProgramsUtils");
        }

        private void RefreshScenes()
        {
            scenes = new List<string>();
            var sceneCount = SceneManager.sceneCountInBuildSettings;
            for (var i = 0; i < sceneCount; i++)
            {
                var sceneName = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                if (!scenes.Contains(sceneName))
                    scenes.Add(sceneName);
            }
        }

        private void OpenScene(string sceneName)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SceneUtility.GetScenePathByBuildIndex(scenes.IndexOf(sceneName)));
        }

        private static IEnumerator OpenFolder(string folderPath)
        {
            var folder = AssetDatabase.LoadAssetAtPath<Object>(folderPath);

            EditorUtility.FocusProjectWindow();

            AssetDatabase.OpenAsset(folder);
            yield return null;
            AssetDatabase.OpenAsset(folder);
        }

        private void SaveFolders()
        {
            var foldersToSave = folders.Except(permanentFolders).ToArray();
            EditorPrefs.SetString(foldersKey, string.Join(";", foldersToSave));
        }

        private void LoadFolders()
        {
            folders = permanentFolders.ToList();
            if (!EditorPrefs.HasKey(foldersKey)) return;

            var saved = EditorPrefs.GetString(foldersKey);
            if (string.IsNullOrEmpty(saved)) return;

            var savedFolders = saved.Split(';');
            folders.AddRange(savedFolders);
        }

        private void SaveGameObjects()
        {
            EditorPrefs.SetString(gameObjectsKey, string.Join(";", gameObjects));
        }

        private void LoadGameObjects()
        {
            gameObjects.Clear();
            if (!EditorPrefs.HasKey(gameObjectsKey)) return;

            var saved = EditorPrefs.GetString(gameObjectsKey);
            if (string.IsNullOrEmpty(saved)) return;

            var saveGameObjects = saved.Split(';');
            gameObjects.AddRange(saveGameObjects);
        }

        private void EditScript()
        {
            var script = MonoScript.FromScriptableObject(this);
            AssetDatabase.OpenAsset(script);
        }

        private static void FindUnnecessaryMeshes()
        {
            var meshFilters = FindObjectsOfType<MeshFilter>();
            var unnecessaryMeshes = meshFilters.Where(meshFilter =>
                !meshFilter.GetComponent<MeshRenderer>() && !meshFilter.GetComponent<Collider>() &&
                meshFilter.transform.childCount == 0).ToList();
            Selection.objects = unnecessaryMeshes.Select(meshFilter => meshFilter.gameObject).ToArray<Object>();
        }

        private static void RemoveMissingScripts()
        {
            foreach (var gameObject in FindObjectsOfType<GameObject>(true))
                if (gameObject.GetComponentsInChildren<Component>().Any(component => component == null))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
        }

        private static void RemoveMissingMaterials()
        {
            foreach (var meshRenderer in FindObjectsOfType<MeshRenderer>(true))
            {
                var materials = meshRenderer.sharedMaterials.ToList();
                if (materials.RemoveAll(material => material == null) > 0)
                {
                    meshRenderer.sharedMaterials = materials.ToArray();
                }
            }
        }

        private static void ClearSceneLighting()
        {
            Lightmapping.Clear();
            Lightmapping.ClearDiskCache();

            var sceneName = SceneManager.GetActiveScene().name;

            var bakeryLightmapsPath = Application.dataPath + "/BakeryLightmaps";
            if (Directory.Exists(bakeryLightmapsPath))
            {
                var files = Directory.GetFiles(bakeryLightmapsPath, $"{sceneName}*", SearchOption.TopDirectoryOnly);
                foreach (var file in files) File.Delete(file);
            }

            var reflectionProbesPath = Application.dataPath + $"/Scenes/{sceneName}";
            if (Directory.Exists(reflectionProbesPath))
            {
                var files = Directory.GetFiles(reflectionProbesPath, "*exr", SearchOption.TopDirectoryOnly);
                foreach (var file in files) File.Delete(file);
            }

            AssetDatabase.Refresh();
        }

        private static void ResetDefaultMaterial()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var meshRenderer = cube.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial.shader = Shader.Find("Standard");
            DestroyImmediate(cube);
        }

        private static void SelectObjectsOnLayers(LayerMask mask)
        {
            var allObjects = FindObjectsOfType<GameObject>(true);
            var matchingObjects = allObjects
                .Where(go => ((1 << go.layer) & mask.value) != 0)
                .ToArray<Object>();

            Selection.objects = matchingObjects;
        }

        #endregion
    }
}

public class CoroutineRunner : ScriptableObject
{
    public static void StartCoroutine(IEnumerator routine)
    {
        var runner = CreateInstance<CoroutineRunner>();
        runner.StartCoroutineInternal(routine);
    }

    private void StartCoroutineInternal(IEnumerator routine)
    {
        EditorApplication.update += () =>
        {
            if (routine.MoveNext()) return;
            EditorApplication.update -= () => StartCoroutineInternal(routine);
            DestroyImmediate(this);
        };
    }
}
