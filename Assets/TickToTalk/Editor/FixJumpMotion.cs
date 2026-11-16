using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

namespace TickToTalk.Editor
{
    public static class FixJumpMotion
    {
        public static void ApplyAndVerify()
        {
            PrepareFrontEnd.FixJump(); AssetDatabase.SaveAssets();
            int checks = 0;
            Action<bool,string> check = (ok, why) => { checks++; if(!ok) throw new Exception(why); };
            // Simulate 20Hz server snapshots rendered at several client frame rates.
            foreach (int fps in new[] { 20, 30, 60, 144 }) {
                var m = new PlayerMotionState(); string previous = "";
                var sequence = new System.Collections.Generic.List<string>();
                for(int frame=0; frame<fps*2; frame++) {
                    float t = frame/(float)fps;
                    float serverTime = (float)Math.Floor(t*20)/20;
                    float flight = serverTime - .2f;
                    float h = flight <= 0 ? 0 : Math.Max(0, 5.5f*flight - 8*flight*flight);
                    string state=m.Select(h, 0, false, t);
                    if(state != previous) { sequence.Add(state); previous=state; }
                }
                check(string.Join(",",sequence) == "Idle_Breathing,Jump_Start,Jump_Loop,Jump_End,Idle_Breathing", "Jump flicker or skipped landing at " + fps + " fps: " + string.Join(",", sequence));
            }
            var quick = new PlayerMotionState();
            check(quick.Select(.2f,0,false,0)=="Jump_Start", "Takeoff");
            check(quick.Select(0,0,false,.1f)=="Jump_End", "Short hop landing");
            check(quick.Select(.2f,0,false,.15f)=="Jump_Start", "Immediate second jump");
            quick.Reset(); check(quick.Select(0,2,true,5)=="Run_Forward", "Reconnect resets pose");
            System.IO.File.WriteAllText("Logs/JumpRevisionTests.txt", "PASS " + checks + " checks: 20Hz snapshots at 20/30/60/144fps; one takeoff, airborne loop, complete landing; short/repeated jump; reset. In-place jump clips retimed to .12s takeoff / .24s landing.");
        }
    }
}
