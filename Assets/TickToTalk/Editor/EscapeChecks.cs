using System;
using System.Reflection;
using UnityEngine;
namespace TickToTalk.Editor {
 public static class EscapeChecks {
  public static string Run(){
   var s=NetworkSession.Instance;var flags=BindingFlags.NonPublic|BindingFlags.Instance;int n=0;
   Action<bool,string> check=(ok,msg)=>{n++;if(!ok)throw new Exception(msg);};
   var type=typeof(NetworkSession);var update=type.GetMethod("Update",flags);var flow=type.GetMethod("UpdateEscapeFlow",flags);var begin=type.GetMethod("BeginCellRound",flags);
   var pos=(Vector2[])type.GetField("positions",flags).GetValue(s);var solo=type.GetField("<IsTestMode>k__BackingField",flags);var gate=type.GetField("gateLift",flags);var open=type.GetField("departureOpen",flags);
   s.StartTestMode();check(s.Rules.Phase==0&&s.View.phase==0,"Solo must wait for Move button");
   check(pos[0].y< -4,"Spawn must be behind exit");s.Command("ready");typeof(NetworkSession).GetField("autoStartAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,Time.realtimeSinceStartupAsDouble-1);typeof(NetworkSession).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);check(s.Rules.Phase==1,"Move starts timer");
   double now=Time.realtimeSinceStartupAsDouble;s.Rules.EndAt=now+120;update.Invoke(s,null);check(s.View.remaining<121,"Solo timer must not reset each frame");
   s.Rules.EndAt=now-1;update.Invoke(s,null);check(s.Rules.Phase==3,"Solo timer expires");
   begin.Invoke(s,null);solo.SetValue(s,false);gate.SetValue(s,1f);
   pos[0]=new Vector2(-4,-2.8f);pos[1]=new Vector2(4,-5.7f);flow.Invoke(s,null);check((bool)open.GetValue(s),"Gate closes with player still inside");
   pos[1]=new Vector2(4,-2.8f);flow.Invoke(s,null);check(!(bool)open.GetValue(s),"Both outside must close gate");
   gate.SetValue(s,0f);var from=new Vector2(-4,-3.2f);check(s.ConstrainCellMovement(0,from,new Vector2(-4,-4.5f))==from,"Closed portal permits crossing");
   now=Time.realtimeSinceStartupAsDouble;
   for(int stage=0;stage<4;stage++)for(int i=0;i<2;i++)s.Rules.Submit(i,s.Rules.Bombs[i].Answers[stage],now+stage);
   check(s.Rules.Phase==4,"Solved puzzles must unlock exit without victory");
   s.Rules.Tick(now+10000);check(s.Rules.Phase==4,"Disarmed bombs still time out");
   gate.SetValue(s,1f);flow.Invoke(s,null);check(s.Rules.Phase==4,"Victory before anyone crosses");
   pos[0]=new Vector2(-1.7f,30.8f);flow.Invoke(s,null);pos[0]=new Vector2(-1.7f,31.8f);flow.Invoke(s,null);check(s.Rules.Phase==4,"Must walk into the meadow before victory");
   check(s.ConstrainCellMovement(0,new Vector2(-4,35),new Vector2(-8,39))==new Vector2(-7.1f,39),"Outdoor fence boundary failed");
   pos[0]=new Vector2(-1.7f,40.2f);flow.Invoke(s,null);check(s.Rules.Phase==2&&pos[1].y>-3.1f,"One escaping player should win for both");
   solo.SetValue(s,true);s.Disconnect();s.StartTestMode();check(s.Rules.Phase==0&&s.View.gateLift==0&&!s.View.departureOpen,"New session retains open gate");s.Disconnect();
   string result="PASS "+n+" checks: waiting cells, Move button, solo countdown/timeout, both-out closure, closed-door collision, disarm-before-escape, one-player escape, restart.";
   System.IO.File.WriteAllText("Logs/EscapeChecks.txt",result);return result;
  }
 }
}
