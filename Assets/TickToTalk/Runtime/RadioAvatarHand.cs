using System.Collections.Generic;
using UnityEngine;
namespace TickToTalk {
 public sealed partial class WalkiePose {
  sealed class HandSurface {
   public SkinnedMeshRenderer source;public Mesh original,baked,visible;public GameObject go;
   public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
  }
  readonly List<HandSurface> handSurfaces=new List<HandSurface>();
  bool handBuilt;
  void BuildAvatarHand(){
   foreach(var old in handSurfaces){Destroy(old.go);owned.Remove(old.baked);owned.Remove(old.visible);Destroy(old.baked);Destroy(old.visible);}handSurfaces.Clear();
   var forearm=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
   if(!forearm)return;
   foreach(var source in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
    var mesh=source.sharedMesh;if(!mesh||!source.gameObject.activeInHierarchy)continue;
    var weights=mesh.boneWeights;var bones=source.bones;var selected=new bool[mesh.vertexCount];
    for(int i=0;i<weights.Length;i++){
     var w=weights[i];float sum=0;
     int[] ids={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};float[] values={w.weight0,w.weight1,w.weight2,w.weight3};
     for(int j=0;j<4;j++)if(ids[j]<bones.Length&&bones[ids[j]]&&(bones[ids[j]]==forearm||bones[ids[j]].IsChildOf(forearm)))sum+=values[j];
     selected[i]=sum>.45f;
    }
    var triangles=new List<int[]>();int total=0;
    for(int sub=0;sub<mesh.subMeshCount;sub++){
     var src=mesh.GetTriangles(sub);var dst=new List<int>();
     for(int i=0;i<src.Length;i+=3)if(selected[src[i]]&&selected[src[i+1]]&&selected[src[i+2]]){dst.Add(src[i]);dst.Add(src[i+1]);dst.Add(src[i+2]);}
     triangles.Add(dst.ToArray());total+=dst.Count;
    }
    if(total==0)continue;
    var surface=new HandSurface{source=source,original=mesh,baked=new Mesh(),visible=new Mesh{name="Actual avatar left hand"}};
    surface.visible.indexFormat=mesh.indexFormat;surface.visible.vertices=mesh.vertices;surface.visible.uv=mesh.uv;surface.visible.subMeshCount=mesh.subMeshCount;
    for(int sub=0;sub<triangles.Count;sub++)surface.visible.SetTriangles(triangles[sub],sub);
    surface.go=new GameObject("Avatar left forearm and fingers");surface.go.layer=29;surface.go.transform.SetParent(firstPerson,false);
    surface.go.AddComponent<MeshFilter>().sharedMesh=surface.visible;var renderer=surface.go.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    owned.Add(surface.baked);owned.Add(surface.visible);handSurfaces.Add(surface);
   }
   handBuilt=true;
  }
  void UpdateAvatarHand(){
   if(!handBuilt)BuildAvatarHand();
   foreach(var surface in handSurfaces){
    if(!surface.source||surface.source.sharedMesh!=surface.original){surface.go.SetActive(false);continue;}
    surface.go.SetActive(surface.source.gameObject.activeInHierarchy);
    surface.source.BakeMesh(surface.baked);
    surface.baked.GetVertices(surface.vertices);surface.baked.GetNormals(surface.normals);
    var matrix=handset.worldToLocalMatrix*surface.source.transform.localToWorldMatrix;
    // Preserve the hand and wrist exactly. Extend only the cropped elbow out of view.
    Vector3 wrist=handset.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
    Vector3 elbow=handset.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position);
    Vector3 arm=elbow-wrist;
    Vector3 extension=wrist+new Vector3(-.16f,-.65f,-.4f)-elbow;
    for(int i=0;i<surface.vertices.Count;i++){
     Vector3 vertex=matrix.MultiplyPoint3x4(surface.vertices[i]);
     float along=Vector3.Dot(vertex-wrist,arm.normalized);
     float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,arm.magnitude,along));
     surface.vertices[i]=vertex+extension*blend;
    }
    surface.visible.SetVertices(surface.vertices);surface.visible.RecalculateNormals();surface.visible.RecalculateBounds();
   }
  }
 }
}
