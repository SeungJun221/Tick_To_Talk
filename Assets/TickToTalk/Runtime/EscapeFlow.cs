using System;
using UnityEngine;
namespace TickToTalk {
 public sealed partial class NetworkSession {
  bool departureOpen;
  float gateLift;
  double autoStartAt=-1;
  readonly bool[] escapeApproach=new bool[2];
  public static Vector2 CellSpawn(int slot) => new Vector2(slot==0?-4:4,-5.7f);
  void ResetCellFlow(){autoStartAt=-1;departureOpen=false;gateLift=0;escapeApproach[0]=escapeApproach[1]=false;}
  bool CanMovePlayer(int slot) => Rules.Phase==1 || Rules.Phase==4 || (Rules.Phase==0 && ready[slot]);
  void BeginCellRound(){
   bool preservePosition=Rules.Phase==0;
   ResetCellFlow();departureOpen=true;
   if(!preservePosition)for(int i=0;i<2;i++){positions[i]=CellSpawn(i);heights[i]=verticalSpeed[i]=0;inputs[i]=Vector2.zero;yaw[i]=0;}
   TestFlying=false;flightVertical=0;
   Rules.Start(Guid.NewGuid().GetHashCode(),Now);
  }
  void StartPreparedRound(){
   BeginCellRound();tablets[0]=tablets[1]=false;walkies[0]=walkies[1]=false;emotes[0]=emotes[1]=0;
   ready[0]=ready[1]=false;notices[0]=notices[1]="5분 안에 두 폭탄을 해체하세요.";
   Debug.Log("TTT_ROUND_STARTED");
  }
  void UpdateEscapeFlow(){
   if(Rules.Phase==0 && autoStartAt>=0){
    bool both=IsTestMode?ready[0]:(ready[0]&&ready[1]&&clients[0].HasValue&&clients[1].HasValue);
    if(!both)autoStartAt=-1;
    else if(Now>=autoStartAt)StartPreparedRound();
   }
   if(Rules.Phase==1 && departureOpen && positions[0].y>-3.1f && (IsTestMode || positions[1].y>-3.1f))departureOpen=false;
   bool open=Rules.Phase==1 && departureOpen;
   gateLift=Mathf.MoveTowards(gateLift,open?1:0,Time.deltaTime*.65f);
   if(Rules.Phase!=4)return;
   for(int i=0;i<(IsTestMode?1:2);i++){
    if(positions[i].y>26.5f && positions[i].y<32f)escapeApproach[i]=true;
    float x=IsTestMode?(positions[i].x<0?-4:4):(i==0?-4:4);
    if(escapeApproach[i] && SectorOpen(3) && Mathf.Abs(positions[i].x)<7.1f && positions[i].y>40f && heights[i]<1.5f && !TestFlying){
     Rules.Phase=2;Rules.Speaker=-1;inputs[0]=inputs[1]=Vector2.zero;notices[0]=notices[1]="탈출 성공";
    }
   }
  }
  // Shared server-side portal boundaries: a closed exit never permits walking through it.
  internal Vector2 ConstrainCellMovement(int slot,Vector2 from,Vector2 to){
   float x=slot==0?-4:4;bool outdoors=Rules.Phase==4 || Rules.Phase==2;to.y=Mathf.Clamp(to.y,-7.25f,outdoors?50f:31.15f);
   if(to.y< -4.3f)to.x=Mathf.Clamp(to.x,x-1.25f,x+1.25f);
   if(to.y>31.5f)to.x=Mathf.Clamp(to.x,-7.1f,7.1f);
   for(int sector=0;sector<4;sector++){
    float z=SectorBoundary(sector);bool crossing=(from.y-z)*(to.y-z)<=0;bool atGate=Mathf.Abs(to.y-z)<.45f;
    if((crossing||atGate)&&(!SectorOpen(sector)||Mathf.Abs(to.x-(slot==0?-1.7f:1.7f))>.62f))return from;
   }
   if(to.y< -3.25f && to.y> -4.3f && (Mathf.Abs(to.x-x)>.65f || gateLift<.85f))return from;
   // Also catch a large frame step crossing the entire doorway.
   if((from.y+3.8f)*(to.y+3.8f)<0 && (Mathf.Abs(to.x-x)>.65f || gateLift<.85f))return from;
   return to;
  }
 }
}
