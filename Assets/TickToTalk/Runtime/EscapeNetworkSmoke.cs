#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
namespace TickToTalk {
 public sealed class EscapeNetworkSmoke:MonoBehaviour {
  float start,next,wait=-1,unlocked=-1;bool sawDeparture,sawClosed,solved,done;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Install(){if(Environment.GetCommandLineArgs().Contains("-escape-smoke"))new GameObject("Escape flow test").AddComponent<EscapeNetworkSmoke>();}
  void Start(){start=Time.realtimeSinceStartup;NetworkSession.Instance.GetComponent<PrisonPresentation>().enabled=false;}
  void Finish(){Application.Quit();}
  void Fail(string reason){Debug.LogError("TTT_ESCAPE_SMOKE_FAIL "+reason);done=true;Invoke(nameof(Finish),5);}
  void Update(){
   if(done)return;float now=Time.realtimeSinceStartup;if(now-start>120){Fail("timeout");return;}
   var s=NetworkSession.Instance;var v=s.View;if(v==null||now<next)return;next=now+.05f;
   if(v.phase==0){if(wait<0)wait=now;if(now-wait>2)s.Command("ready");return;}
   float z=v.slot==0?v.az:v.bz;
   if(v.phase==1){
    sawDeparture|=v.departureOpen;
    if(z< -2.7f)s.Command("move","",Vector2.up,0);else s.Command("move");
    if(!v.departureOpen&&sawDeparture){sawClosed=true;
     if(v.remaining<296&&s.IsRoomHost&&!solved){solved=true;double t=Time.realtimeSinceStartupAsDouble;
      for(int stage=0;stage<4;stage++)for(int i=0;i<2;i++)s.Rules.Submit(i,s.Rules.Bombs[i].Answers[stage],t);
     }
    }return;
   }
   if(v.phase==4){if(!sawClosed){Fail("Gate did not close after both exits");return;}if(unlocked<0)unlocked=now;
    s.Command("move","",v.slot==0&&now-unlocked>2&&v.gateLift>.85f?Vector2.down:Vector2.zero,180);return;
   }
   if(v.phase==2){
    if(unlocked<0||!sawDeparture||!sawClosed||v.az> -4.65f||v.bz< -3.1f){Fail("Victory before exactly one crossed");return;}
    Debug.Log("TTT_ESCAPE_SMOKE_PASS slot="+v.slot+" shared door/timer/unlock/one-player crossing");done=true;Invoke(nameof(Finish),6);return;
   }
   Fail("unexpected phase "+v.phase);
  }
 }
}
#endif
