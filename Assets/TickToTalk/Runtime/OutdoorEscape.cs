using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace TickToTalk {
 public sealed partial class CellSequence {
  readonly List<GameObject> cellContents=new List<GameObject>();
  GameObject meadow; bool outsideShown;
  Color oldAmbient,oldFog,oldBackground;float oldDensity;CameraClearFlags oldClear;
  void BuildMeadow(){
   // Only the holding cells disappear. Door frames and the puzzle rooms remain.
   foreach(Transform child in root){string n=child.name;if(n.StartsWith("Cell ")||n.StartsWith("Bed ")||n.StartsWith("Pillow")||n.StartsWith("Folded blanket")||n=="Vent slat")cellContents.Add(child.gameObject);}
   oldAmbient=RenderSettings.ambientLight;oldFog=RenderSettings.fogColor;oldDensity=RenderSettings.fogDensity;oldBackground=p.eye.backgroundColor;oldClear=p.eye.clearFlags;
   meadow=new GameObject("Escape meadow");meadow.transform.SetParent(root,false);
   var grass=Mat("Meadow grass",new Color(.27f,.44f,.13f));grass.SetFloat("_Smoothness",0);
   var soil=Mat("Meadow path",new Color(.48f,.43f,.28f));soil.SetFloat("_Smoothness",0);
   var bark=Mat("Tree bark",new Color(.22f,.16f,.1f));var leaves=Mat("Tree foliage",new Color(.17f,.34f,.12f));
   var floor=Cube("Outdoor grass ground",new Vector3(0,-.2f,-52),new Vector3(180,.4f,96),grass);floor.transform.SetParent(meadow.transform,true);
   foreach(float x in new[]{-1.7f,1.7f}){var path=Cube("Worn grass trail",new Vector3(x,.003f,-9),new Vector3(1.35f,.006f,10),soil,false);path.transform.SetParent(meadow.transform,true);}
   bark.SetFloat("_Smoothness",0);leaves.SetFloat("_Smoothness",0);
   var random=new System.Random(741);
   var verts=new List<Vector3>();var tris=new List<int>();var colors=new List<Color>();
   for(int i=0;i<18000;i++){
    float x=(float)random.NextDouble()*110-55,z=-4.3f-(float)random.NextDouble()*80;
    if(z>-15 && (Mathf.Abs(x-1.7f)<.82f||Mathf.Abs(x+1.7f)<.82f))continue;
    float h=.08f+(float)random.NextDouble()*.2f;float a=(float)random.NextDouble()*Mathf.PI;
    Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.045f;Vector3 b=new Vector3(x,.01f,z);
    int k=verts.Count;verts.Add(b-d);verts.Add(b+d);verts.Add(b+new Vector3(.04f,h,0));tris.Add(k);tris.Add(k+1);tris.Add(k+2);tris.Add(k+2);tris.Add(k+1);tris.Add(k);
    Color c=Color.Lerp(new Color(.22f,.34f,.08f),new Color(.47f,.59f,.2f),(float)random.NextDouble());colors.Add(c);colors.Add(c);colors.Add(c);
   }
   var mesh=new Mesh{name="Meadow grass blades",indexFormat=IndexFormat.UInt32};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.SetColors(colors);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
   var blades=new GameObject("Grass blades");blades.transform.SetParent(meadow.transform,false);blades.AddComponent<MeshFilter>().sharedMesh=mesh;blades.AddComponent<MeshRenderer>().sharedMaterial=grass;
   for(int i=0;i<48;i++){
    float x=(float)random.NextDouble()*100-50,z=-20-(float)random.NextDouble()*65;float h=3+(float)random.NextDouble()*4;
    var trunk=Cube("Distant tree trunk",new Vector3(x,h*.5f,z),new Vector3(.4f,h,.4f),bark,false);trunk.transform.SetParent(meadow.transform,true);
    var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere);crown.name="Tree crown";crown.transform.SetParent(meadow.transform,false);crown.transform.position=new Vector3(x,h,z);crown.transform.localScale=new Vector3(h*.85f,h*.8f,h*.85f);crown.GetComponent<Renderer>().sharedMaterial=leaves;crown.GetComponent<Collider>().enabled=false;Destroy(crown.GetComponent<Collider>());
   }
   var sun=new GameObject("Outdoor daylight").AddComponent<Light>();sun.transform.SetParent(meadow.transform,false);sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.color=new Color(1,.96f,.85f);sun.intensity=1.2f;sun.shadows=LightShadows.Soft;
   // Tall prison bars join the building to a point beyond the escape finish line.
   foreach(float side in new[]{-7.5f,7.5f}){
    var fence=new GameObject("Meadow prison fence");fence.transform.SetParent(meadow.transform,false);
    var barrier=fence.AddComponent<BoxCollider>();barrier.center=new Vector3(side,2.75f,-17);barrier.size=new Vector3(.22f,5.5f,26.4f);
    for(int i=0;i<=65;i++){
     float z=-3.8f-i*.4f;
     var bar=Cube("Tall iron bar",new Vector3(side,2.75f,z),new Vector3(.08f,5.5f,.08f),metal,false);bar.transform.SetParent(fence.transform,true);
    }
    foreach(float y in new[]{.35f,2.8f,5.3f}){
     var rail=Cube("Iron cross rail",new Vector3(side,y,-16.8f),new Vector3(.15f,.13f,26.2f),metal,false);rail.transform.SetParent(fence.transform,true);
    }
    for(int i=0;i<=8;i++){
     var post=Cube("Iron fence pillar",new Vector3(side,2.85f,-3.8f-i*3.25f),new Vector3(.22f,5.7f,.22f),metal,false);post.transform.SetParent(fence.transform,true);
    }
   }
   meadow.SetActive(false);
  }
  void ShowMeadow(bool show){
   if(!meadow||outsideShown==show)return;outsideShown=show;
   foreach(var go in cellContents)if(go)go.SetActive(!show);
   meadow.SetActive(show);
   RenderSettings.ambientLight=show?new Color(.55f,.62f,.67f):oldAmbient;
   RenderSettings.fogColor=show?new Color(.64f,.78f,.86f):oldFog;RenderSettings.fogDensity=show?.008f:oldDensity;
   p.eye.clearFlags=show?CameraClearFlags.SolidColor:oldClear;p.eye.backgroundColor=show?new Color(.53f,.73f,.87f):oldBackground;
  }
 }
}
