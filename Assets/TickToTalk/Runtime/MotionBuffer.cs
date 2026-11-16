using System.Collections.Generic;
using UnityEngine;
namespace TickToTalk {
 public sealed class MotionBuffer {
  struct Frame { public float time, yaw; public Vector3 position; }
  readonly List<Frame> frames = new List<Frame>();
  public void Clear() { frames.Clear(); }
  public void Push(Vector3 position,float yaw,float time) {
   if(frames.Count>0 && time<=frames[frames.Count-1].time)return;
   frames.Add(new Frame{position=position,yaw=yaw,time=time});
   if(frames.Count>32)frames.RemoveAt(0);
  }
  public Vector3 Sample(float time,out float yaw) {
   yaw=0; if(frames.Count==0)return Vector3.zero;
   while(frames.Count>2 && frames[1].time<=time)frames.RemoveAt(0);
   var a=frames[0]; if(frames.Count==1 || time<=a.time){yaw=a.yaw;return a.position;}
   var b=frames[1]; float t=Mathf.Clamp01((time-a.time)/(b.time-a.time));
   yaw=Mathf.LerpAngle(a.yaw,b.yaw,t);return Vector3.Lerp(a.position,b.position,t);
  }
 }
}
