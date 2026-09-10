using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Espectro.Editor
{
    public static class EspectroVisualLabBuilder
    {
        private const string VillageRoot = "Assets/Resources/EspectroModels/VillageOBJ/";
        private const string NatureRoot = "Assets/Resources/EspectroModels/Nature/";
        private const string ScenePath = "Assets/Scenes/VisualLab.unity";
        private const string PrefabPath = "Assets/Prefabs/Visual/CasaEspectroPrototype.prefab";

        [MenuItem("Espectro/Visual/Criar Laboratorio Visual")]
        public static void CreateVisualLab()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Espectro", "Saia do Play antes de criar o laboratorio visual.", "OK");
                return;
            }

            Directory.CreateDirectory("Assets/Prefabs/Visual");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "VisualLab";

            ConfigureEnvironment();
            CreateGround();
            var house = BuildHouse();
            RefineHouseMaterials(house);
            AddHouseCollider(house);
            CreateCamera(house.transform.position);
            CreateLight();

            PrefabUtility.SaveAsPrefabAssetAndConnect(house, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = house;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[Espectro] Laboratorio visual criado em " + ScenePath);
        }

        [MenuItem("Espectro/Visual/Aplicar Casa Oeste no Corte 0")]
        public static void ApplyWestHouseToCorte0()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Espectro", "Saia do Play antes de aplicar a casa.", "OK");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("Espectro", "Crie o laboratorio visual primeiro.", "OK");
                return;
            }

            Directory.CreateDirectory("Assets/Scenes/Backups");
            const string backup = "Assets/Scenes/Backups/Corte0_antes_casa_oeste.unity";
            if (!File.Exists(backup)) FileUtil.CopyFileOrDirectory("Assets/Scenes/Corte0.unity", backup);

            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corte0.unity", OpenSceneMode.Single);
            var original = GameObject.Find("Casa Oeste");
            if (original == null) throw new MissingReferenceException("Casa Oeste nao encontrada no Corte 0.");
            var oldRenderers = original.GetComponentsInChildren<Renderer>(true);
            var oldBounds = CombineBounds(oldRenderers, original.transform.position);
            foreach (var renderer in oldRenderers) renderer.enabled = false;

            var existing = GameObject.Find("Casa Oeste - Visual Espectro");
            if (existing != null) Object.DestroyImmediate(existing);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "Casa Oeste - Visual Espectro";
            var newBounds = CalculateBounds(instance);
            var playerController = Object.FindAnyObjectByType<CharacterController>();
            var playerHeight = playerController != null ? playerController.height : 2f;
            // A referencia de escala e humana: o prefab foi criado para um personagem de 2 m.
            // Uma pequena folga mantem portas e beirais confortaveis para gameplay.
            var uniformScale = playerHeight / 2f * 1.08f;
            instance.transform.localScale = Vector3.one * uniformScale;
            newBounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(
                oldBounds.center.x - newBounds.center.x,
                oldBounds.min.y - newBounds.min.y,
                oldBounds.center.z - newBounds.center.z);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("[Espectro] Casa Oeste substituida. Backup: " + backup);
        }

        private static GameObject BuildHouse()
        {
            var house = new GameObject("Casa Espectro - Prototipo");
            var wallTop = 0f;
            // Os modulos medem 2 m de largura por aproximadamente 3,12 m de altura.
            // Manter a altura nativa preserva o encaixe exato na grade de 2 m.
            const float wallHeight = 3.123f;

            for (var column = -1; column <= 1; column++)
            {
                var frontAsset = column == 0 ? "Wall_Plaster_Door_Round.obj" : "Wall_Plaster_Window_Wide_Round.obj";
                var front = PlaceNormalized(frontAsset, new Vector3(column * 2f, 0f, -3f), Quaternion.identity, house.transform, wallHeight);
                var back = PlaceNormalized("Wall_Plaster_Straight.obj", new Vector3(column * 2f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f), house.transform, wallHeight);
                wallTop = Mathf.Max(wallTop, CalculateBounds(front).max.y, CalculateBounds(back).max.y);
            }

            for (var column = -1; column <= 1; column++)
            {
                var asset = column == 0 ? "Wall_Plaster_Window_Wide_Round.obj" : "Wall_Plaster_Straight.obj";
                var left = PlaceNormalized(asset, new Vector3(-3f, 0f, column * 2f), Quaternion.Euler(0f, 90f, 0f), house.transform, wallHeight);
                var right = PlaceNormalized(asset, new Vector3(3f, 0f, column * 2f), Quaternion.Euler(0f, -90f, 0f), house.transform, wallHeight);
                wallTop = Mathf.Max(wallTop, CalculateBounds(left).max.y, CalculateBounds(right).max.y);
            }

            PlaceNormalized("Corner_ExteriorWide_Brick.obj", new Vector3(-3f, 0f, -3f), Quaternion.identity, house.transform, wallHeight);
            PlaceNormalized("Corner_ExteriorWide_Brick.obj", new Vector3(3f, 0f, -3f), Quaternion.Euler(0f, -90f, 0f), house.transform, wallHeight);
            PlaceNormalized("Corner_ExteriorWide_Brick.obj", new Vector3(3f, 0f, 3f), Quaternion.Euler(0f, 180f, 0f), house.transform, wallHeight);
            PlaceNormalized("Corner_ExteriorWide_Brick.obj", new Vector3(-3f, 0f, 3f), Quaternion.Euler(0f, 90f, 0f), house.transform, wallHeight);

            PlaceNormalized("Door_1_Round.obj", new Vector3(0f, 0f, -3.08f), Quaternion.identity, house.transform, 2.15f);
            PlaceNormalizedByWidth("Roof_Front_Brick6.obj", new Vector3(0f, wallTop - 0.08f, -3f), Quaternion.identity, house.transform, 6.15f);
            PlaceNormalizedByWidth("Roof_Front_Brick6.obj", new Vector3(0f, wallTop - 0.08f, 3f), Quaternion.Euler(0f, 180f, 0f), house.transform, 6.15f);
            PlaceNormalizedByWidth("Roof_FrontSupports.obj", new Vector3(0f, wallTop - 0.12f, -3.08f), Quaternion.identity, house.transform, 6.05f);
            PlaceNormalizedByWidth("Roof_RoundTiles_4x6.obj", new Vector3(0f, wallTop - 0.04f, 0f), Quaternion.identity, house.transform, 6.4f);
            PlaceNormalized("Prop_Chimney.obj", new Vector3(1.65f, wallTop + 0.15f, 0.65f), Quaternion.identity, house.transform, 1.8f);
            for (var x = 0; x < 4; x++)
                for (var z = 0; z < 4; z++)
                    PlaceNative("Floor_UnevenBrick.obj", new Vector3(-3f + x * 2f, 0.015f, -3f + z * 2f), Quaternion.identity, house.transform);

            return house;
        }

        private static void RefineHouseMaterials(GameObject house)
        {
            Directory.CreateDirectory("Assets/Materials/VisualLab");
            AssetDatabase.Refresh();
            foreach (var renderer in house.GetComponentsInChildren<Renderer>())
            {
                var refined = new Material[renderer.sharedMaterials.Length];
                for (var i = 0; i < refined.Length; i++)
                {
                    var source = renderer.sharedMaterials[i];
                    if (source == null) continue;
                    var safeName = string.Join("_", source.name.Split(Path.GetInvalidFileNameChars()));
                    var materialPath = "Assets/Materials/VisualLab/" + safeName + "_Espectro.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(source) { name = source.name + " Espectro" };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.CopyPropertiesFromMaterial(source);
                    var lowerName = source.name.ToLowerInvariant();
                    if (lowerName.Contains("round") || lowerName.Contains("tile"))
                        material.color *= new Color(0.72f, 0.58f, 0.48f, 1f);
                    else if (lowerName.Contains("plaster"))
                        material.color *= new Color(0.9f, 0.82f, 0.67f, 1f);
                    else if (lowerName.Contains("wood"))
                        material.color *= new Color(0.7f, 0.54f, 0.4f, 1f);
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
                    EditorUtility.SetDirty(material);
                    refined[i] = material;
                }
                renderer.sharedMaterials = refined;
            }
        }

        private static void AddHouseCollider(GameObject house)
        {
            foreach (var collider in house.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var colliderBox = house.AddComponent<BoxCollider>();
            colliderBox.center = new Vector3(0f, 1.45f, 0f);
            colliderBox.size = new Vector3(5.9f, 2.9f, 5.9f);
        }

        private static Bounds CombineBounds(Renderer[] renderers, Vector3 fallback)
        {
            if (renderers.Length == 0) return new Bounds(fallback, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static GameObject PlaceNormalized(string fileName, Vector3 groundCenter, Quaternion rotation, Transform parent, float targetHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageRoot + fileName);
            if (prefab == null) throw new FileNotFoundException("Modulo visual nao importado", VillageRoot + fileName);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(Vector3.zero, rotation);
            var bounds = CalculateBounds(instance);
            var scale = bounds.size.y > 0.001f ? targetHeight / bounds.size.y : 1f;
            instance.transform.localScale = Vector3.one * scale;
            bounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(
                groundCenter.x - bounds.center.x,
                groundCenter.y - bounds.min.y,
                groundCenter.z - bounds.center.z);
            return instance;
        }

        private static GameObject PlaceNormalizedByWidth(string fileName, Vector3 groundCenter, Quaternion rotation, Transform parent, float targetWidth)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageRoot + fileName);
            if (prefab == null) throw new FileNotFoundException("Modulo visual nao importado", VillageRoot + fileName);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(Vector3.zero, rotation);
            var bounds = CalculateBounds(instance);
            var scale = bounds.size.x > 0.001f ? targetWidth / bounds.size.x : 1f;
            instance.transform.localScale = Vector3.one * scale;
            bounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(groundCenter.x - bounds.center.x, groundCenter.y - bounds.min.y, groundCenter.z - bounds.center.z);
            return instance;
        }

        private static GameObject PlaceNative(string fileName, Vector3 groundCenter, Quaternion rotation, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VillageRoot + fileName);
            if (prefab == null) throw new FileNotFoundException("Modulo visual nao importado", VillageRoot + fileName);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(Vector3.zero, rotation);
            var bounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(groundCenter.x - bounds.center.x, groundCenter.y - bounds.min.y, groundCenter.z - bounds.center.z);
            return instance;
        }

        private static void AddNature()
        {
            PlaceNature("NormalTree_1.fbx", new Vector3(-6.8f, 0f, 1.8f), 28f, 1f);
            PlaceNature("NormalTree_2.fbx", new Vector3(7.2f, 0f, 2.6f), -35f, 0.9f);
            PlaceNature("Rock_2.fbx", new Vector3(-5.2f, 0f, -4.5f), 16f, 1.2f);
            PlaceNature("Rock_4.fbx", new Vector3(5.4f, 0f, -4.2f), -22f, 0.85f);
            PlaceNature("Bush.fbx", new Vector3(-3.8f, 0f, -3.7f), 40f, 0.8f);
            PlaceNature("Bush_Large.fbx", new Vector3(3.9f, 0f, -3.6f), -20f, 0.75f);
        }

        private static void PlaceNature(string fileName, Vector3 groundCenter, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NatureRoot + fileName);
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            var naturalBounds = CalculateBounds(instance);
            var desiredHeight = fileName.Contains("Tree") ? 5.2f * scale : 1.1f * scale;
            var normalizedScale = naturalBounds.size.y > 0.001f ? desiredHeight / naturalBounds.size.y : 1f;
            instance.transform.localScale = Vector3.one * normalizedScale;
            var bounds = CalculateBounds(instance);
            instance.transform.position += new Vector3(groundCenter.x - bounds.center.x, -bounds.min.y, groundCenter.z - bounds.center.z);
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void CreateGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Terreno de Avaliacao";
            ground.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            ground.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = new Color(0.19f, 0.42f, 0.2f) };
        }

        private static void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.6f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.64f, 0.46f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.22f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.57f, 0.64f, 0.55f);
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 55f;
        }

        private static void CreateLight()
        {
            var sun = new GameObject("Sol do Laboratorio").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.72f, 0.48f);
            sun.intensity = 1.12f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
        }

        private static void CreateCamera(Vector3 target)
        {
            var cameraObject = new GameObject("Camera do Laboratorio");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 52f;
            cameraObject.transform.position = target + new Vector3(10.5f, 6.8f, -12.5f);
            cameraObject.transform.LookAt(target + Vector3.up * 1.7f);
            cameraObject.AddComponent<AudioListener>();
        }
    }
}
