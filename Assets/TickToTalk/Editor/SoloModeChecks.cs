using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;

namespace TickToTalk.Editor
{
    public static class SoloModeChecks
    {
        public static void Run()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode required");
            var s=NetworkSession.Instance; var p=s.GetComponent<PrisonPresentation>();
            int checks=0; Action<bool,string> check=(ok,msg)=>{checks++;if(!ok)throw new Exception(msg);};
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var update=typeof(NetworkSession).GetMethod("Update",flags);
            var fly=typeof(NetworkSession).GetMethod("MoveTestFlight",flags);
            s.ToggleTestFlight();check(!s.TestFlying,"Flight allowed outside solo test");
            s.StartTestMode();
            check(s.IsTestMode && s.View.phase==0 && s.View.count==1,"Solo waiting room requires partner");
            s.Command("ready");typeof(NetworkSession).GetField("autoStartAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,Time.realtimeSinceStartupAsDouble-1);typeof(NetworkSession).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);update.Invoke(s,null);
            check(!s.GetComponent<NetworkManager>().IsListening && !s.UsingRelay,"Offline test started a network listener");
            s.ToggleTestFlight(); s.Command("move","",Vector2.right); s.SetTestVertical(1);
            fly.Invoke(s,new object[]{2f});update.Invoke(s,null);
            check(s.View.ax>0 && s.View.ay>5,"Flight cannot cross glass or gain altitude");
            float height=s.View.ay; s.Command("move");s.SetTestVertical(-1);fly.Invoke(s,new object[]{.25f});update.Invoke(s,null);
            check(s.View.ay<height,"Flight descent failed");
            p.OpenSettings();float x=s.View.ax;float y=s.View.ay;fly.Invoke(s,new object[]{1f});update.Invoke(s,null);
            check(Math.Abs(x-s.View.ax)<.01f && Math.Abs(y-s.View.ay)<.01f,"Flight continues in settings");p.CloseSettings();
            s.Rules.EndAt=Time.realtimeSinceStartupAsDouble+10;update.Invoke(s,null);
            check(s.View.phase==1 && s.View.remaining<=10,"Test timer is still frozen");
            float landingX=s.View.ax, landingZ=s.View.az;
            s.ToggleTestFlight();update.Invoke(s,null);
            check(!s.TestFlying&&Math.Abs(s.View.ax-landingX)<.01f&&Math.Abs(s.View.az-landingZ)<.01f&&s.View.ay<=y,"Landing changed horizontal position");
            s.Command("move","",Vector2.right);
            typeof(NetworkSession).GetMethod("MoveTestGround",flags).Invoke(s,new object[]{.1f});update.Invoke(s,null);
            check(s.View.ax>=landingX && s.View.ax>0,"Walking after landing returned to room A");
            s.Command("move");
            s.Disconnect();check(!s.IsTestMode&&!s.TestFlying&&s.View==null,"Test state leaked after exit");
            s.StartTestMode();check(s.View.phase==0&&s.View.strikes==0,"Test mode cannot restart");s.Disconnect();
            System.IO.File.WriteAllText("Logs/SoloModeChecks.txt","PASS "+checks+" checks: offline solo start, flight gate, glass crossing, ascent/descent, settings stop, running timer, landing in place and walking in room B, cleanup, restart.");
        }
    }
}
