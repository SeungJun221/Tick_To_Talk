using System.Collections.Generic;
using UnityEngine;
namespace TickToTalk {
 [DefaultExecutionOrder(500)]
 public sealed partial class CellSequence : MonoBehaviour {
  const float StandDuration=1.4f;
  PrisonPresentation p;NetworkSession session;bool wasConnected;bool standingSent;float stand=0;int lastPhase=-1;
  Transform root;readonly List<Object> owned=new List<Object>();
  Material concrete,metal,cloth,linen,dark;CellIdlePose idleA,idleB;string lastLook;
  public bool StandingReady=>stand>=1;
void Start()
{
    p=GetComponent<PrisonPresentation>();session=GetComponent<NetworkSession>();
    foreach(var sign in Object.FindObjectsByType<TextMesh>())
        if(sign.name=="Intercom instruction" || (sign.text.Contains("무전기")&&sign.text.Contains("10초")))
            sign.gameObject.SetActive(false);
    if(!BindBakedEnvironment()){BuildCells();BuildMeadow();BuildPrisonFloor();}
    idleA=PrepareIdlePose(p.playerA);
    idleB=PrepareIdlePose(p.playerB);
}

CellIdlePose PrepareIdlePose(Transform actor)
{
    var animator=actor.GetComponentInChildren<Animator>();
    var pose=animator.GetComponent<CellIdlePose>();
    if(!pose)pose=animator.gameObject.AddComponent<CellIdlePose>();
    pose.Initialize(actor);
    return pose;
}


bool BindBakedEnvironment()
{
    // Transform.Find treats '/' inside an object name as a hierarchy separator.
    // Match direct children by their exact names so the baked environment is reused.
    root=null;
    foreach(Transform child in transform)
        if(child.name=="Arrival cells / escape passages"){root=child;break;}
    if(!root)return false;

    meadow=null;
    foreach(Transform child in root)
        if(child.name=="Escape meadow"){meadow=child.gameObject;break;}

    floorGates.Clear();gateColliders.Clear();stageLamps.Clear();
    var descendants=root.GetComponentsInChildren<Transform>(true);
    for(int stage=1;stage<=4;stage++)
    {
        foreach(int side in new[]{-1,1})
        {
            string gateName="Sector "+stage+" gate "+side;
            Transform gate=null;
            foreach(var child in descendants)
                if(child.name==gateName){gate=child;break;}
            if(gate && gate.TryGetComponent<BoxCollider>(out var collider))
            {
                floorGates.Add(gate);
                gateColliders.Add(collider);
            }
        }
    }

    foreach(var child in descendants)
        if(child.name=="Sector release lamp"&&child.TryGetComponent<Renderer>(out var lamp))
            stageLamps.Add(lamp);
    stageLamps.Sort((a,b)=>a.transform.position.z!=b.transform.position.z
        ?a.transform.position.z.CompareTo(b.transform.position.z)
        :a.transform.position.x.CompareTo(b.transform.position.x));

    if(floorGates.Count!=8||gateColliders.Count!=8||stageLamps.Count!=8||!meadow)
        Debug.LogError("The baked prison environment is incomplete. Re-bake it from Tools > Tick To Talk.");
    return true;
}

