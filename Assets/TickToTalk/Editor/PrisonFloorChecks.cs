using System;
using System.Reflection;
using UnityEngine;
namespace TickToTalk.Editor {
 public static class PrisonFloorChecks {
  public static string Run(){
   int checks=0;Action<bool,string> check=(ok,m)=>{checks++;if(!ok)throw new Exception(m);};
   var s=NetworkSession.Instance;var f=BindingFlags.Instance|BindingFlags.NonPublic;var t=s.GetType();
   s.StartTestMode();t.GetMethod("StartPreparedRound",f).Invoke(s,null);
   var update=t.GetMethod("Update",f);var flow=t.GetMethod("UpdateEscapeFlow",f);var move=t.GetMethod("MoveGround",f);
   var pos=(Vector2[])t.GetField("positions",f).GetValue(s);var input=(Vector2[])t.GetField("inputs",f).GetValue(s);var last=(double[])t.GetField("lastInput",f).GetValue(s);
   var cell=s.GetComponent<CellSequence>();var geometry=typeof(CellSequence).GetMethod("UpdatePrisonFloor",f);
   for(int stage=0;stage<4;stage++){
    float boundary=NetworkSession.SectorBoundary(stage);
    check(Mathf.Abs(NetworkSession.Bomb(0).z-stage*9)<.001f&&Mathf.Abs(NetworkSession.Manual(1).z-(stage*9+3.6f))<.001f,"Puzzle/hint target is in wrong sector");
    var before=new Vector2(-1.7f,boundary-.6f);check(s.ConstrainCellMovement(0,before,new Vector2(-1.7f,boundary+.6f))==before,"Locked sector bypass");
    s.Rules.Submit(0,s.Rules.Bombs[0].Answers[stage],Time.realtimeSinceStartupAsDouble);
    check(!s.SectorOpen(stage),"A alone opened sector");
    s.Rules.Submit(1,s.Rules.Bombs[1].Answers[stage],Time.realtimeSinceStartupAsDouble);
    check(s.SectorOpen(stage),"Both solutions failed to open sector");
    update.Invoke(s,null);for(int tick=0;tick<300;tick++)geometry.Invoke(cell,new object[]{s.View});Physics.SyncTransforms();
    for(int slot=0;slot<2;slot++){
     pos[slot]=new Vector2(slot==0?-1.7f:1.7f,boundary-1);input[slot]=Vector2.up;last[slot]=Time.realtimeSinceStartupAsDouble;
     for(int tick=0;tick<45;tick++)move.Invoke(s,new object[]{slot,.02f,true});input[slot]=Vector2.zero;
     check(pos[slot].y>boundary+.6f,"Open gate still physically blocks lane "+slot+" stage "+stage+" at "+pos[slot]);
    }
    if(stage<3)check(!s.SectorOpen(stage+1),"Next sector opened too early");
   }
   check(s.Rules.Phase==4,"Completion must allow walking outside");
   pos[0]=new Vector2(-4,-15);flow.Invoke(s,null);check(s.Rules.Phase==4,"Old south exit still wins");
   pos[0]=new Vector2(-1.7f,30.8f);flow.Invoke(s,null);pos[0]=new Vector2(-1.7f,40.2f);flow.Invoke(s,null);check(s.Rules.Phase==2,"North meadow escape failed");
   s.Disconnect();s.StartTestMode();update.Invoke(s,null);geometry.Invoke(cell,new object[]{s.View});check(!s.SectorOpen(0)&&s.View.phase==0,"Restart did not reset sectors");s.Disconnect();
   string result="PASS "+checks+" checks: ordered paired gates, physical traversal A/B, puzzle/hint locations, no old exit, north escape, restart.";System.IO.File.WriteAllText("Logs/PrisonFloorChecks.txt",result);return result;
  }
 }
}
