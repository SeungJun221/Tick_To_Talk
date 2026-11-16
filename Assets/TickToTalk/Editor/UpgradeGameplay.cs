using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace TickToTalk.Editor
{
    public static class UpgradeGameplay
    {
        public static void Apply()
        {
            const string path = "Assets/TickToTalk/PlayerMotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
                var machine = controller.layers[0].stateMachine;
                foreach (string name in new[] { "Idle_Breathing", "Walk_Forward", "Run_Forward", "Jump_Start", "Jump_Loop", "Jump_End" }) {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/" + name + ".anim");
                    if (!clip) throw new Exception("Missing animation " + name);
                    var state = machine.AddState(name); state.motion = clip;
                    if (name == "Idle_Breathing") machine.defaultState = state;
                }
            }
            var view = UnityEngine.Object.FindFirstObjectByType<PrisonPresentation>();
            foreach (var actor in new[] {view.playerA, view.playerB}) {
                var animator = actor.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; EditorUtility.SetDirty(animator);
            }
            foreach (string name in new[] { "A", "B" }) {
                string prefab = "Assets/TickToTalk/Prefabs/Player_" + name + ".prefab";
                var obj = PrefabUtility.LoadPrefabContents(prefab);
                try { obj.GetComponentInChildren<Animator>().runtimeAnimatorController = controller; PrefabUtility.SaveAsPrefabAsset(obj, prefab); }
                finally { PrefabUtility.UnloadPrefabContents(obj); }
            }
            EditorSceneManager.MarkSceneDirty(view.gameObject.scene); EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            Debug.Log("TTT_MOVEMENT_ANIMATIONS_INSTALLED");
        }
        public static void VerifyAudio()
        {
            var go = new GameObject("Temporary audio verification");
            try {
                var voice = go.AddComponent<TableVoice>();
                for (uint packet = 0; packet < 5; packet++) voice.Receive(Enumerable.Repeat(.25f, 320).ToArray(), packet);
                var data = new float[2880]; voice.RenderAudio(data, 2);
                if (data.Count(v => v > .2f) < 2800 || voice.PlayedSamples < 479 || voice.PlayedSamples > 481) throw new Exception("Audio resampling/output failed");
                voice.Receive(new float[320], 2);
                if (voice.ReceivedPackets != 5) throw new Exception("Out of order packet accepted");
                voice.ResetPlayback(); voice.RenderAudio(data, 2);
                if (data.Any(v => v != 0)) throw new Exception("Stale voice after disconnect");
                System.IO.File.WriteAllText("Logs/AudioOutputTests.txt", "PASS: PCM reception, 16kHz->48kHz stereo rendering, audible nonzero samples, reorder rejection, disconnect silence.");
                Debug.Log("TTT_AUDIO_OUTPUT_PASS");
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