  Material Mat(string name,Color c,bool emission=false){var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=c};
   if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*3);}owned.Add(m);return m;}
  GameObject Cube(string name,Vector3 pos,Vector3 size,Material mat,bool solid=true){
   var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
   if(!solid){go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());}return go;
  }
  void BuildCells(){
   root=new GameObject("Arrival cells / escape passages").transform;root.SetParent(transform,false);
   concrete=Mat("Cell concrete",new Color(.19f,.21f,.22f));metal=Mat("Cell iron",new Color(.08f,.1f,.11f));cloth=Mat("Worn mattress",new Color(.22f,.29f,.28f));linen=Mat("Bed linen",new Color(.52f,.5f,.41f));dark=Mat("Cell floor",new Color(.13f,.15f,.16f));
   foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
    if(t.name=="South concrete wall"||t.name=="Exit frame"||t.name=="Exit prison bar")t.gameObject.SetActive(false);
   // Retain the wall around actual apertures instead of leaving a solid wall behind each door.
   Cube("South west wall",new Vector3(-6.48f,2,-4),new Vector3(3.04f,4,.35f),concrete);
   Cube("South centre wall",new Vector3(0,2,-4),new Vector3(6.08f,4,.35f),concrete);
   Cube("South east wall",new Vector3(6.48f,2,-4),new Vector3(3.04f,4,.35f),concrete);
   foreach(int slot in new[]{0,1}){
    float x=slot==0?-4:4;
    Cube("Cell floor "+slot,new Vector3(x,-.18f,-6),new Vector3(3.3f,.36f,4.1f),dark);
    Cube("Cell rear wall "+slot,new Vector3(x,1.7f,-7.8f),new Vector3(3.3f,3.4f,.22f),concrete);
    foreach(int side in new[]{-1,1})Cube("Cell side wall",new Vector3(x+side*1.6f,1.7f,-6),new Vector3(.22f,3.4f,3.6f),concrete);
    Cube("Cell ceiling",new Vector3(x,3.45f,-6),new Vector3(3.3f,.18f,3.8f),concrete);
    Cube("Portal lintel",new Vector3(x,3.5f,-4),new Vector3(1.92f,1,.35f),concrete);
    foreach(int side in new[]{-1,1})Cube("Portal jamb",new Vector3(x+side*.91f,1.6f,-3.8f),new Vector3(.12f,3.2f,.28f),metal);
    Cube("Portal header",new Vector3(x,3.22f,-3.8f),new Vector3(1.94f,.16f,.28f),metal);
    Cube("Bed frame "+slot,new Vector3(x,.20f,-6.85f),new Vector3(2.15f,.16f,1.05f),metal);
    Cube("Bed mattress "+slot,new Vector3(x,.34f,-6.85f),new Vector3(2.08f,.17f,.99f),cloth);
    Cube("Pillow "+slot,new Vector3(x-.72f,.48f,-6.95f),new Vector3(.52f,.13f,.68f),linen);
    Cube("Folded blanket "+slot,new Vector3(x+.68f,.46f,-6.85f),new Vector3(.47f,.08f,.96f),linen);
    foreach(int side in new[]{-1,1})foreach(float z in new[]{-7.22f,-6.48f})Cube("Bed leg",new Vector3(x+side*.9f,.09f,z),new Vector3(.07f,.18f,.07f),metal);
    Cube("Cell lamp",new Vector3(x,3.2f,-6.5f),new Vector3(.6f,.08f,.25f),linen);
    var lamp=new GameObject("Cell warm light").AddComponent<Light>();lamp.transform.SetParent(root,false);lamp.transform.position=new Vector3(x,2.75f,-6.1f);lamp.type=LightType.Point;lamp.range=5;lamp.intensity=2;lamp.color=new Color(1,.82f,.61f);
    for(int k=0;k<7;k++)Cube("Vent slat",new Vector3(x-.9f+k*.13f,2.55f,-7.66f),new Vector3(.055f,.36f,.035f),metal,false);
    var door=slot==0?p.doorA:p.doorB;door.position=new Vector3(x,1.6f,-3.8f);
    // Keep the solid collision gate, replacing its visible slab with iron bars.
    var doorSkin=door.GetComponent<Renderer>();if(doorSkin)doorSkin.enabled=false;
    for(int k=0;k<8;k++){
     var bar=Cube("Sliding cell gate bar",new Vector3(x-.74f+k*.211f,1.6f,-3.8f),new Vector3(.065f,3.15f,.085f),metal,false);bar.transform.SetParent(door,true);
    }
    foreach(float y in new[]{.075f,1.2f,3.125f}){
     var rail=Cube("Sliding cell gate rail",new Vector3(x,y,-3.8f),new Vector3(1.65f,.105f,.1f),metal,false);rail.transform.SetParent(door,true);
    }

   }
  }
