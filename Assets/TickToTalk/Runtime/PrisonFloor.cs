using System.Collections.Generic;
using UnityEngine;
namespace TickToTalk {
 public sealed partial class CellSequence {
  readonly List<Transform> floorGates=new List<Transform>();
  readonly List<BoxCollider> gateColliders=new List<BoxCollider>();
  readonly float[] floorLift=new float[4];
  readonly List<Renderer> stageLamps=new List<Renderer>();
  Transform floorRoot;
  void BuildPrisonFloor(){
   floorRoot=new GameObject("Prison first floor / four puzzle sectors").transform;floorRoot.SetParent(root,false);
   var originals=Object.FindObjectsByType<Transform>();
   foreach(var t in originals)if(t.name=="North concrete wall"||t.name=="Concrete block joint"||t.name=="Concrete vertical joint")t.gameObject.SetActive(false);
   var floor=Cube("Continuous prison floor",new Vector3(0,-.21f,13.6f),new Vector3(16,.4f,35.8f),concrete);floor.transform.SetParent(floorRoot,true);
   foreach(int side in new[]{-1,1}){var wall=Cube("Continuous outer wall",new Vector3(side*8,2,13.6f),new Vector3(.35f,4,35.8f),concrete);wall.transform.SetParent(floorRoot,true);}
   string[] copyNames={"West concrete wall","East concrete wall","Ceiling","Ceiling beam","Floor seam","Floor longitudinal joint","REINFORCED GLASS / acoustic isolation","Glass vertical reinforcement","Glass horizontal rail","Glass safety stripe","Glass bolt","Bomb workbench","Bomb armored housing","Charge cylinder","Bomb terminal display","Terminal wire","Encrypted manual cabinet","Manual screen","Manual status mounting strip","Bomb warning mounting strip","Cell softbox","Station light"};
   for(int stage=1;stage<4;stage++){
    foreach(var original in originals){
     if(System.Array.IndexOf(copyNames,original.name)<0)continue;
     var clone=Instantiate(original.gameObject,floorRoot);clone.name=original.name+" / sector "+(stage+1);clone.transform.position=original.position+Vector3.forward*(stage*9);clone.SetActive(true);
     // Close the seams between the repeated rooms.
     if(original.name=="West concrete wall"||original.name=="East concrete wall"||original.name=="Ceiling"||original.name=="REINFORCED GLASS / acoustic isolation"){
      var scale=clone.transform.localScale;scale.z=9;clone.transform.localScale=scale;
     }
    }
   }
   // Keep each entrance open while backing the mirror against the south partition.
   for(int stage=1;stage<4;stage++){
    float wallZ=-4+stage*9;
    foreach(int side in new[]{-1,1}){
     var mirrorWall=Cube("Sector south mirror wall",new Vector3(side*5.3f,2,wallZ-.205f),new Vector3(5.05f,4,.76f),concrete);
     mirrorWall.transform.SetParent(floorRoot,true);
    }
   }
   // The TV and mirror share a solid wall section; the center opening stays clear for the gate.
   for(int stage=0;stage<4;stage++){
    float wallZ=4+stage*9;
    foreach(int side in new[]{-1,1}){
     var wall=Cube("Sector north TV wall",new Vector3(side*5.3f,2,wallZ),new Vector3(5.05f,4,.35f),concrete);
     wall.transform.SetParent(floorRoot,true);
     wall=Cube("Sector north inner wall",new Vector3(side*.4f,2,wallZ),new Vector3(.8f,4,.35f),concrete);
     wall.transform.SetParent(floorRoot,true);
     wall=Cube("Sector north gate lintel",new Vector3(side*1.7f,3.6f,wallZ),new Vector3(1.9f,.8f,.35f),concrete);
     wall.transform.SetParent(floorRoot,true);
    }
   }
   // Each lane has one upward-sliding gate at the end of each sector.
   for(int stage=0;stage<4;stage++){
    float z=NetworkSession.SectorBoundary(stage);
    foreach(int side in new[]{-1,1}){
     float x=side*1.7f;
     var wall=Cube("Sector partition outer",new Vector3(side*5.25f,2,z),new Vector3(5.5f,4,.3f),concrete);wall.transform.SetParent(floorRoot,true);
     wall=Cube("Sector partition inner",new Vector3(side*.35f,2,z),new Vector3(.7f,4,.3f),concrete);wall.transform.SetParent(floorRoot,true);
     wall=Cube("Sector gate lintel",new Vector3(x,3.6f,z),new Vector3(1.95f,.8f,.3f),metal);wall.transform.SetParent(floorRoot,true);
     var gate=new GameObject("Sector "+(stage+1)+" gate "+side).transform;gate.SetParent(floorRoot,false);gate.position=new Vector3(x,0,z);
     var col=gate.gameObject.AddComponent<BoxCollider>();col.center=new Vector3(0,1.6f,0);col.size=new Vector3(1.9f,3.2f,.18f);
     for(int bar=0;bar<9;bar++){
      var g=Cube("Sector iron bar",new Vector3(x-.84f+bar*.21f,1.6f,z),new Vector3(.065f,3.2f,.09f),metal,false);g.transform.SetParent(gate,true);
     }
     foreach(float y in new[]{.1f,1.1f,3.1f}){var g=Cube("Sector gate rail",new Vector3(x,y,z),new Vector3(1.9f,.09f,.12f),metal,false);g.transform.SetParent(gate,true);}
     floorGates.Add(gate);gateColliders.Add(col);
     var lamp=Cube("Sector release lamp",new Vector3(x,3.6f,z-.18f),new Vector3(.45f,.12f,.06f),Mat("Sector lamp",Color.red,true),false);lamp.transform.SetParent(floorRoot,true);stageLamps.Add(lamp.GetComponent<Renderer>());
    }
   }
   // Existing meadow points south. Turn it around to connect to sector four.
   meadow.transform.localRotation=Quaternion.Euler(0,180,0);meadow.transform.localPosition=new Vector3(0,0,27.5f);
  }
  void UpdatePrisonFloor(PlayerView view){
   if(floorGates.Count!=8||gateColliders.Count!=8||stageLamps.Count!=8)return;
   int completed=view==null?0:(view.phase==4||view.phase==2?4:Mathf.Min(view.stageA,view.stageB));
   for(int stage=0;stage<4;stage++){
    floorLift[stage]=view==null?0:Mathf.MoveTowards(floorLift[stage],completed>stage?1:0,Time.deltaTime*.7f);
    for(int lane=0;lane<2;lane++){
     int i=stage*2+lane;var position=floorGates[i].position;position.y=floorLift[stage]*3.5f;floorGates[i].position=position;
     gateColliders[i].enabled=floorLift[stage]<.93f;
     var mat=stageLamps[i].sharedMaterial;Color color=completed>stage?new Color(.1f,.8f,.25f):new Color(.65f,.08f,.025f);mat.color=color;mat.SetColor("_EmissionColor",color*1.5f);
    }
   }
  }
 }
 public sealed partial class NetworkSession {
  public static float SectorBoundary(int stage)=>4.25f+stage*9;
  public static int PuzzleSector(int slot){
   var s=Instance;if(!s)return 0;
   if((s.IsTestMode||(s.net&&s.net.IsServer))&&s.Rules.Bombs!=null)return Mathf.Clamp(s.Rules.Bombs[slot].Stage,0,3);
   return s.View==null?0:Mathf.Clamp(slot==0?s.View.stageA:s.View.stageB,0,3);
  }
  internal bool SectorOpen(int stage)=>Rules.Phase==4||Rules.Phase==2||(Rules.Bombs!=null&&Mathf.Min(Rules.Bombs[0].Stage,Rules.Bombs[1].Stage)>stage);
 }
}
