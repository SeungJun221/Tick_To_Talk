using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace TickToTalk.Editor
{
    [InitializeOnLoad]
    public static class FailureSequenceChecks
    {
        static FailureSequenceChecks() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if(SessionState.GetBool("TTT_FailureChecksPending",false) && EditorApplication.isPlaying && NetworkSession.Instance && !EditorApplication.isCompiling)
            { SessionState.SetBool("TTT_FailureChecksPending",false); Run(); }
            const string path = "Temp/ttt-failure-check-request";
            if(EditorApplication.isCompiling || !File.Exists(path)) return;
            string request = File.ReadAllText(path).Trim();
            File.Delete(path);
            if(request=="setup") { PrepareFailureEffect.Run(); return; }
            if(request=="test") { StartChecks(); return; }
            if(request == "packages")
            {
                UnityEditor.PackageManager.UI.Window.Open(null);
                EditorApplication.delayCall += () => {
                    File.WriteAllText("Temp/ttt-editor-windows.txt", "");
                    foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    {
                        File.AppendAllText("Temp/ttt-editor-windows.txt", window.GetType().FullName + " " + window.position + "\n");
                        if(window.GetType().Name.Contains("PackageManager"))
                        { window.position = new Rect(200, 180, 1100, 650); window.Show(); window.Focus(); }
                    }
                };
            }
        }

        [MenuItem("Tick To Talk/Verify Failure Cinematic")]
        public static void StartChecks()
        {
            SessionState.SetBool("TTT_FailureChecksPending",true);
            EditorApplication.isPlaying=true;
        }
        static void Check(bool condition,string message)
        { if(!condition)throw new Exception(message); }

        static async void Run()
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/FailureSequenceChecks.txt","RUNNING");
            NetworkSession s=null;
            try
            {
                await Task.Delay(1200);
                s=NetworkSession.Instance;
                var p=s.GetComponent<PrisonPresentation>();
                var cinematic=s.GetComponent<FailureCinematic>();
                Check(cinematic,"Failure component missing");
                var prefab=Resources.Load<GameObject>("Effects/FailureExplosion");
                Check(prefab && prefab.GetComponentsInChildren<ParticleSystem>(true).Length>0,"WAR FX prefab not bound");
                foreach(var renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    Check(renderer.sharedMaterial && renderer.sharedMaterial.shader.isSupported,"Unsupported explosion material: "+renderer.name);
                    foreach(var mat in renderer.sharedMaterials)if(mat)Check(mat.shader.isSupported,"Unsupported shader: "+mat.name);
                }
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var tick=typeof(NetworkSession).GetMethod("Update",flags);
                var start=typeof(NetworkSession).GetMethod("StartPreparedRound",flags);
                var create=typeof(NetworkSession).GetMethod("CreateView",flags);
                var positions=(Vector2[])typeof(NetworkSession).GetField("positions",flags).GetValue(s);
                s.StartTestMode(); start.Invoke(s,null);
                positions[0]=new Vector2(-4,0);positions[1]=new Vector2(4,0);
                tick.Invoke(s,null); await Task.Delay(250);
                s.Rules.EndAt=Time.realtimeSinceStartupAsDouble-1;
                tick.Invoke(s,null);
                Check(s.View.phase==3 && s.View.exploded,"Timeout did not trigger explosion");
                s.Command("ready"); Check(s.Rules.Phase==3,"Early ready bypassed cinematic");
                await Task.Delay(200);
                Check(cinematic.Active&&!cinematic.Finished&&cinematic.ObservedRoom==1,"Player A must watch room B before UI: active="+cinematic.Active+" finished="+cinematic.Finished+" room="+cinematic.ObservedRoom+" elapsed="+s.View.failureElapsed+" phase="+s.View.phase+" pEnabled="+p.enabled+" paused="+EditorApplication.isPaused);
                ScreenCapture.CaptureScreenshot("Logs/Failure-CCTV.png");
                await Task.Delay(950);
                Check(cinematic.BlastCount==2,"Both rooms must explode exactly once");
                Check(p.playerA.GetComponentsInChildren<Rigidbody>().Length>=9 && p.playerB.GetComponentsInChildren<Rigidbody>().Length>=9,"Both ragdolls required");
                Check(!p.playerB.GetComponentInChildren<Animator>().enabled,"Animator overrides ragdoll");
                ScreenCapture.CaptureScreenshot("Logs/Failure-Blast.png");
                await Task.Delay(2600);
                var hips=p.playerB.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Hips);
                Check(hips.position.y<.8f && hips.position.y>-.5f,"Corpse did not land on floor: "+hips.position);
                Check(!cinematic.Finished,"Failure UI shown before sequence ends");
                ScreenCapture.CaptureScreenshot("Logs/Failure-Corpse.png");
                await Task.Delay(2000);
                Check(cinematic.Finished && cinematic.BlastCount==2,"Repeated explosions or missing result UI");
                ScreenCapture.CaptureScreenshot("Logs/Failure-Result.png");
                s.Command("ready");await Task.Delay(250);
                Check(s.Rules.Phase==1&&!cinematic.Active,"Restart did not restore gameplay");
                Check(p.playerA.GetComponentsInChildren<Rigidbody>().Length==0 && p.playerA.GetComponentInChildren<Animator>().enabled,"Ragdoll not cleaned after restart");
                Check(Mathf.Abs(p.eye.fieldOfView-60)<.1f,"Camera FOV not restored");
                // Exercise the other client's serialized view independently of solo-mode slot 0.
                positions[0]=new Vector2(-4,9);positions[1]=new Vector2(4,9);
                s.Rules.Bombs[0].Stage=s.Rules.Bombs[1].Stage=1;
                for(int n=0;n<3;n++)s.Rules.Submit(0,"wrong",Time.realtimeSinceStartupAsDouble);
                Check(s.Rules.Exploded && s.Rules.Phase==3,"Third wrong answer did not detonate");
                s.enabled=false;
                s.View=JsonUtility.FromJson<PlayerView>(JsonUtility.ToJson(create.Invoke(s,new object[]{1})));
                await Task.Delay(250);
                Check(cinematic.ObservedRoom==0,"Player B must watch room A");
                await Task.Delay(1200);ScreenCapture.CaptureScreenshot("Logs/Failure-PlayerB.png");
                Check(cinematic.BlastCount==2,"Second failure must replay both blasts");
                s.View=null;await Task.Delay(100);
                Check(!cinematic.Active && p.playerB.GetComponentsInChildren<Rigidbody>().Length==0,"Disconnect cleanup failed");
                File.WriteAllText("Logs/FailureSequenceChecks.txt","PASS: WAR FX/URP assets, timeout, early-ready rejection, A->B CCTV, two blasts once, both ragdolls, corpse landing, delayed result UI, restart/body/camera restoration, third wrong answer, B->A CCTV, repeated round, disconnect cleanup.");
            }
            catch(Exception e) { File.WriteAllText("Logs/FailureSequenceChecks.txt","FAIL "+e);Debug.LogException(e); }
            finally { if(s){s.enabled=true;s.Disconnect();} EditorApplication.isPlaying=false; }
        }
    }
}
