#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Espectro.Editor
{
    // Monta, uma única vez, o personagem real a partir do pacote gratuito KayKit Adventurers
    // (Assets/ThirdParty/KayKit) e salva como prefab em Resources para o runtime só instanciar
    // (AdventurerCharacterBootstrap.cs). Mesmo padrão já usado para o pipeline URP: gerar e
    // persistir o asset uma vez, e o runtime carrega via Resources.Load em qualquer plataforma.
    [InitializeOnLoad]
    public static class AdventurerCharacterSetup
    {
        private const string CharactersDir = "Assets/ThirdParty/KayKit/Adventurers/Characters";
        private const string EquipmentDir = "Assets/ThirdParty/KayKit/Adventurers/Equipment";
        private const string AnimationsDir = "Assets/ThirdParty/KayKit/Adventurers/Animations/Rig_Medium";
        private const string GeneralFbx = AnimationsDir + "/Rig_Medium_General.fbx";
        private const string MovementFbx = AnimationsDir + "/Rig_Medium_MovementBasic.fbx";

        private const string OutputDir = "Assets/Resources/EspectroModels/Adventurers";
        private const string ClipsDir = OutputDir + "/Animations";
        private const string MaterialsDir = OutputDir + "/Materials";
        private const string ControllerPath = OutputDir + "/AdventurerLocomotion.controller";
        private const string PrefabPath = OutputDir + "/Aventureiro.prefab";
        private const string NpcPrefabPath = OutputDir + "/Vila_NPC.prefab";

        // Um pouco menor que os 2 m do CharacterController, pra ficar proporcional às casas
        // (ajustado a pedido: o personagem estava grande demais perto da Casa Oeste).
        private const float TargetHeightUnits = 1.65f;

        static AdventurerCharacterSetup()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        // Chamável via -executeMethod para rodar imediatamente em sessões batchmode curtas, onde
        // delayCall pode não ter tempo de disparar antes do -quit.
        public static void RunSetup() => EnsureAssets();

        private static void EnsureAssets()
        {
            var hasPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            var hasNpcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath) != null;
            if (hasPlayerPrefab && hasNpcPrefab) return;
            if (!File.Exists(CharactersDir + "/Knight.fbx") || !File.Exists(GeneralFbx) || !File.Exists(MovementFbx))
            {
                return; // Pacote KayKit ainda não importado neste projeto.
            }

            Directory.CreateDirectory(ClipsDir);

            var controller = EnsureAnimatorController();
            if (controller == null) return;

            if (!hasPlayerPrefab) BuildAdventurerPrefab(controller);
            if (!hasNpcPrefab) BuildNpcPrefab(controller);
        }

        private static RuntimeAnimatorController EnsureAnimatorController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (existing != null) return existing;

            var idle = CreateLoopingClip(GeneralFbx, "Idle_A", "Idle_A_Loop");
            var walk = CreateLoopingClip(MovementFbx, "Walking_B", "Walking_B_Loop");
            var run = CreateLoopingClip(MovementFbx, "Running_B", "Running_B_Loop");
            if (idle == null || walk == null || run == null)
            {
                Debug.LogWarning("[AdventurerCharacterSetup] Clipes de locomoção do KayKit não encontrados (Idle_A/Walking_B/Running_B); controller não foi criado.");
                return null;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var blendTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(blendTree, ControllerPath);
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, 2.5f);
            blendTree.AddChild(run, 6.3f); // moveSpeed(4.5) * sprintMultiplier(1.4), ver PrototypePlayerController.

            var rootStateMachine = controller.layers[0].stateMachine;
            var state = rootStateMachine.AddState("Locomotion");
            state.motion = blendTree;
            rootStateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[AdventurerCharacterSetup] Animator de locomoção criado em " + ControllerPath);
            return controller;
        }

        private static AnimationClip CreateLoopingClip(string fbxPath, string sourceClipName, string outputName)
        {
            var outputPath = $"{ClipsDir}/{outputName}.anim";
            var existingClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
            if (existingClip != null) return existingClip;

            var source = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == sourceClipName);
            if (source == null) return null;

            var clone = Object.Instantiate(source);
            clone.name = outputName;
            var settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clone, settings);
            AssetDatabase.CreateAsset(clone, outputPath);
            return clone;
        }

        private static void BuildAdventurerPrefab(RuntimeAnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/Knight.fbx");
            if (modelAsset == null)
            {
                Debug.LogWarning("[AdventurerCharacterSetup] Knight.fbx não encontrado.");
                return;
            }

            var root = (GameObject)Object.Instantiate(modelAsset);
            root.name = "Aventureiro";
            ApplyCharacterTexture(root, CharactersDir + "/knight_texture.png", "Knight");
            ScaleToTargetHeight(root);

            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            AttachEquipment(root.transform, "handslot.r", EquipmentDir + "/sword_1handed.fbx", CharactersDir + "/knight_texture.png", "Knight_Sword");
            AttachEquipment(root.transform, "handslot.l", EquipmentDir + "/shield_round.fbx", CharactersDir + "/knight_texture.png", "Knight_Shield");

            Directory.CreateDirectory(OutputDir);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[AdventurerCharacterSetup] Prefab do aventureiro criado em " + PrefabPath);
        }

        // NPC da vila (ex.: Ancia Mira) usava cápsula+esfera genérica — visualmente muito abaixo do
        // personagem real do jogador. Reaproveita o mesmo pipeline com outro modelo KayKit (Mage,
        // sem arma) só pra ter um humanoide de verdade parado na cena.
        private static void BuildNpcPrefab(RuntimeAnimatorController controller)
        {
            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharactersDir + "/Mage.fbx");
            if (modelAsset == null)
            {
                Debug.LogWarning("[AdventurerCharacterSetup] Mage.fbx não encontrado.");
                return;
            }

            var root = (GameObject)Object.Instantiate(modelAsset);
            root.name = "Vila NPC";
            ApplyCharacterTexture(root, CharactersDir + "/mage_texture.png", "Mage");
            ScaleToTargetHeight(root);

            var animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            Directory.CreateDirectory(OutputDir);
            PrefabUtility.SaveAsPrefabAsset(root, NpcPrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[AdventurerCharacterSetup] Prefab de NPC criado em " + NpcPrefabPath);
        }

        private static void AttachEquipment(Transform characterRoot, string boneName, string fbxPath, string texturePath, string materialName)
        {
            var bone = FindDeepChild(characterRoot, boneName);
            if (bone == null)
            {
                Debug.LogWarning($"[AdventurerCharacterSetup] Osso '{boneName}' não encontrado no modelo.");
                return;
            }

            var itemAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (itemAsset == null) return;

            var item = (GameObject)Object.Instantiate(itemAsset, bone, false);
            item.name = Path.GetFileNameWithoutExtension(fbxPath);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            ApplyCharacterTexture(item, texturePath, materialName);
        }

        private static void ScaleToTargetHeight(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.01f) return;
            var scale = TargetHeightUnits / bounds.size.y;
            root.transform.localScale *= scale;
        }

        // Materiais criados em script (new Material(...)) só existem na memória: se forem
        // referenciados por um GameObject salvo com PrefabUtility.SaveAsPrefabAsset sem antes
        // virarem um asset persistido (AssetDatabase.CreateAsset/AddObjectToAsset), a referência
        // serializa como null (fileID: 0) e o Unity renderiza o material de erro (rosa). Por
        // isso cada material aqui é salvo como .mat de verdade antes de ser atribuído.
        private static void ApplyCharacterTexture(GameObject instance, string texturePath, string materialName)
        {
            Directory.CreateDirectory(MaterialsDir);
            var materialPath = $"{MaterialsDir}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var shader = Shader.Find("Espectro/ToonLit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = materialName };
                material.color = Color.white;
                if (texture != null) material.mainTexture = texture;
                AssetDatabase.CreateAsset(material, materialPath);
            }

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (var i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
