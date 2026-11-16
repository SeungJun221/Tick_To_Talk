using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
namespace TickToTalk.Editor {
 public static class AutoToolsChecks {
  public static async void Run(){
   int n=0;Action<bool,string> check=(v,m)=>{n++;if(!v)throw new Exception(m);};var s=NetworkSession.Instance;
   try{
    s.StartTestMode();check(s.View.phase==0,"Must begin sitting/standing wait");
    double deadline=Time.realtimeSinceStartupAsDouble+10;while(!s.View.readyA && Time.realtimeSinceStartupAsDouble<deadline)await Task.Delay(30);check(s.Rules.Phase==0,"Door opened before post-stand delay");
    check(s.View.readyA&&s.View.autoStartRemaining>1,"Standing did not auto-ready and count down");
    double readyTime=Time.realtimeSinceStartupAsDouble;await Task.Delay(900);check(s.Rules.Phase==0,"Door did not wait two seconds");while(s.View.phase==0 && Time.realtimeSinceStartupAsDouble<readyTime+5)await Task.Delay(30);check(Time.realtimeSinceStartupAsDouble-readyTime>=1.8,"Countdown shorter than two seconds");check(s.Rules.Phase==1&&s.View.departureOpen,"Automatic start failed");
    check(s.View.remaining>298&&s.View.remaining<=300,"Timer began too early");
    check(s.TestSolvePuzzle(0)&&s.Rules.Bombs[0].Stage==1&&s.Rules.Bombs[1].Stage==0,"Remote A success changed B");
    check(s.TestSolvePuzzle(1)&&s.Rules.Bombs[1].Stage==1,"Remote B success failed");
    check(!s.TestSolvePuzzle(-1)&&!s.TestSolvePuzzle(2),"Invalid room accepted");
    s.TestSolvePuzzle(0,true);check(s.Rules.Bombs[0].Armed&&s.Rules.Phase==1,"A bomb alone won");
    s.Rules.Bombs[0].ArmedAt=Time.realtimeSinceStartupAsDouble-20;
    s.TestSolvePuzzle(0,true);s.TestSolvePuzzle(1,true);
    check(s.Rules.Phase==4,"Both solve failed after stale arming");
    var field=typeof(NetworkSession).GetField("<IsTestMode>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic);field.SetValue(s,false);
    check(!s.TestSolvePuzzle(0,true),"Developer bypass available outside solo");field.SetValue(s,true);
    System.IO.File.WriteAllText("Logs/AutoToolsChecks.txt","PASS "+n+" checks: real stand + two-second countdown, timer, remote A/B, independent stages, stale-arm recovery, both unlock, non-test restriction.");
   }catch(Exception e){System.IO.File.WriteAllText("Logs/AutoToolsChecks.txt","FAIL "+e);Debug.LogException(e);}
  }
 }
}
