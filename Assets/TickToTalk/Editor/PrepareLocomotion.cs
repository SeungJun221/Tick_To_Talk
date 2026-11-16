using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace TickToTalk.Editor
{
    public static class PrepareLocomotion
    {
        public static void Apply()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/TickToTalk/PlayerMotion.controller");
            foreach(string name in new[]{"Idle_Breathing","Walk_Forward","Run_Forward"})
            {
                string path="Assets/TickToTalk/"+name+"_Loop.anim";
                var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/"+name+".anim");
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(!clip){clip=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(clip,path);}
                else EditorUtility.CopySerialized(source,clip);
                clip.name=name+"_Loop";
                var settings=AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime=true;settings.loopBlend=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                var state=controller.layers[0].stateMachine.states.First(s=>s.state.name==name).state;
                state.motion=clip;EditorUtility.SetDirty(state);EditorUtility.SetDirty(clip);
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        public static void Verify()
        {
            var p=UnityEngine.Object.FindFirstObjectByType<PrisonPresentation>();
            var copy=UnityEngine.Object.Instantiate(p.playerA.gameObject);
            try
            {
                foreach(var r in copy.GetComponentsInChildren<Renderer>())r.enabled=false;
                var animator=copy.GetComponentInChildren<Animator>();
                animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.applyRootMotion=false;
                animator.Rebind();animator.Update(0);
                var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                int checks=0;
                foreach(string state in new[]{"Walk_Forward","Run_Forward"})
                {
                    animator.Play(state,0,0);animator.Update(0);
                    Vector3 min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue),max=new Vector3(float.MinValue,float.MinValue,float.MinValue);
                    for(int frame=0;frame<240;frame++)
                    {
                        animator.Update(.05f);
                        if(frame>=160){Vector3 local=copy.transform.InverseTransformPoint(foot.position);min=Vector3.Min(min,local);max=Vector3.Max(max,local);}
                    }
                    var info=animator.GetCurrentAnimatorStateInfo(0);
                    if(!info.loop||info.normalizedTime<5||(max-min).magnitude<.08f)throw new Exception(state+" stopped looping: "+info.normalizedTime+" foot range="+(max-min).magnitude);
                    checks++;
                }
                System.IO.File.WriteAllText("Logs/LocomotionLoopChecks.txt","PASS "+checks+" animations: 12 seconds each with renderers hidden; normalized time >5 cycles and foot motion continues during seconds 8-12.");
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }
    }
}
