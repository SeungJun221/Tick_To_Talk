using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor;
using ithappy.Creative_Characters_FREE.CharacterCustomizationTool.Editor.MaterialManagement;

namespace TickToTalk.Editor
{
    public static class PrepareFrontEnd
    {
        public static void Apply()
        {
            if(!AssetDatabase.IsValidFolder("Assets/TickToTalk/Resources")) AssetDatabase.CreateFolder("Assets/TickToTalk","Resources");
            var source=AssetDatabase.LoadAssetAtPath<SlotLibrary>(AssetsPath.SlotLibrary);
            const string path="Assets/TickToTalk/Resources/WardrobeCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<WardrobeCatalog>(path);
            if(!catalog) {catalog=ScriptableObject.CreateInstance<WardrobeCatalog>();AssetDatabase.CreateAsset(catalog,path);}
            var labels=new System.Collections.Generic.Dictionary<string,string>{{"Body","체형"},{"Faces","표정"},{"Outerwear","상의 / 의상"},{"Pants","하의"},{"Accessories","액세서리"},{"Glasses","안경"},{"Gloves","장갑"},{"Hairstyle","헤어"},{"Hat","모자"},{"Shoes","신발 / 양말"}};
            var categories=source.Slots.Select(slot=>new WearCategory {
                slot=slot.Type.ToString(),label=labels.ContainsKey(slot.Type.ToString())?labels[slot.Type.ToString()]:slot.Type.ToString(),required=slot.Type==SlotType.Body||slot.Type==SlotType.Faces,
                options=slot.Groups.SelectMany(group=>group.Variants.Select(go=>new WearOption {name=go.name,group=group.Type.ToString(),pieces=new[]{new WearPiece{slot=slot.Type.ToString(),mesh=go.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh}}})).ToArray()
            }).ToList();
            categories.Add(new WearCategory{slot="FullBody",label="전신 코스튬",options=source.FullBodyCostumes.Select((entry,i)=>new WearOption{name="코스튬 "+(i+1),group="FullBody",pieces=entry.Slots.Select(s=>new WearPiece{slot=s.Type.ToString(),mesh=s.GameObject.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh}).ToArray()}).ToArray()});
            catalog.categories=categories.ToArray();catalog.basePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TickToTalk/Prefabs/Player_A.prefab");catalog.material=new MaterialProvider().MainColor;
            EditorUtility.SetDirty(catalog);
            FixJump(); AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText("Logs/FrontEndAssets.txt","Wardrobe categories="+categories.Count+" options="+categories.Sum(c=>c.options.Length)+". Every SlotLibrary variant and full-body costume included. Jump body translation normalized; anticipation trimmed.");
        }
        public static void FixJump()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/TickToTalk/PlayerMotion.controller");
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/Idle_Breathing.anim");
            var roots=AnimationUtility.GetCurveBindings(idle).Where(b=>b.propertyName.StartsWith("RootT.")).ToDictionary(b=>b.propertyName,b=>AnimationUtility.GetEditorCurve(idle,b).Evaluate(0));
            foreach(var child in controller.layers[0].stateMachine.states) {
                var state=child.state;if(!state.name.StartsWith("Jump_"))continue;
                var original=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ithappy/Creative_Characters_FREE/Animations/Other_Animations/"+state.name+".anim");
                string path="Assets/TickToTalk/"+state.name+"_InPlace.anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(!clip){clip=UnityEngine.Object.Instantiate(original);AssetDatabase.CreateAsset(clip,path);}
                else EditorUtility.CopySerialized(original,clip);
                clip.name=state.name+"_InPlace";
                float start=state.name=="Jump_Start"?original.length*.70f:state.name=="Jump_End"?original.length*.40f:0;
                float duration=original.length-start;
                foreach(var binding in AnimationUtility.GetCurveBindings(original)) {
                    var curve=AnimationUtility.GetEditorCurve(original,binding);
                    if(roots.TryGetValue(binding.propertyName,out float baseline)) curve=AnimationCurve.Constant(0,duration,baseline);
                    else {
                        var keys=curve.keys.Where(k=>k.time>start && k.time<original.length).Select(k=>{k.time-=start;return k;}).ToList();
                        keys.Insert(0,new Keyframe(0,curve.Evaluate(start)));keys.Add(new Keyframe(duration,curve.Evaluate(original.length)));
                        curve=new AnimationCurve(keys.ToArray());
                    }
                    AnimationUtility.SetEditorCurve(clip,binding,curve);
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.startTime=0;settings.stopTime=duration;
                settings.loopTime=state.name=="Jump_Loop";settings.keepOriginalPositionY=true;settings.loopBlendPositionY=true;settings.loopBlendPositionXZ=true;settings.loopBlendOrientation=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                state.motion=clip;state.speed=state.name=="Jump_Start"?duration/PlayerMotionState.TakeoffDuration:state.name=="Jump_End"?duration/PlayerMotionState.LandingDuration:1;
                EditorUtility.SetDirty(clip);EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
        }
    }
}
