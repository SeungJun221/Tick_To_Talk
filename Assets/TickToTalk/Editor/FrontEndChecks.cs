using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace TickToTalk.Editor
{
    public static class FrontEndChecks
    {
        public static void RunInPlayMode()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Enter play mode");
            int checks=0; Action<bool,string> check=(ok,why)=>{checks++;if(!ok)throw new Exception(why);};
            var c=Resources.Load<WardrobeCatalog>("WardrobeCatalog");
            var actor=UnityEngine.Object.Instantiate(c.basePrefab); actor.transform.position=new Vector3(1000,0,1000);
            try {
                for(int i=0;i<c.categories.Length;i++)for(int j=0;j<c.categories[i].options.Length;j++) {
                    var choices=c.Defaults();choices[i]=j;c.Apply(actor.transform,choices);
                    foreach(var piece in c.categories[i].options[j].pieces)check(actor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r=>r.sharedMesh==piece.mesh),"Missing variant "+c.categories[i].options[j].name);
                    check(c.IsValid(c.Encode(choices)),"Appearance round trip");
                }
                check(!c.IsValid("999,evil"),"Invalid appearance accepted");
                var a=actor.GetComponentInChildren<Animator>();
                foreach(string state in new[]{"Jump_Start","Jump_Loop","Jump_End"}) {
                    var ys=new System.Collections.Generic.List<float>();
                    for(int i=0;i<=20;i++){a.Play(state,0,i/20f);a.Update(0);ys.Add(a.GetBoneTransform(HumanBodyBones.Hips).position.y-actor.transform.position.y);}
                    check(ys.Max()-ys.Min()<.18f && ys.Max()<1,"Jump body still moves independently: "+state);
                }
                var p=UnityEngine.Object.FindFirstObjectByType<PrisonPresentation>();
                var prefs=p.GetComponent<GamePreferences>();var voice=p.GetComponent<TableVoice>();
                float original=prefs.Master;prefs.Master=.37f;prefs.ApplyAudio();check(Math.Abs(AudioListener.volume-.37f)<.001f,"Master slider not applied");prefs.Master=original;prefs.ApplyAudio();
                p.OpenSettings();voice.MonitorRequested=true;p.CloseSettings();check(!p.SettingsOpen&&!voice.MonitorRequested,"Microphone test continues after closing");
                var session=p.GetComponent<NetworkSession>();var previous=session.View;
                session.View=new PlayerView{phase=1,slot=0};p.OpenSettings();check(p.SettingsOpen&&Cursor.lockState==CursorLockMode.None,"In-game settings failed");p.CloseSettings();session.View=previous;
                var startCurve=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/TickToTalk/Jump_Start_InPlace.anim");
                var binding=AnimationUtility.GetCurveBindings(startCurve).First(b=>b.propertyName=="RootT.y");var curve=AnimationUtility.GetEditorCurve(startCurve,binding);
                check(Math.Abs(curve.Evaluate(0)-curve.Evaluate(startCurve.length))<.001f,"Root height not normalized");
                System.IO.File.WriteAllText("Logs/FrontEndChecks.txt","PASS "+checks+" checks: all 33 wardrobe options, invalid IDs, humanoid jump body height, master volume application, microphone test cleanup, in-game settings.");
            } finally {UnityEngine.Object.Destroy(actor);}
        }
    }
}
