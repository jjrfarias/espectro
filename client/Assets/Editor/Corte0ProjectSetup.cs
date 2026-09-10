#if UNITY_EDITOR
using System.IO;
using Espectro.Prototype;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Espectro.Editor
{
    [InitializeOnLoad]
    public static class Corte0ProjectSetup
    {
        private const string SceneDirectory = "Assets/Scenes";
        private const string ScenePath = SceneDirectory + "/Corte0.unity";
        private const string SettingsDirectory = "Assets/Settings";
        private const string PipelinePath = SettingsDirectory + "/MobileURP.asset";
        private const string RendererPath = SettingsDirectory + "/MobileRenderer.asset";

        static Corte0ProjectSetup()
        {
            EditorApplication.delayCall += EnsureProject;
        }

        [MenuItem("Espectro/Recriar Cena do Corte 0")]
        public static void RebuildScene()
        {
            BuildScene(true);
        }

        public static void BuildAndroid()
        {
            ConfigurePlayer();
            ConfigureRenderPipeline();
            if (!File.Exists(ScenePath))
            {
                BuildScene(false);
            }

            const string outputDirectory = "Builds/Android";
            const string outputPath = outputDirectory + "/Espectro-Corte0.apk";
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Build Android falhou: {report.summary.result}, {report.summary.totalErrors} erro(s).");
            }

            Debug.Log($"APK criado em {outputPath} ({report.summary.totalSize} bytes).");
        }

        // Alvo de teste rápido em paralelo ao Android (ver README raiz): mesmo projeto, sem
        // recriar nada. Exige o módulo "WebGL Build Support" instalado pelo Unity Hub — sem ele
        // o BuildPipeline falha com uma mensagem clara.
        [MenuItem("Espectro/Build WebGL (teste)")]
        public static void BuildWebGL()
        {
            ConfigurePlayer();
            ConfigureRenderPipeline();
            if (!File.Exists(ScenePath))
            {
                BuildScene(false);
            }

            const string outputDirectory = "Builds/WebGL";
            Directory.CreateDirectory(outputDirectory);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputDirectory,
                target = BuildTarget.WebGL,
                options = BuildOptions.Development
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Build WebGL falhou: {report.summary.result}, {report.summary.totalErrors} erro(s). " +
                    "Confira se o módulo WebGL Build Support está instalado no Unity Hub.");
            }

            Debug.Log(
                $"Build WebGL criado em {outputDirectory} ({report.summary.totalSize} bytes). " +
                "Sirva a pasta com um servidor HTTP local (ex.: 'npx serve Builds/WebGL') — abrir o index.html direto do disco não funciona por causa das políticas de CORS/COOP do navegador.");
        }

        private static void EnsureProject()
        {
            ConfigurePlayer();
            if (!File.Exists(ScenePath))
            {
                BuildScene(false);
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Espectro Studio";
            PlayerSettings.productName = "Espectro";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.espectro.prototype");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            // Necessário para o cliente de rede (Espectro.Network): sem isso a APK pode sair
            // sem a permissão INTERNET e toda chamada HTTP/WebSocket falha silenciosamente no aparelho.
            PlayerSettings.Android.forceInternetPermission = true;
        }

        private static void ConfigureRenderPipeline()
        {
            Directory.CreateDirectory(SettingsDirectory);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "Mobile Renderer";
                AssetDatabase.CreateAsset(renderer, RendererPath);

                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Mobile URP";
                pipeline.supportsHDR = false;
                pipeline.msaaSampleCount = 1;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.shadowDistance = 25f;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        private static void BuildScene(bool force)
        {
            if (!force && File.Exists(ScenePath))
            {
                return;
            }

            Directory.CreateDirectory(SceneDirectory);
            ConfigureRenderPipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.68f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.68f, 0.72f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 32f;
            RenderSettings.fogEndDistance = 75f;
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;

            CreateLight();
            CreateEnvironment();
            var player = CreatePlayer();
            CreateInteractions();
            CreateCamera(player.transform);
            CreateInterface(player);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = player.gameObject;
            Debug.Log("Corte 0 criado. Pressione Play ou gere uma build Android.");
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Sol");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.94f, 0.82f);
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        }

        private static void CreateEnvironment()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Terreno do Berco";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            SetColor(ground, new Color(0.24f, 0.42f, 0.25f));

            CreateBlock("Praca", new Vector3(0f, 0.08f, 3f), new Vector3(12f, 0.16f, 11f), new Color(0.55f, 0.48f, 0.36f));
            CreateBlock("Estrada Norte", new Vector3(0f, 0.09f, 15f), new Vector3(4f, 0.18f, 18f), new Color(0.45f, 0.37f, 0.27f));
            CreateBlock("Estrada Mina", new Vector3(12f, 0.1f, -5f), new Vector3(21f, 0.18f, 3f), new Color(0.43f, 0.36f, 0.27f));

            CreateHouse("Casa do Conselho", new Vector3(0f, 0f, 12f), new Vector3(5f, 3.4f, 4f), new Color(0.7f, 0.58f, 0.39f));
            CreateHouse("Casa Oeste", new Vector3(-7f, 0f, 7f), new Vector3(4f, 2.8f, 3.5f), new Color(0.63f, 0.45f, 0.3f));
            CreateHouse("Estalagem", new Vector3(7f, 0f, 7f), new Vector3(5f, 3.1f, 4f), new Color(0.68f, 0.5f, 0.32f));
            CreateForge(new Vector3(-8f, 0f, -2f));
            CreateMine(new Vector3(22f, 0f, -5f));

            var treePositions = new[] { new Vector3(-16,0,10), new Vector3(-19,0,4), new Vector3(-15,0,-3), new Vector3(-18,0,-10), new Vector3(-11,0,-13), new Vector3(12,0,14), new Vector3(17,0,10), new Vector3(19,0,18) };
            for (var i = 0; i < treePositions.Length; i++) CreateTree("Arvore " + (i + 1), treePositions[i], 0.85f + (i % 3) * 0.13f);

            for (var i = 0; i < 8; i++)
            {
                var position = new Vector3(16f + (i % 3) * 4f, 0.7f + i * 0.18f, -14f + (i / 3) * 5f);
                var rock = CreatePrimitive("Rocha " + (i + 1), PrimitiveType.Sphere, position, new Vector3(3.2f, 1.7f + i * 0.15f, 2.5f), new Color(0.34f, 0.35f, 0.36f));
                rock.transform.rotation = Quaternion.Euler(0f, i * 31f, i * 7f);
            }
        }

        private static void CreateHouse(string name, Vector3 position, Vector3 size, Color color)
        {
            var root = new GameObject(name);
            CreatePrimitive("Paredes", PrimitiveType.Cube, position + Vector3.up * size.y * 0.5f, size, color, root.transform);
            var roof = CreatePrimitive("Telhado", PrimitiveType.Cube, position + Vector3.up * (size.y + 0.55f), new Vector3(size.x * 0.8f, 1.1f, size.z * 0.8f), new Color(0.32f, 0.14f, 0.08f), root.transform);
            roof.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            CreatePrimitive("Porta", PrimitiveType.Cube, position + new Vector3(0f, 1f, -size.z * 0.51f), new Vector3(1f, 2f, 0.12f), new Color(0.22f, 0.12f, 0.06f), root.transform);
        }

        private static void CreateForge(Vector3 position)
        {
            CreateHouse("Forja", position, new Vector3(5f, 2.6f, 4f), new Color(0.4f, 0.34f, 0.3f));
            CreatePrimitive("Chamime da Forja", PrimitiveType.Cube, position + new Vector3(1.4f, 3.7f, 0.8f), new Vector3(0.8f, 3.4f, 0.8f), new Color(0.2f, 0.18f, 0.17f));
            CreatePrimitive("Braseiro", PrimitiveType.Cylinder, position + new Vector3(2.8f, 0.45f, -1.8f), new Vector3(1.1f, 0.45f, 1.1f), new Color(1f, 0.3f, 0.05f));
        }

        private static void CreateMine(Vector3 position)
        {
            CreatePrimitive("Montanha da Mina", PrimitiveType.Sphere, position + new Vector3(0f, 3f, 0f), new Vector3(11f, 7f, 9f), new Color(0.31f, 0.32f, 0.34f));
            CreatePrimitive("Entrada Escura", PrimitiveType.Cube, position + new Vector3(0f, 1.7f, -4.15f), new Vector3(3.4f, 3.4f, 0.3f), new Color(0.04f, 0.04f, 0.045f));
            CreatePrimitive("Pilar Esquerdo", PrimitiveType.Cube, position + new Vector3(-2f, 1.8f, -4.4f), new Vector3(0.55f, 3.6f, 0.55f), new Color(0.25f, 0.13f, 0.06f));
            CreatePrimitive("Pilar Direito", PrimitiveType.Cube, position + new Vector3(2f, 1.8f, -4.4f), new Vector3(0.55f, 3.6f, 0.55f), new Color(0.25f, 0.13f, 0.06f));
            CreatePrimitive("Viga", PrimitiveType.Cube, position + new Vector3(0f, 3.5f, -4.4f), new Vector3(4.5f, 0.55f, 0.55f), new Color(0.25f, 0.13f, 0.06f));
        }

        private static void CreateInteractions()
        {
            var npc = new GameObject("Ancia Mira");
            npc.transform.position = new Vector3(2.5f, 0f, 2.8f);
            CreatePrimitive("Corpo", PrimitiveType.Capsule, npc.transform.position + Vector3.up, new Vector3(0.65f, 1f, 0.65f), new Color(0.48f, 0.2f, 0.58f), npc.transform);
            CreatePrimitive("Cabeca", PrimitiveType.Sphere, npc.transform.position + Vector3.up * 2.1f, Vector3.one * 0.62f, new Color(0.75f, 0.55f, 0.4f), npc.transform);
            CreatePrimitive("Indicador", PrimitiveType.Sphere, npc.transform.position + Vector3.up * 3f, Vector3.one * 0.28f, new Color(1f, 0.82f, 0.12f), npc.transform);
            npc.AddComponent<WorldInteractable>().Configure("Ancia Mira", "O Berco desperta com voce. Siga a estrada a leste e investigue a mina. Ha algo se movendo sob as pedras.");

            var caveRoot = new GameObject("Interior da Mina");
            var caveCenter = new Vector3(0f, 0f, -40f);
            CreatePrimitive("Piso da Mina", PrimitiveType.Cube, caveCenter, new Vector3(14f, 0.3f, 18f), new Color(0.16f, 0.14f, 0.12f), caveRoot.transform);
            CreatePrimitive("Parede Fundo", PrimitiveType.Cube, caveCenter + new Vector3(0f, 3f, -9f), new Vector3(14f, 6f, 1f), new Color(0.24f, 0.23f, 0.22f), caveRoot.transform);
            CreatePrimitive("Parede Esquerda", PrimitiveType.Cube, caveCenter + new Vector3(-7f, 3f, 0f), new Vector3(1f, 6f, 18f), new Color(0.24f, 0.23f, 0.22f), caveRoot.transform);
            CreatePrimitive("Parede Direita", PrimitiveType.Cube, caveCenter + new Vector3(7f, 3f, 0f), new Vector3(1f, 6f, 18f), new Color(0.24f, 0.23f, 0.22f), caveRoot.transform);
            for (var i = 0; i < 5; i++)
                CreatePrimitive("Cristal " + (i + 1), PrimitiveType.Cube, caveCenter + new Vector3(-4f + i * 2f, 0.8f, -5f + (i % 2) * 3f), new Vector3(0.45f, 1.6f, 0.45f), new Color(0.15f, 0.75f, 0.95f), caveRoot.transform).transform.rotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -18f : 18f);

            var insidePoint = new GameObject("Destino Interior").transform;
            insidePoint.position = caveCenter + new Vector3(0f, 0.3f, 5f);
            insidePoint.rotation = Quaternion.Euler(0f, 180f, 0f);
            var outsidePoint = new GameObject("Destino Vila").transform;
            outsidePoint.position = new Vector3(18f, 0.25f, -5f);
            outsidePoint.rotation = Quaternion.Euler(0f, -90f, 0f);

            var mineEntrance = new GameObject("Interacao Entrada Mina");
            mineEntrance.transform.position = new Vector3(22f, 0.5f, -10.2f);
            mineEntrance.AddComponent<WorldInteractable>().Configure("Entrada da Mina", "Voce entrou na Mina Silenciosa. Os cristais azuis pulsam como se estivessem vivos.", insidePoint);
            var mineExit = new GameObject("Interacao Saida Mina");
            mineExit.transform.position = caveCenter + new Vector3(0f, 0.5f, 7f);
            mineExit.AddComponent<WorldInteractable>().Configure("Saida da Mina", "Voce retornou ao Berco. A Ancia precisa saber o que encontrou.", outsidePoint);
        }

        private static void CreateTree(string name, Vector3 position, float scale)
        {
            var root = new GameObject(name);
            CreatePrimitive("Tronco", PrimitiveType.Cylinder, position + Vector3.up * 1.6f * scale, new Vector3(0.55f, 1.6f, 0.55f) * scale, new Color(0.3f, 0.17f, 0.08f), root.transform);
            CreatePrimitive("Copa", PrimitiveType.Sphere, position + Vector3.up * 4f * scale, new Vector3(3.1f, 3.4f, 3.1f) * scale, new Color(0.12f, 0.38f, 0.16f), root.transform);
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color, Transform parent = null)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.transform.localScale = scale;
            SetColor(item, color);
            return item;
        }

        private static void CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetPositionAndRotation(position, Quaternion.identity);
            block.transform.localScale = scale;
            SetColor(block, color);
        }

        private static PrototypePlayerController CreatePlayer()
        {
            var playerObject = new GameObject("Jogador");
            playerObject.name = "Jogador";
            playerObject.transform.position = new Vector3(0f, 0.05f, 0f);
            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 2f;
            characterController.radius = 0.38f;
            characterController.center = new Vector3(0f, 1f, 0f);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(playerObject.transform, false);
            CreatePrimitive("Corpo", PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(0.78f, 0.95f, 0.42f), new Color(0.16f, 0.44f, 0.78f), visual);
            CreatePrimitive("Cabeca", PrimitiveType.Sphere, new Vector3(0f, 2.08f, 0f), Vector3.one * 0.58f, new Color(0.82f, 0.63f, 0.45f), visual);
            var leftArm = CreateLimb("Braco Esquerdo", new Vector3(-0.55f, 1.35f, 0f), visual, new Color(0.82f, 0.63f, 0.45f));
            var rightArm = CreateLimb("Braco Direito", new Vector3(0.55f, 1.35f, 0f), visual, new Color(0.82f, 0.63f, 0.45f));
            var leftLeg = CreateLimb("Perna Esquerda", new Vector3(-0.22f, 0.65f, 0f), visual, new Color(0.18f, 0.2f, 0.25f));
            var rightLeg = CreateLimb("Perna Direita", new Vector3(0.22f, 0.65f, 0f), visual, new Color(0.18f, 0.2f, 0.25f));
            var animator = playerObject.AddComponent<ProceduralCharacterAnimator>();
            animator.Configure(visual, leftArm, rightArm, leftLeg, rightLeg);
            return playerObject.AddComponent<PrototypePlayerController>();
        }

        private static Transform CreateLimb(string name, Vector3 position, Transform parent, Color color)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.position = position;
            CreatePrimitive("Forma", PrimitiveType.Capsule, position + Vector3.down * 0.32f, new Vector3(0.27f, 0.45f, 0.27f), color, pivot);
            return pivot;
        }

        private static void CreateCamera(Transform target)
        {
            var cameraObject = new GameObject("Camera Principal");
            cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<ThirdPersonCamera>().SetTarget(target);
        }

        private static void CreateInterface(PrototypePlayerController player)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();

            var canvasObject = new GameObject("Interface");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var joystickArea = CreatePanel(canvasObject.transform, "Joystick", new Vector2(230f, 230f), new Vector2(170f, 170f), new Color(0f, 0f, 0f, 0.25f));
            joystickArea.anchorMin = Vector2.zero;
            joystickArea.anchorMax = Vector2.zero;
            joystickArea.pivot = new Vector2(0.5f, 0.5f);

            var handle = CreatePanel(joystickArea, "Alavanca", new Vector2(100f, 100f), Vector2.zero, new Color(1f, 1f, 1f, 0.55f));
            handle.anchorMin = new Vector2(0.5f, 0.5f);
            handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            joystickArea.gameObject.AddComponent<VirtualJoystick>().Configure(handle, player);

            var actionRect = CreatePanel(canvasObject.transform, "Botao Interagir", new Vector2(270f, 140f), new Vector2(-180f, -170f), new Color(0.1f, 0.22f, 0.3f, 0.88f));
            actionRect.anchorMin = Vector2.one;
            actionRect.anchorMax = Vector2.one;
            actionRect.pivot = new Vector2(0.5f, 0.5f);
            var actionButton = actionRect.gameObject.AddComponent<Button>();
            var actionLabel = CreateText(actionRect, "Texto Acao", "INTERAGIR", 27, TextAnchor.MiddleCenter, Color.white);
            actionLabel.rectTransform.anchorMin = Vector2.zero;
            actionLabel.rectTransform.anchorMax = Vector2.one;
            actionLabel.rectTransform.offsetMin = Vector2.zero;
            actionLabel.rectTransform.offsetMax = Vector2.zero;

            var dialogueRect = CreatePanel(canvasObject.transform, "Dialogo", new Vector2(1100f, 230f), new Vector2(0f, 145f), new Color(0.035f, 0.045f, 0.06f, 0.94f));
            dialogueRect.anchorMin = new Vector2(0.5f, 0f);
            dialogueRect.anchorMax = new Vector2(0.5f, 0f);
            dialogueRect.pivot = new Vector2(0.5f, 0.5f);
            var speaker = CreateText(dialogueRect, "Nome", "", 31, TextAnchor.UpperLeft, new Color(1f, 0.82f, 0.25f));
            speaker.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            speaker.rectTransform.sizeDelta = new Vector2(-60f, 55f);
            var body = CreateText(dialogueRect, "Mensagem", "", 27, TextAnchor.UpperLeft, Color.white);
            body.rectTransform.anchoredPosition = new Vector2(0f, -75f);
            body.rectTransform.sizeDelta = new Vector2(-60f, 130f);

            var objective = CreateText(canvasObject.transform, "Objetivo", "OBJETIVO: Fale com a Ancia Mira na praca", 25, TextAnchor.UpperLeft, Color.white);
            objective.rectTransform.anchorMin = new Vector2(0f, 1f);
            objective.rectTransform.anchorMax = new Vector2(0f, 1f);
            objective.rectTransform.pivot = new Vector2(0f, 1f);
            objective.rectTransform.anchoredPosition = new Vector2(30f, -30f);
            objective.rectTransform.sizeDelta = new Vector2(750f, 70f);

            var interaction = canvasObject.AddComponent<InteractionController>();
            interaction.Configure(player, actionButton, actionLabel, dialogueRect.gameObject, speaker, body, objective);
            actionRect.gameObject.AddComponent<InteractionButton>().Configure(interaction);
            UnityEventTools.AddPersistentListener(actionButton.onClick, interaction.Interact);
            EditorUtility.SetDirty(actionButton);
            EditorUtility.SetDirty(interaction);

            var hudObject = new GameObject("Performance", typeof(RectTransform), typeof(Text), typeof(PerformanceHud));
            hudObject.transform.SetParent(canvasObject.transform, false);
            var hudRect = (RectTransform)hudObject.transform;
            hudRect.anchorMin = new Vector2(1f, 1f);
            hudRect.anchorMax = new Vector2(1f, 1f);
            hudRect.pivot = new Vector2(1f, 1f);
            hudRect.anchoredPosition = new Vector2(-30f, -30f);
            hudRect.sizeDelta = new Vector2(500f, 100f);
            var text = hudObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.UpperRight;
            text.color = Color.white;
            hudObject.GetComponent<PerformanceHud>().Configure(text);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Vector2 size, Vector2 position, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            panel.GetComponent<Image>().color = color;
            return rect;
        }

        private static Text CreateText(Transform parent, string name, string value, int fontSize, TextAnchor alignment, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            return text;
        }

        private static void SetColor(GameObject target, Color color)
        {
            var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            target.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
#endif
