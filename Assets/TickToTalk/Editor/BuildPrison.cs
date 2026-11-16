using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.Character;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.MaterialManagement;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.FaceEditor;

namespace TickToTalk.Editor
{
    public static class BuildPrison
    {
        const string Root = "Assets/TickToTalk/";
        static Material concrete, steel, wood, black, amber, cyan, red, glass;
        static Transform architecture;
        static Material Mat(string name, Color color, float metal = 0, float smooth = .3f)
        {
            var path = Root + "Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth); return m;
        }
        static GameObject Shape(string name, Vector3 p, Vector3 size, Material mat, PrimitiveType primitive = PrimitiveType.Cube)
        {
            var g = GameObject.CreatePrimitive(primitive); g.name = name; g.transform.SetParent(architecture);
            g.transform.position = p; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = mat; return g;
        }
        // Floor paint and seams are visual only; Foundation provides the single flat walking surface.
        static void FloorMark(string name, Vector3 p, Vector3 size, Material mat)
        {
            p.y = .0005f; size.y = .001f;
            var g = Shape(name, p, size, mat);
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        static void Sign(string name, string value, Vector3 p, Vector3 rotation, float scale, Color color)
        {
            var g = new GameObject(name); g.transform.SetParent(architecture); g.transform.position = p; g.transform.eulerAngles = rotation;
            var t = g.AddComponent<TextMesh>(); t.text = value; t.fontSize = 72; t.characterSize = scale * .6f; t.anchor = TextAnchor.MiddleCenter; t.alignment = TextAlignment.Center; t.color = color;
            var renderer = g.GetComponent<Renderer>();
            var textMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/WorldText.mat");
            if (!textMaterial) { textMaterial = new Material(Shader.Find("TickToTalk/WorldText")); textMaterial.mainTexture = renderer.sharedMaterial.mainTexture; AssetDatabase.CreateAsset(textMaterial, Root + "Materials/WorldText.mat"); }
            renderer.sharedMaterial = textMaterial;
            g.AddComponent<WorldKoreanSign>();
        }
        static Light Lamp(string name, Vector3 p, Color color, float intensity, float range)
        {
            var g = new GameObject(name); g.transform.SetParent(architecture); g.transform.position = p;
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = color; l.intensity = intensity; l.range = range; l.shadows = LightShadows.Soft; return l;
        }
        [MenuItem("Tick To Talk/Build Prison Scene")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Root + "Materials"); Directory.CreateDirectory(Root + "Prefabs"); Directory.CreateDirectory(Root + "Scenes");
            // Preserve the user's existing scene. Generate our own additive scene.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            architecture = new GameObject("THE DIVIDE / Prison Architecture").transform;
            concrete = Mat("Concrete", new Color(.19f, .21f, .22f));
            steel = Mat("Gunmetal", new Color(.1f, .14f, .16f), .8f);
            wood = Mat("Worn Oak", new Color(.28f, .14f, .065f), 0, .15f);
            black = Mat("Rubber", new Color(.025f, .035f, .04f));
            amber = Mat("Amber", new Color(1, .48f, .12f), .4f); cyan = Mat("Cyan", new Color(.1f, .68f, .8f), .4f);
            red = Mat("Danger", new Color(.65f, .06f, .045f), .1f);
            glass = Mat("Reinforced Translucent Glass", new Color(.24f, .55f, .62f, .22f), .15f, .87f);
            glass.SetFloat("_Surface", 1); glass.SetFloat("_Blend", 0); glass.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); glass.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glass.SetInt("_ZWrite", 0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.SetOverrideTag("RenderType", "Transparent"); glass.renderQueue = 3000;
            Shape("Foundation", new Vector3(0, -.2f, 0), new Vector3(16, .4f, 8.5f), concrete);
            Shape("North concrete wall", new Vector3(0, 2, 4), new Vector3(16, 4, .35f), concrete);
            Shape("South concrete wall", new Vector3(0, 2, -4), new Vector3(16, 4, .35f), concrete);
            Shape("West concrete wall", new Vector3(-8, 2, 0), new Vector3(.35f, 4, 8), concrete);
            Shape("East concrete wall", new Vector3(8, 2, 0), new Vector3(.35f, 4, 8), concrete);
            Shape("Ceiling", new Vector3(0, 4.15f, 0), new Vector3(16, .3f, 8), black);
            for (int x = -7; x <= 7; x += 2)
            {
                Shape("Ceiling beam", new Vector3(x, 3.85f, 0), new Vector3(.15f, .3f, 8), steel);
                for (int z = -3; z <= 3; z += 2) FloorMark("Floor seam", new Vector3(x, .006f, z), new Vector3(1.98f, .012f, .025f), steel);
                FloorMark("Floor longitudinal joint", new Vector3(x, .006f, 0), new Vector3(.022f, .012f, 8), steel);
            }
            for (int row = 0; row < 5; row++) for (int brick = 0; brick < 16; brick++)
            {
                float x = -7.5f + brick + (row % 2) * .35f;
                if (x > 7.7f) continue;
                Shape("Concrete block joint", new Vector3(x, .4f + row * .72f, 3.81f), new Vector3(.985f, .012f, .015f), black);
                Shape("Concrete vertical joint", new Vector3(x - .5f, .75f + row * .72f, 3.81f), new Vector3(.012f, .7f, .015f), black);
            }
            var pane = Shape("REINFORCED GLASS / acoustic isolation", new Vector3(0, 2, 0), new Vector3(.12f, 3.8f, 7.7f), glass);
            for (int z = -4; z <= 4; z += 2)
            {
                Shape("Glass vertical reinforcement", new Vector3(0, 2, z), new Vector3(.24f, 4, .12f), steel);
                for (int y = 1; y <= 3; y++) Shape("Glass bolt", new Vector3(-.14f, y, z), Vector3.one * .075f, amber, PrimitiveType.Sphere);
            }
            for (int y = 0; y <= 4; y += 4) Shape("Glass horizontal rail", new Vector3(0, y, 0), new Vector3(.28f, .16f, 8), steel);
            for (int z = -3; z <= 3; z++) Shape("Glass safety stripe", new Vector3(-.075f, 1.2f, z), new Vector3(.006f, .04f, .42f), cyan);
            var runtime = new GameObject("TickToTalk / Dedicated Server + Client"); runtime.AddComponent<NetworkSession>();
            var presentation = runtime.AddComponent<PrisonPresentation>(); presentation.glass = pane.GetComponent<Renderer>();
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1 : 1; var color = s == 0 ? amber : cyan;
                var room = new GameObject(s == 0 ? "CELL A / AMBER" : "CELL B / CYAN"); room.transform.SetParent(architecture);
                Shape("Intercom sign backing", new Vector3(side * .29f, 2.85f, 0), new Vector3(.04f, .62f, 1.85f), steel);
                Shape("Bomb workbench", new Vector3(side * 7.25f, .7f, 0), new Vector3(1.1f, 1.4f, 2.15f), steel);
                Shape("Bomb armored housing", new Vector3(side * 7.2f, 1.6f, 0), new Vector3(.8f, .65f, 1.7f), black);
                for (int k = 0; k < 4; k++) Shape("Charge cylinder", new Vector3(side * 7.2f, 1.62f, -.57f + k * .38f), new Vector3(.25f, .42f, .25f), red, PrimitiveType.Cylinder);
                Shape("Bomb terminal display", new Vector3(side * 6.76f, 1.48f, 0), new Vector3(.035f, .28f, .54f), color);
                for (int k = 0; k < 6; k++) Shape("Terminal wire", new Vector3(side * 6.73f, 1.22f, -.5f + k * .2f), new Vector3(.04f, .1f, .035f), k % 2 == 0 ? color : red);
                var indicators = new GameObject("Bomb status lights " + s).transform; indicators.SetParent(architecture);
                Shape("Bomb warning mounting strip", new Vector3(side * 6.798f, 1.74f, 0), new Vector3(.045f, .24f, 1.38f), steel);
                for (int k = 0; k < 3; k++)
                {
                    var light = Shape("Wrong answer " + (k + 1), new Vector3(side * 6.77f, 1.74f, -.4f + k * .4f), new Vector3(.10f, .14f, .14f), black, PrimitiveType.Sphere);
                    light.transform.SetParent(indicators, true); light.GetComponent<Collider>().enabled = false;
                }
                for (int sector = 1; sector < 4; sector++)
                {
                    float z = sector * 9;
                    Shape("Bomb warning mounting strip", new Vector3(side * 6.798f, 1.74f, z), new Vector3(.045f, .24f, 1.38f), steel);
                    for (int k = 0; k < 3; k++)
                    {
                        var light = Shape("Wrong answer " + (k + 1), new Vector3(side * 6.77f, 1.74f, z - .4f + k * .4f), new Vector3(.10f, .14f, .14f), black, PrimitiveType.Sphere);
                        light.transform.SetParent(indicators, true); light.GetComponent<Collider>().enabled = false;
                    }
                }
                Shape("Encrypted manual cabinet", new Vector3(side * 4, 1.9f, 3.70f), new Vector3(2.25f, 1.4f, .22f), steel);
                Shape("Manual screen", new Vector3(side * 4, 1.9f, 3.57f), new Vector3(2.05f, 1.2f, .035f), black);
                for (int sector = 0; sector < 4; sector++)
                {
                    float z = sector * 9;
                    if (sector == 0) Shape("Manual status mounting strip", new Vector3(side * 4, 1.04f, 3.70f), new Vector3(2.05f, .24f, .05f), steel);
                    var light = Shape("Sector success light " + (sector + 1) + (s == 0 ? "A" : "B"), new Vector3(side * 4, 1.04f, z + 3.645f), new Vector3(.14f, .14f, .10f), black, PrimitiveType.Sphere);
                    light.GetComponent<Collider>().enabled = false;
                }
                if (s == 0) presentation.indicatorA = indicators; else presentation.indicatorB = indicators;
                Shape("Controls placard backing", new Vector3(side * 2.22f, 1.68f, -3.81f), new Vector3(1.65f, 1.52f, .10f), steel);
                Shape("Controls placard face", new Vector3(side * 2.22f, 1.68f, -3.745f), new Vector3(1.56f, 1.43f, .016f), black);
                Sign("Emote key instruction", "`  감정표현\nCaps Lock  무전기 송신", new Vector3(side * 2.22f, 1.99f, -3.70f), new Vector3(0,180,0), .025f, Color.white);
                Sign("Tablet key instruction", "T  태블릿\n마우스로 필기 · 자동 저장", new Vector3(side * 2.22f, 1.38f, -3.70f), new Vector3(0,180,0), .025f, Color.white);
                Shape("Exit frame", new Vector3(side * 4, 1.65f, -3.75f), new Vector3(1.95f, 3.3f, .3f), steel);
                var door = Shape("Locked exit " + s, new Vector3(side * 4, 1.6f, -3.53f), new Vector3(1.65f, 3.15f, .15f), black).transform;
                for (int k = 0; k < 7; k++) Shape("Exit prison bar", new Vector3(side * 4 - .7f + k * .23f, 2.4f, -3.4f), new Vector3(.045f, 1.25f, .045f), steel);
                FloorMark("Cell stripe", new Vector3(side * 4, .02f, -2.7f), new Vector3(5.8f, .025f, .06f), color);
                Shape("Ceiling lamp housing", new Vector3(side * 4, 3.78f, 0), new Vector3(2, .12f, .5f), steel);
                Lamp("Cell softbox", new Vector3(side * 4, 3.5f, 0), new Color(.75f, .85f, .92f), 4, 9);
                Lamp("Station light", new Vector3(side * 2, 2.7f, 1), s == 0 ? new Color(1, .6f, .3f) : new Color(.25f, .75f, 1), 1.8f, 5);
                var alarm = Lamp("Alarm " + s, new Vector3(side * 6.5f, 3, -2.8f), new Color(1, .1f, .04f), .4f, 6);
                var actor = Character(s); actor.transform.position = new Vector3(side * 4, 0, 0); actor.transform.eulerAngles = new Vector3(0, s == 0 ? 90 : 270, 0);
                if (s == 0) { presentation.playerA = actor.transform; presentation.doorA = door; presentation.alarmA = alarm; }
                else { presentation.playerB = actor.transform; presentation.doorB = door; presentation.alarmB = alarm; }
            }
            var sun = new GameObject("Main Directional Light").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = .3f; sun.transform.rotation = Quaternion.Euler(45, -30, 0);
            var camera = new GameObject("Player View / Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera"; camera.fieldOfView = 72; camera.nearClipPlane = .05f; camera.farClipPlane = 80;
            camera.transform.position = new Vector3(-6.3f, 2.2f, -2.7f); camera.transform.LookAt(new Vector3(1, 1.3f, 1)); camera.gameObject.AddComponent<AudioListener>(); presentation.eye = camera;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.24f, .27f, .3f); RenderSettings.fog = true; RenderSettings.fogColor = new Color(.045f, .065f, .075f); RenderSettings.fogDensity = .018f;
            EditorSceneManager.SaveScene(scene, Root + "Scenes/TickToTalk_Prison.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "Scenes/TickToTalk_Prison.unity", true) };
            PlayerSettings.companyName = "Tick To Talk"; PlayerSettings.productName = "Tick To Talk"; PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720; PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            AssetDatabase.SaveAssets();
            Debug.Log("TTT_SCENE_CREATED");
        }
        static GameObject Character(int slot)
        {
            var character = new CustomizableCharacter(SlotLibraryLoader.LoadSlotLibrary());
            character.ToDefault();
            foreach (var type in new[] { SlotType.Body, SlotType.Faces, SlotType.Outerwear, SlotType.Pants, SlotType.Shoes, SlotType.Hairstyle }) character.Toggle(type, true);
            if (slot == 1) { character.SelectNext(SlotType.Outerwear); character.SelectNext(SlotType.Pants); character.SelectNext(SlotType.Hairstyle); }
            var go = character.InstantiateCharacter(); go.name = slot == 0 ? "Player_A_Creative" : "Player_B_Creative";
            var material = new MaterialProvider().MainColor;
            foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.sharedMesh = null;
            foreach (var s in character.Slots.Where(s => s.IsEnabled)) foreach (var mesh in s.Meshes)
            {
                var child = go.transform.Cast<Transform>().First(t => t.name.StartsWith(mesh.Item1.ToString()));
                var renderer = child.GetComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh.Item2; renderer.sharedMaterial = material; renderer.localBounds = mesh.Item2.bounds; renderer.updateWhenOffscreen = true;
            }
            FaceLoader.AddFaces(go);
            var animator = go.GetComponent<Animator>(); if (!animator) animator = go.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Root + "PlayerMotion.controller") ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AssetsPath.AnimationController); animator.applyRootMotion = false;
            PrefabUtility.SaveAsPrefabAsset(go, Root + "Prefabs/Player_" + (slot == 0 ? "A" : "B") + ".prefab");
            return go;
        }
        [MenuItem("Tick To Talk/Build Windows Online Client")]
        public static void BuildWindows()
        {
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Root + "Scenes/TickToTalk_Prison.unity" }, locationPathName = "Builds/OnlineWindows/TickToTalk.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Build failed: " + result.summary.result);
            File.WriteAllText("Builds/OnlineWindows/Start_Client.cmd", "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" TickToTalk.exe\r\n");
            Debug.Log("TTT_BUILD_SUCCEEDED");
        }
        [MenuItem("Tick To Talk/Build Tick_To_Talk_test")]
        public static void BuildTestRelease()
        {
            const string output = "Builds/Tick_To_Talk_test";
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Root + "Scenes/TickToTalk_Prison.unity" }, locationPathName = output + "/TickToTalk.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Build failed: " + result.summary.result);
            File.WriteAllText(output + "/Start_Client.cmd", "@echo off\r\ncd /d \"%~dp0\"\r\nstart \"\" TickToTalk.exe\r\n");
            File.Copy(Root + "README_KO.md", output + "/README_KO.md", true);
            Debug.Log("TTT_TEST_BUILD_SUCCEEDED");
        }
        [MenuItem("Tick To Talk/Verify Puzzle Rules")]
        public static void Verify()
        {
            int checks = 0;
            Action<bool, string> assert = (ok, message) => { checks++; if (!ok) throw new Exception(message); };
            for (int seed = 0; seed < 2000; seed++)
            {
                var r = new RoundRules(); r.Start(seed, 0);
                foreach (var b in r.Bombs) for (int i = 0; i < 4; i++) assert(b.Answers[i].Length == 6 && b.Answers[i].All(char.IsDigit), "Malformed answer");
                assert(r.Talk(0, 1), "Cannot acquire token"); assert(!r.Talk(1, 2), "Duplex audio allowed"); r.Tick(21); assert(r.Speaker == 0 && !r.Talk(1, 22), "Speech ended without release"); r.Release(23); assert(r.Talk(1, 23), "Speech must be available immediately after release"); r.Release(24);
                for (int s = 0; s < 2; s++) for (int stage = 0; stage < 3; stage++) r.Submit(s, r.Bombs[s].Answers[stage], 30 + stage);
                assert(r.EndAt == 332, "Both players' third modules did not reset the clock");
                r.Start(seed, 0);
                r.Submit(0, "bad", 1);
                r.Submit(0, r.Bombs[0].Answers[0], 10);
                assert(r.EndAt == 280, "First player alone reset the clock");
                r.Submit(1, r.Bombs[1].Answers[0], 11);
                assert(r.EndAt == 311, "Both first modules did not reset the clock");
                r.Submit(1, r.Bombs[1].Answers[1], 14);
                assert(r.EndAt == 311, "First player on second module reset the clock");
                r.Submit(0, r.Bombs[0].Answers[1], 15);
                assert(r.EndAt == 315, "Both second modules did not reset the clock");
                r.Submit(0, "bad", 16);
                assert(r.EndAt == 295, "Penalty was lost after reset");
                r.Submit(1, r.Bombs[1].Answers[2], 18);
                assert(r.EndAt == 295, "Third module reset early");
                r.Submit(0, r.Bombs[0].Answers[2], 19);
                assert(r.EndAt == 319, "Both third modules did not reset the clock");
                r.Submit(0, r.Bombs[0].Answers[3], 40); r.Tick(56); assert(!r.Bombs[0].Armed && r.Phase == 1, "Arm window stuck");
                r.Submit(0, r.Bombs[0].Answers[3], 57); r.Submit(1, r.Bombs[1].Answers[3], 60); assert(r.Phase == 4, "Exit must unlock before escape");
                r.Start(seed, 0); r.Submit(0, "bad", 1); r.Submit(1, "bad", 2); r.Submit(0, "bad", 3);
                assert(r.Phase == 1 && r.StrikesByRoom[0] == 2 && r.StrikesByRoom[1] == 1 && r.EndAt == 240, "Strikes must be per room");
                r.Submit(0, "bad", 4); assert(r.Phase == 3 && r.StrikesByRoom[0] == 3 && r.EndAt == 220, "First room reaching three strikes must end round");
                r.Start(seed, 0); r.Submit(1, "bad", 1); r.Submit(1, "bad", 2); r.Submit(1, "bad", 3);
                assert(r.Phase == 3 && r.StrikesByRoom[1] == 3 && r.StrikesByRoom[0] == 0, "Second room reaching three strikes must end round");
                r.Start(seed, 0); r.Submit(0, "bad", 1); r.Submit(0, "bad", 2); r.Submit(0, r.Bombs[0].Answers[0], 3);
                assert(r.Bombs[0].Stage == 1 && r.Bombs[0].Mistakes[1] == 0 && r.Phase == 1, "Next bomb must have three fresh chances");
                r.Submit(0, "bad", 4); r.Submit(0, "bad", 5); r.Submit(0, "bad", 6);
                assert(r.Phase == 3 && r.Bombs[0].Mistakes[1] == 3, "Three mistakes on the next bomb must fail");
                r.Start(seed, 0); r.Submit(0, "bad", 295); assert(r.Phase == 3, "Penalty expired timer not enforced");
                r.Start(seed, 0); r.Tick(300); assert(r.Phase == 3 && !r.Talk(0, 301), "Timeout not enforced");
            }
            File.WriteAllText("Logs/TickToTalk_PuzzleTests.txt", "PASS " + checks + " assertions across 2000 seeds."); Debug.Log("TTT_TESTS_PASS " + checks);
        }
    }
}