void LateUpdate()
{
    if(!session||!p||!idleA||!idleB)return;
    if(p.FailureActive)return;
    var v=session.View;
    // Start the same wake-up sequence only after a real player view exists.
    bool connected=v!=null;
    if(connected!=wasConnected){stand=0;standingSent=false;wasConnected=connected;}
    stand=connected?Mathf.MoveTowards(stand,1,Time.unscaledDeltaTime/StandDuration):0;

    if(v!=null && v.phase==0 && stand>=1 && !standingSent)
    {
        standingSent=true;
        session.Command("ready");
        if(!p.SettingsOpen&&!p.WardrobeOpen){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
    }
    if(v==null || v.phase!=0)standingSent=false;

    float seatedWeight=connected?1-Mathf.SmoothStep(0,1,stand):1;
    idleA.SeatedWeight=seatedWeight;
    idleB.SeatedWeight=seatedWeight;
    float armWeight=connected
        ?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.42f,stand))
        :1;
    idleA.ArmWeight=armWeight;
    idleB.ArmWeight=armWeight;

    if(!connected)
    {
        p.playerA.position=new Vector3(-4,0,-6.32f);p.playerA.rotation=Quaternion.identity;
        string look=PlayerPrefs.GetString("TTT_Outfit","");
        if(lastLook!=look)
        {
            var catalog=Resources.Load<WardrobeCatalog>("WardrobeCatalog");
            if(catalog)catalog.Apply(p.playerA,catalog.Decode(look));
            lastLook=look;
        }
        foreach(var renderer in p.playerA.GetComponentsInChildren<Renderer>())renderer.enabled=true;
        p.playerB.position=new Vector3(4,0,-6.32f);
        p.eye.transform.position=new Vector3(-2.85f,1.7f,-5.35f);
        p.eye.transform.LookAt(new Vector3(-3.4f,1.3f,-6.35f));
        p.eye.fieldOfView=60;
    }
    else
    {
        // Keep the seated pose on the mattress, then step smoothly to the authoritative spawn.
        if(v.phase==0)
        {
            float placement=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1f,stand));
            p.playerA.position=Vector3.Lerp(new Vector3(-4,0,-6.32f),new Vector3(v.ax,v.ay,v.az),placement);
            p.playerB.position=Vector3.Lerp(new Vector3(4,0,-6.32f),new Vector3(v.bx,v.by,v.bz),placement);
        }

        int slot=v.slot;
        var actor=slot==0?p.playerA:p.playerB;
        bool waking=v.phase==0 && stand<1;
        // Hide just before the camera reaches the head to prevent first-person body clipping.
        foreach(var renderer in actor.GetComponentsInChildren<Renderer>())renderer.enabled=waking && stand<.72f;
        if(waking)
        {
            float x=slot==0?-4:4;
            float t=Mathf.SmoothStep(0,1,stand);
            var start=new Vector3(x+1.15f,1.7f,-5.35f);
            p.eye.transform.position=Vector3.Lerp(start,actor.position+Vector3.up*1.62f,t);
            var target=actor.position+Vector3.up*1.3f+Vector3.right*.6f+Vector3.back*.03f;
            p.eye.transform.rotation=Quaternion.Slerp(Quaternion.LookRotation(target-start),Quaternion.identity,t);
            p.eye.fieldOfView=60;
        }
    }

    if(v!=null && (v.phase==1||v.phase==4) && lastPhase!=v.phase && !p.SettingsOpen && !p.WardrobeOpen)
    {
        Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
    }
    lastPhase=v==null?-1:v.phase;
    float lift=v==null?0:v.gateLift;
    foreach(var door in new[]{p.doorA,p.doorB})
    {
        var position=door.position;
        position.y=Mathf.Lerp(1.6f,4.95f,lift);
        door.position=position;
        var collider=door.GetComponent<Collider>();
        if(collider)collider.enabled=lift<.85f;
    }
    UpdatePrisonFloor(v);
    ShowMeadow(v!=null&&(v.phase==4||v.phase==2));
}
  void OnDestroy(){ShowMeadow(false);if(root)Destroy(root.gameObject);foreach(var m in owned)if(m)Destroy(m);}
 }
 [DefaultExecutionOrder(800)]
 public sealed class CellIdlePose:MonoBehaviour {
  Animator anim;Transform actor;public float SeatedWeight;public float ArmWeight=1;
  public void Initialize(Transform root){actor=root;anim=GetComponent<Animator>();}
  void LateUpdate()
  {
   float w=SeatedWeight;if(!anim||!actor||w<=0)return;
   var hips=anim.GetBoneTransform(HumanBodyBones.Hips);if(!hips)return;
   hips.position-=actor.up*(.36f*w);
   Limb(HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,new Vector3(-.15f,.08f,.33f),new Vector3(-.17f,.52f,.5f),w);
   Limb(HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,new Vector3(.17f,.08f,.33f),new Vector3(.19f,.52f,.5f),w);
   float armW=Mathf.Min(w,ArmWeight);
   if(armW>0)
   {
    // Brace both hands on the mattress' front lip while waiting in the lobby.
    Limb(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,new Vector3(.38f,.54f,-.03f),new Vector3(.56f,.76f,-.28f),armW);
    Limb(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,new Vector3(-.38f,.54f,-.03f),new Vector3(-.56f,.76f,-.28f),armW);
   }
   var head=anim.GetBoneTransform(HumanBodyBones.Head);head.rotation=Quaternion.Slerp(head.rotation,actor.rotation*Quaternion.Euler(12,0,-12)*head.rotation,w);
  }
  void Limb(HumanBodyBones upper,HumanBodyBones lower,HumanBodyBones end,Vector3 target,Vector3 bend,float w){
   var a=anim.GetBoneTransform(upper);var b=anim.GetBoneTransform(lower);var c=anim.GetBoneTransform(end);if(!a||!b||!c)return;
   Vector3 t=Vector3.Lerp(c.position,actor.TransformPoint(target),w),d=t-a.position;float l1=Vector3.Distance(a.position,b.position),l2=Vector3.Distance(b.position,c.position);
   float dist=Mathf.Clamp(d.magnitude,.001f,l1+l2-.001f);Vector3 dir=d.normalized;
   float along=(dist*dist+l1*l1-l2*l2)/(2*dist);float high=Mathf.Sqrt(Mathf.Max(0,l1*l1-along*along));
   Vector3 normal=Vector3.ProjectOnPlane(actor.TransformPoint(bend)-a.position,dir).normalized;
   Vector3 elbow=a.position+dir*along+normal*high;
   a.rotation=Quaternion.FromToRotation(b.position-a.position,elbow-a.position)*a.rotation;
   b.rotation=Quaternion.FromToRotation(c.position-b.position,t-b.position)*b.rotation;
  }
 }
}
