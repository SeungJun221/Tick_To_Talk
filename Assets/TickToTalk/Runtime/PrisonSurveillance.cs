using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TickToTalk
{
    // Environmental CCTV prop: recording is a visual effect, no video capture or player microphone access.
    public sealed class PrisonSurveillance : MonoBehaviour
    {
        readonly Transform[] heads = new Transform[8];
        readonly Light[] lamps = new Light[8];
        readonly Material[] indicators = new Material[8];
        readonly List<Material> materials = new List<Material>();
        PrisonPresentation presentation;
        Material shell, black, steel, lens, infrared;
        Material Make(Color color, float metallic, float smooth)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smooth);
            materials.Add(m);return m;
        }
        GameObject Part(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Vector3 rotation=default)
        {
            var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);
            g.transform.localPosition=position;g.transform.localScale=scale;g.transform.localEulerAngles=rotation;
            g.GetComponent<Renderer>().sharedMaterial=material;
            var collider=g.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            return g;
        }
        void Start()
        {
            presentation=GetComponent<PrisonPresentation>();
            if(BindBakedSurveillance())return;
            shell=Make(new Color(.78f,.81f,.82f),.35f,.55f);
            black=Make(new Color(.018f,.025f,.03f),.3f,.6f);
            steel=Make(new Color(.18f,.22f,.24f),.8f,.7f);
            lens=Make(new Color(.025f,.1f,.16f),.65f,.95f);
            infrared=Make(new Color(.12f,.24f,.3f),.6f,.9f);
            for(int i=0;i<8;i++)
            {
                var mount=new GameObject("CCTV " +(i%2==0?"A":"B")+" / Sector "+(i/2+1)+" / Ceiling mount").transform;
                mount.SetParent(transform,false);mount.position=new Vector3(i%2==0?-6.6f:6.6f,3.95f,2.7f+(i/2)*9);
                Part("Ceiling plate",mount,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.43f,.035f,.43f),shell);
                Part("Suspension bracket",mount,PrimitiveType.Cylinder,new Vector3(0,-.24f,0),new Vector3(.105f,.22f,.105f),steel);
                Part("Swivel joint",mount,PrimitiveType.Sphere,new Vector3(0,-.48f,0),Vector3.one*.23f,steel);
                for(int bolt=0;bolt<4;bolt++)
                {
                    float angle=bolt*Mathf.PI*.5f;
                    Part("Mount screw",mount,PrimitiveType.Cylinder,new Vector3(Mathf.Cos(angle)*.15f,-.038f,Mathf.Sin(angle)*.15f),new Vector3(.035f,.012f,.035f),black);
                }
                var head=new GameObject("Tracking camera head").transform;head.SetParent(mount,false);head.localPosition=new Vector3(0,-.63f,0);heads[i]=head;
                head.rotation=Quaternion.LookRotation(new Vector3(i%2==0?-4:4,1.45f,(i/2)*9)-head.position);
                Part("Weatherproof white housing",head,PrimitiveType.Cylinder,Vector3.zero,new Vector3(.48f,.38f,.48f),shell,new Vector3(90,0,0));
                Part("Upper sun hood",head,PrimitiveType.Cube,new Vector3(0,.225f,.09f),new Vector3(.53f,.065f,.83f),shell);
                Part("Rear cap",head,PrimitiveType.Cylinder,new Vector3(0,0,-.38f),new Vector3(.46f,.025f,.46f),shell,new Vector3(90,0,0));
                Part("Black lens bezel",head,PrimitiveType.Cylinder,new Vector3(0,0,.39f),new Vector3(.445f,.035f,.445f),black,new Vector3(90,0,0));
                Part("Optical glass",head,PrimitiveType.Cylinder,new Vector3(0,0,.43f),new Vector3(.22f,.015f,.22f),lens,new Vector3(90,0,0));
                Part("Lens aperture",head,PrimitiveType.Cylinder,new Vector3(0,0,.448f),new Vector3(.105f,.006f,.105f),black,new Vector3(90,0,0));
                for(int diode=0;diode<12;diode++)
                {
                    float angle=diode*Mathf.PI/6;
                    Part("Infrared diode",head,PrimitiveType.Sphere,new Vector3(Mathf.Cos(angle)*.17f,Mathf.Sin(angle)*.17f,.43f),Vector3.one*.037f,infrared);
                }
                indicators[i]=Make(new Color(.5f,.005f,.002f),.1f,.45f);indicators[i].EnableKeyword("_EMISSION");
                var led=Part("REC / blinking red indicator",head,PrimitiveType.Sphere,new Vector3(.205f,-.14f,.43f),Vector3.one*.065f,indicators[i]);
                led.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
                var lightObject=new GameObject("Recording red glow");lightObject.transform.SetParent(head,false);lightObject.transform.localPosition=new Vector3(.205f,-.14f,.51f);
                var lamp=lightObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.015f,.005f);lamp.range=1.5f;lamp.shadows=LightShadows.None;lamps[i]=lamp;
            }
        }

