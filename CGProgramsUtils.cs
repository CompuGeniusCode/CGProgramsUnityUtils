/*
 * Copyright (c) CompuGenius Programs. All Rights Reserved.
 * https://www.cgprograms.com
 */

#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        private Material materialToReplace;
        private Material materialToReplaceWith;

        private Shader shaderToReplace;
        private Shader shaderToReplaceWith;

        private readonly string[] texturePlatforms =
        {
            "Default", "Standalone", "Android", "iPhone", "tvOS", "WebGL", "Windows Store Apps",
            "PS4", "PS5", "XboxOne", "Switch"
        };
        private int sourcePlatformIndex = 2;
        private int targetPlatformMask = 0;
        private bool copyIfSourceNotOverridden = false;
        private const string Pref_SourceIdx = "CGP_CopyTex_SourceIdx";
        private const string Pref_TargetMask = "CGP_CopyTex_TargetMask";
        private const string Pref_CopyIfNotOverridden = "CGP_CopyTex_CopyIfNotOverridden";

        private string renameText;

        public GameObject prefabToReplaceWith;

        private Vector2 scrollPosition;

        #endregion

        #region Unity Methods

        private void OnEnable()
        {
            foldersKey = $"{Application.productName}_CGProgramsUtilsFolders";
            gameObjectsKey = $"{Application.productName}_CGProgramsUtilsGameObjects";

            if (EditorPrefs.HasKey(Pref_SourceIdx)) sourcePlatformIndex = EditorPrefs.GetInt(Pref_SourceIdx);
            if (EditorPrefs.HasKey(Pref_TargetMask)) targetPlatformMask = EditorPrefs.GetInt(Pref_TargetMask);
            if (EditorPrefs.HasKey(Pref_CopyIfNotOverridden)) copyIfSourceNotOverridden = EditorPrefs.GetBool(Pref_CopyIfNotOverridden);

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
            if (GUILayout.Button("Export MeshBaker Mesh", GUILayout.Width(position.width * 0.9f)))
                ExportMeshBakerMesh();

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

            GUILayout.Space(10);
            GUILayout.Label("Replace Material", EditorStyles.boldLabel);
            materialToReplace = (Material)EditorGUILayout.ObjectField("Material to Replace", materialToReplace, typeof(Material), false);
            materialToReplaceWith = (Material)EditorGUILayout.ObjectField("Material to Replace With", materialToReplaceWith, typeof(Material), false);

            if (GUILayout.Button("Replace Material in Scene", GUILayout.Width(position.width * 0.9f)))
            {
                ReplaceMaterialInScene(materialToReplace, materialToReplaceWith);
            }

            GUILayout.Space(10);
            GUILayout.Label("Replace Shader in Selection", EditorStyles.boldLabel);
            shaderToReplace = (Shader)EditorGUILayout.ObjectField("Shader to Replace", shaderToReplace, typeof(Shader), false);
            shaderToReplaceWith = (Shader)EditorGUILayout.ObjectField("Shader to Replace With", shaderToReplaceWith, typeof(Shader), false);

            if (GUILayout.Button("Replace Shader in Selection", GUILayout.Width(position.width * 0.9f)))
            {
                ReplaceShaderInSelection(shaderToReplace, shaderToReplaceWith);
            }

            GUILayout.Space(10);
            GUILayout.Label("Copy Texture Import Settings", EditorStyles.boldLabel);
            sourcePlatformIndex = EditorGUILayout.Popup("Copy From", sourcePlatformIndex, texturePlatforms);
            var disabled = new bool[texturePlatforms.Length];
            disabled[sourcePlatformIndex] = true;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Target Platforms (source is disabled below)");
            }
            targetPlatformMask = MaskFieldWithDisabled("Copy To", targetPlatformMask, texturePlatforms, disabled);

            copyIfSourceNotOverridden = EditorGUILayout.Toggle(
                new GUIContent("Copy When Source Not Overridden",
                    "If OFF and source is not overridden, skip. Has no effect when source == Default."),
                copyIfSourceNotOverridden);

            EditorPrefs.SetInt(Pref_SourceIdx, sourcePlatformIndex);
            EditorPrefs.SetInt(Pref_TargetMask, targetPlatformMask);
            EditorPrefs.SetBool(Pref_CopyIfNotOverridden, copyIfSourceNotOverridden);

            if (GUILayout.Button("Execute Copy", GUILayout.Width(position.width * 0.9f)))
            {
                var from = texturePlatforms[sourcePlatformIndex];
                var targets = GetSelectedTargets(texturePlatforms, targetPlatformMask, sourcePlatformIndex);
                CopyPlatformTextureSettings(from, targets, copyIfSourceNotOverridden);
            }

            GUILayout.Space(10);
            GUILayout.Label("Rename Selected Objects", EditorStyles.boldLabel);
            renameText = EditorGUILayout.TextField("New Name", renameText);

            if (GUILayout.Button("Rename Selected Objects", GUILayout.Width(position.width * 0.9f)))
            {
                RenameSelectedObjects(renameText);
            }

            GUILayout.Label("Mass Replace Prefabs", EditorStyles.boldLabel);
            prefabToReplaceWith = (GameObject)EditorGUILayout.ObjectField("New Prefab", prefabToReplaceWith, typeof(GameObject), false);
            if (GUILayout.Button("Replace Selected Prefabs", GUILayout.Width(position.width * 0.9f)))
            {
                MassReplacePrefabs(prefabToReplaceWith);
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
            var meshFilters = FindObjectsOfType<MeshFilter>(true);
            var unnecessaryMeshes = meshFilters.Where(meshFilter =>
            {
                var hasRenderer = meshFilter.GetComponent<MeshRenderer>();
                var hasCollider = meshFilter.GetComponent<Collider>();
                var hasSkinned = meshFilter.GetComponent<SkinnedMeshRenderer>();
                var noChildren = meshFilter.transform.childCount == 0;

                if (hasCollider || hasSkinned || !noChildren) return false;
                if (!hasRenderer && !hasCollider && !hasSkinned) return true;

                var materials = hasRenderer.sharedMaterials;
                return materials == null || materials.Length == 0 || materials.All(m => !m);
            }).ToList();

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

        private static void ExportMeshBakerMesh()
        {
            var go = Selection.activeGameObject;
            if (!go)
            {
                Debug.LogError("No GameObject selected.");
                return;
            }

            Mesh mesh = null;
            var transform = go.transform;

            var mf = go.GetComponent<MeshFilter>();
            if (mf)
            {
                mesh = mf.sharedMesh;
            }
            else
            {
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr)
                {
                    mesh = new Mesh();
                    smr.BakeMesh(mesh);
                }
            }

            if (!mesh)
            {
                Debug.LogError("No mesh found on selected GameObject.");
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export OBJ", "", go.name + ".obj", "obj");
            if (string.IsNullOrEmpty(path)) return;

            using (var sw = new StreamWriter(path))
            {
                sw.Write(MeshToString(mesh, transform));
            }

            Debug.Log("Exported mesh to: " + path);
        }

        private static string MeshToString(Mesh mesh, Transform transform)
        {
            var sb = new StringBuilder();

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;

            foreach (var v in vertices)
            {
                var wv = transform.TransformPoint(v);
                sb.AppendLine($"v {wv.x} {wv.y} {wv.z}");
            }

            foreach (var n in normals)
            {
                var wn = transform.TransformDirection(n);
                sb.AppendLine($"vn {wn.x} {wn.y} {wn.z}");
            }

            foreach (var uv in uvs) sb.AppendLine($"vt {uv.x} {uv.y}");

            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                var tris = mesh.GetTriangles(i);
                for (var j = 0; j < tris.Length; j += 3)
                {
                    var a = tris[j + 0] + 1;
                    var b = tris[j + 1] + 1;
                    var c = tris[j + 2] + 1;
                    sb.AppendLine($"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}");
                }
            }

            return sb.ToString();
        }

        private static void ReplaceMaterialInScene(Material from, Material to)
        {
            if (!from || !to)
            {
                Debug.LogError("Please assign both materials.");
                return;
            }

            var renderers = FindObjectsOfType<MeshRenderer>(true);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != from) continue;
                    materials[i] = to;
                    changed = true;
                }

                if (!changed) continue;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }
            Debug.Log("Material replacement complete.");
        }

        private static void ReplaceShaderInSelection(Shader fromShader, Shader toShader)
        {
            if (!fromShader || !toShader)
            {
                Debug.LogError("Please assign both source and target shaders.");
                return;
            }

            foreach (var obj in Selection.objects)
            {
                if (obj is not Material mat || mat.shader != fromShader) continue;

                mat.shader = toShader;
                EditorUtility.SetDirty(mat);
            }

            foreach (var obj in Selection.gameObjects)
            {
                var renderers = obj.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    var changed = false;
                    foreach (var mat in materials)
                    {
                        if (!mat || mat.shader != fromShader) continue;

                        mat.shader = toShader;
                        changed = true;
                    }

                    if (!changed) continue;

                    renderer.sharedMaterials = materials;
                    EditorUtility.SetDirty(renderer);
                }
            }
            Debug.Log("Shader replacement in selection complete.");
        }

        private static List<string> GetSelectedTargets(string[] all, int mask, int sourceIdx)
        {
            return all.Where((t, i) => i != sourceIdx && (mask & (1 << i)) != 0).ToList();
        }

        private static int MaskFieldWithDisabled(string label, int mask, string[] displayed, bool[] disabled)
        {
            var contents = new GUIContent[displayed.Length];
            for (var i = 0; i < displayed.Length; i++)
            {
                contents[i] = new GUIContent(disabled[i] ? $"{displayed[i]} (source)" : displayed[i]);
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
            for (var i = 0; i < displayed.Length; i++)
            {
                using (new EditorGUI.DisabledScope(disabled[i]))
                {
                    var on = (mask & (1 << i)) != 0;
                    var toggled = EditorGUILayout.ToggleLeft(contents[i], on);
                    if (toggled) mask |= (1 << i);
                    else mask &= ~(1 << i);
                }
            }

            EditorGUILayout.EndVertical();
            return mask;
        }

        private static void CopyPlatformTextureSettings(string sourcePlatform, List<string> targetPlatforms,
            bool copyIfSourceNotOverridden)
        {
            if (targetPlatforms == null || targetPlatforms.Count == 0)
            {
                Debug.LogWarning("No target platforms selected.");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture");
            var total = guids.Length;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (var i = 0; i < total; i++)
                {
                    var guid = guids[i];
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    EditorUtility.DisplayProgressBar($"Copy {sourcePlatform} → targets", path, (float)i / total);

                    if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

                    var src = ti.GetPlatformTextureSettings(sourcePlatform);

                    var isDefault = string.Equals(sourcePlatform, "Default");
                    switch (isDefault)
                    {
                        case false when !src.overridden && !copyIfSourceNotOverridden:
                        case false when src.maxTextureSize == 0 && !copyIfSourceNotOverridden:
                            continue;
                    }

                    foreach (var target in targetPlatforms)
                    {
                        if (string.Equals(target, sourcePlatform)) continue;

                        var dst = ti.GetPlatformTextureSettings(target);

                        dst.name = target;
                        dst.overridden = true;

                        dst.maxTextureSize = src.maxTextureSize > 0 ? src.maxTextureSize : 2048;
                        dst.resizeAlgorithm = src.resizeAlgorithm;
                        dst.compressionQuality = src.compressionQuality;
                        dst.crunchedCompression = src.crunchedCompression;

                        dst.format = src.format;

                        if (TrySetPlatform(ti, dst)) continue;
                        dst.format = TextureImporterFormat.Automatic;
                        TrySetPlatform(ti, dst);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return;

            static bool TrySetPlatform(TextureImporter ti, TextureImporterPlatformSettings ps)
            {
                try
                {
                    ti.SetPlatformTextureSettings(ps);
                    ti.SaveAndReimport();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void RenameSelectedObjects(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            foreach (var obj in Selection.objects)
            {
                if (obj is not GameObject go) continue;
                go.name = newName;
                EditorUtility.SetDirty(go);
            }
        }

        private static void MassReplacePrefabs(GameObject newPrefab)
        {
            if (!newPrefab)
            {
                Debug.LogError("Please assign a prefab to replace with.");
                return;
            }

            var selectedObjects = Selection.gameObjects;
            foreach (var oldObj in selectedObjects)
            {
                var parent = oldObj.transform.parent;
                var siblingIndex = oldObj.transform.GetSiblingIndex();

                var newObj = (GameObject)PrefabUtility.InstantiatePrefab(newPrefab, oldObj.scene);

                newObj.transform.SetParent(parent, false);
                newObj.transform.localPosition = oldObj.transform.localPosition;
                newObj.transform.localRotation = oldObj.transform.localRotation;
                newObj.transform.localScale = oldObj.transform.localScale;
                newObj.transform.SetSiblingIndex(siblingIndex);

                var oldComponents = oldObj.GetComponents<MonoBehaviour>();
                var newComponents = newObj.GetComponents<MonoBehaviour>();
                foreach (var oldComp in oldComponents)
                {
                    var type = oldComp.GetType();
                    var newComp = newComponents.FirstOrDefault(c => c.GetType() == type);
                    if (!newComp) continue;

                    var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (field.IsDefined(typeof(SerializeField), true) || field.IsPublic)
                        {
                            field.SetValue(newComp, field.GetValue(oldComp));
                        }
                    }
                }

                var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
                foreach (var behaviour in allBehaviours)
                {
                    var type = behaviour.GetType();
                    var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    foreach (var field in fields)
                    {
                        if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;

                        var value = field.GetValue(behaviour);
                        if (value == null) continue;

                        if (ReferenceEquals(value, oldObj))
                        {
                            field.SetValue(behaviour, newObj);
                            EditorUtility.SetDirty(behaviour);
                        }
                        else if (value is Component oldComp)
                        {
                            if (oldComp.gameObject != oldObj) continue;

                            var newComp = newObj.GetComponent(oldComp.GetType());
                            if (!newComp) continue;

                            field.SetValue(behaviour, newComp);
                            EditorUtility.SetDirty(behaviour);
                        }
                    }
                }

                Undo.RegisterCreatedObjectUndo(newObj, "Replace Prefab");
                Undo.DestroyObjectImmediate(oldObj);
            }
            Debug.Log("Mass prefab replacement complete.");
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


#endif
