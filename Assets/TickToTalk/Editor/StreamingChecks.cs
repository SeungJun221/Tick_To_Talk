using System;
using UnityEngine;
namespace TickToTalk.Editor {
 public static class StreamingChecks {
  static int checks;
  static void Check(bool v,string name){checks++;if(!v)throw new Exception(name);}
  public static string Run(){
   checks=0;
   var m=new MotionBuffer();float yaw;
   m.Push(Vector3.zero,350,0);m.Push(Vector3.right,10,.05f);
   Check(Mathf.Abs(m.Sample(.025f,out yaw).x-.5f)<.001f,"half-frame interpolation");
   Check(Mathf.Abs(Mathf.DeltaAngle(yaw,0))<.01f,"yaw wraps through zero");
   m.Push(Vector3.one,20,.1f);m.Push(Vector3.one*100,90,.09f);
   Check(m.Sample(1,out yaw)==Vector3.one,"late frame rejected; loss holds last position");
   var go=new GameObject("Streaming check");var voice=go.AddComponent<TableVoice>();voice.enabled=false;
   try {
    Func<int,float[]> packet = n => {var a=new float[320];for(int i=0;i<320;i++)a[i]=.2f;return a;};
    for(uint i=0;i<6;i++)voice.Receive(packet((int)i),i);
    var output=new float[1920];int silent=0;
    for(uint i=6;i<306;i++) {
     // Every 17th packet is lost; consecutive pairs arrive in reverse order.
     if(i%2==0) {if((i+1)%17!=0)voice.Receive(packet((int)i+1),i+1);if(i%17!=0)voice.Receive(packet((int)i),i);}
     voice.RenderAudio(output,2);
     float energy=0;foreach(var sample in output)energy+=sample*sample;
     if(energy<.001f)silent++;
    }
    Check(silent==0,"6 seconds reordered audio plus packet loss remains continuous: "+silent);
    Check(voice.ReceivedPackets>275,"reordered packets retained");
    voice.Receive(packet(1),1);Check(voice.ReceivedPackets<310,"late packet rejected");
    for(int i=0;i<30;i++)voice.RenderAudio(output,2);
    float tail=0;foreach(var sample in output)tail+=Mathf.Abs(sample);
    Check(tail==0,"playback drains to silence at talk end");
    voice.ResetPlayback();Check(voice.ReceivedPackets==0,"disconnect resets audio");
    for(uint i=0;i<6;i++)voice.Receive(packet((int)i),i);voice.RenderAudio(output,2);
    Check(output[100]>.1f,"new session accepts sequence zero");
   }finally{UnityEngine.Object.DestroyImmediate(go);}
   string result="PASS "+checks+" streaming checks: interpolation, angle wrap, stale motion rejection, 6s audio reorder/loss, tail, reconnect.";
   System.IO.File.WriteAllText("Logs/StreamingChecks.txt",result);return result;
  }
 }
}
