using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace TickToTalk.Editor
{
    public static class TabletChecks
    {
        public static void Prepare()
        {
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/TickToTalk/PlayerMotion.controller");
            var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        public static async void Run()
        {
            var s=NetworkSession.Instance;var p=s.GetComponent<PrisonPresentation>();var f=BindingFlags.Instance|BindingFlags.NonPublic;
            int count=0;
            Action<bool,string> check=(ok,msg)=>{count++;if(!ok)throw new Exception(msg);};
            try
            {
                s.StartTestMode();s.Command("ready");typeof(NetworkSession).GetField("autoStartAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,Time.realtimeSinceStartupAsDouble-1);typeof(NetworkSession).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);await Task.Delay(150);p.OpenTablet();
                var create=typeof(NetworkSession).GetMethod("CreateView",f);var handle=typeof(NetworkSession).GetMethod("HandleCommand",f);
                var view=(PlayerView)create.Invoke(s,new object[]{1});
                check(view.tabletA&&!view.tabletB,"Partner snapshot does not carry A tablet flag");
                handle.Invoke(s,new object[]{1,new ClientCommand{kind="tablet",answer="1"}});
                view=JsonUtility.FromJson<PlayerView>(JsonUtility.ToJson(create.Invoke(s,new object[]{0})));
                check(view.tabletA&&view.tabletB,"Both roles' tablet flags did not survive serialization");
                s.Command("move","",Vector2.one,0,true);s.Command("jump");
                var inputs=(Vector2[])typeof(NetworkSession).GetField("inputs",f).GetValue(s);var speed=(float[])typeof(NetworkSession).GetField("verticalSpeed",f).GetValue(s);
                check(inputs[0]==Vector2.zero&&speed[0]<=0,"Typing can move/jump player");
                check(!JsonUtility.ToJson(view).Contains("strokes"),"Private handwriting sent in snapshot");
                await Task.Delay(800);
                foreach(var actor in new[]{p.playerA,p.playerB})
                {
                    var pose=actor.GetComponentInChildren<TabletPose>();var anim=actor.GetComponentInChildren<Animator>();
                    check(pose.HoldWeight>.9f&&pose.Prop.gameObject.activeSelf,"Hold pose not raised");
                    foreach(var bone in new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                    {
                        float edge=bone==HumanBodyBones.LeftHand?-.27f:.27f;
                        float distance=Vector3.Distance(anim.GetBoneTransform(bone).position,pose.Prop.TransformPoint(new Vector3(edge,-.01f,0)));
                        check(distance<.13f,"Hand does not reach tablet: "+bone+" distance="+distance);
                    }
                }
                p.CloseTablet();check(!p.TabletOpen,"Tablet did not close");
                view=(PlayerView)create.Invoke(s,new object[]{1});check(!view.tabletA&&view.tabletB,"Closing A also cleared B");
                handle.Invoke(s,new object[]{1,new ClientCommand{kind="tablet",answer="0"}});
                await Task.Delay(500);check(p.playerA.GetComponentInChildren<TabletPose>().HoldWeight==0,"Pose did not lower");
                s.Disconnect();check(!p.TabletOpen,"Tablet remained open after close");
                System.IO.File.WriteAllText("Logs/TabletChecks.txt","PASS "+count+" checks: both-role serialized hold flags, private handwriting, movement/jump gating, both hands reach tablet on both avatars, close/release.");
            }
            catch(Exception e){System.IO.File.WriteAllText("Logs/TabletChecks.txt","FAIL "+e);Debug.LogException(e);}
            finally{p.CloseTablet();s.Disconnect();}
        }
    }
}