bool BindBakedSurveillance()
{
    // The baked object names contain '/', which Transform.Find interprets as a path.
    // Bind the exact direct-child names so only the editable scene CCTV set is used.
    for(int i=0;i<8;i++)
    {
        string mountName="CCTV "+(i%2==0?"A":"B")+" / Sector "+(i/2+1)+" / Ceiling mount";
        Transform mount=null;
        foreach(Transform child in transform)
            if(child.name==mountName){mount=child;break;}
        if(!mount)return false;

        heads[i]=mount.Find("Tracking camera head");
        if(!heads[i])return false;

        Transform ledTransform=null;
        foreach(Transform child in heads[i])
            if(child.name=="REC / blinking red indicator"){ledTransform=child;break;}
        var led=ledTransform?ledTransform.GetComponent<Renderer>():null;
        indicators[i]=led?led.sharedMaterial:null;
        lamps[i]=heads[i].Find("Recording red glow")?.GetComponent<Light>();
        if(!indicators[i]||!lamps[i])return false;
    }
    return true;
}

        void LateUpdate()
        {
            var session=NetworkSession.Instance;
            for(int i=0;i<8;i++)
            {
                if(!heads[i])continue;
                var target=new Vector3(i%2==0?-4:4,1.45f,(i/2)*9);
                if(session && session.View!=null)
                {
                    var actor=i%2==0?presentation.playerA:presentation.playerB;
                    if(session.IsTestMode)actor=presentation.playerA;
                    if((actor.position.x<0)==(i%2==0) && Mathf.Abs(actor.position.z-(i/2)*9)<4.5f)target=actor.position+Vector3.up*1.4f;
                    else target.x+=Mathf.Sin(Time.time*.24f+i)*1.8f;
                }
                else target.x+=Mathf.Sin(Time.time*.24f+i)*1.8f;
                var desired=Quaternion.LookRotation(target-heads[i].position);
                heads[i].rotation=Quaternion.RotateTowards(heads[i].rotation,desired,22*Time.deltaTime);
                bool recording=(Time.unscaledTime+i*.23f)%1.2f<.65f;
                indicators[i].SetColor("_EmissionColor",recording?new Color(5,.015f,.005f):Color.black);
                indicators[i].SetColor("_BaseColor",recording?Color.red:new Color(.1f,.002f,.002f));
                lamps[i].intensity=recording?1.2f:0;
            }
        }
        public Vector3 FailureCameraPosition(int room, int sector, Vector3 target)
        {
            var head=heads[Mathf.Clamp(sector,0,3)*2+Mathf.Clamp(room,0,1)];
            var position=head?head.position:new Vector3(room==0?-6.6f:6.6f,3.32f,2.7f+sector*9);
            // Put the lens in front of the CCTV housing, rather than inside its mesh.
            return position+(target-position).normalized*.65f;
        }
        void OnDestroy(){foreach(var m in materials)if(m)Destroy(m);}
    }
}
