using System.Collections.Generic;
using UnityEngine;

namespace TickToTalk
{
    public sealed class TabletPose : MonoBehaviour
    {
        Transform actor,prop;
        Animator animator;
        int slot;
        float weight;
        const float HideWeight=.5f;
        readonly List<Object> owned=new List<Object>();
        public float HoldWeight=>weight;
        public Transform Prop=>prop;
        public void Initialize(Transform root,int playerSlot)
        {
            actor=root;slot=playerSlot;animator=GetComponent<Animator>();
            prop=new GameObject("Two-handed tablet").transform;prop.SetParent(actor,false);
            prop.localPosition=new Vector3(0,1.16f,.39f);prop.localRotation=Quaternion.Euler(58,0,0);
            var shell=Mat(new Color(.29f,.3f,.34f),.65f); var bezel=Mat(new Color(.012f,.015f,.022f),.25f); var screen=Mat(new Color(.2f,.25f,.55f),.1f);
            screen.EnableKeyword("_EMISSION");screen.SetColor("_EmissionColor",new Color(.12f,.18f,.4f));
            Plate("Graphite aluminum body",.55f,.35f,.02f,0,shell);
            Plate("Black bezel",.54f,.34f,.004f,-.012f,bezel);
            Plate("Notebook display",.505f,.305f,.002f,-.015f,screen);
            for(int i=0;i<2;i++)
            {
                var camera=GameObject.CreatePrimitive(PrimitiveType.Cylinder);camera.name="Rear camera";camera.transform.SetParent(prop,false);
                camera.transform.localPosition=new Vector3(.21f-i*.04f,.135f,.014f);camera.transform.localRotation=Quaternion.Euler(90,0,0);camera.transform.localScale=new Vector3(.03f,.004f,.03f);
                camera.GetComponent<Renderer>().sharedMaterial=bezel;var c=camera.GetComponent<Collider>();c.enabled=false;Destroy(c);
            }
            Plate("Rear pen rail",.25f,.014f,.003f,.013f,bezel).localPosition+=new Vector3(-.025f,.135f,0);
            prop.gameObject.SetActive(false);
        }
        Material Mat(Color color,float metal){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.6f);owned.Add(m);return m;}
        Transform Plate(string name,float width,float height,float depth,float z,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(prop,false);go.transform.localPosition=new Vector3(0,0,z);
            var vertices=new List<Vector3>();var triangles=new List<int>();float radius=Mathf.Min(.018f,height*.4f,width*.4f);
            for(int side=0;side<2;side++)for(int corner=0;corner<4;corner++)for(int step=0;step<=6;step++)
            {
                float a=(corner*90+step*15)*Mathf.Deg2Rad;
                float cx=(corner==0||corner==3?1:-1)*(width*.5f-radius),cy=(corner<2?1:-1)*(height*.5f-radius);
                vertices.Add(new Vector3(cx+Mathf.Cos(a)*radius,cy+Mathf.Sin(a)*radius,(side==0?-1:1)*depth*.5f));
            }
            int n=vertices.Count/2;
            for(int i=1;i<n-1;i++){triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}
            for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j,n+j,i,n+j,n+i});}
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        void Update()
        {
            var s=NetworkSession.Instance;if(!s||!actor)return;
            bool local=s.View!=null && s.View.slot==slot;
            bool holding=s.View!=null && s.View.phase==1 && (slot==0?s.View.tabletA:s.View.tabletB);
            if(local)holding=s.GetComponent<PrisonPresentation>().TabletOpen;
            weight=Mathf.MoveTowards(weight,holding?1:0,Time.deltaTime*4);
            prop.gameObject.SetActive(holding || weight>HideWeight);
            foreach(var t in prop.GetComponentsInChildren<Transform>(true))t.gameObject.layer=local?30:0;
        }
        void OnAnimatorIK(int layer)
        {
            if(!animator||!prop)return;
            foreach(var goal in new[]{AvatarIKGoal.LeftHand,AvatarIKGoal.RightHand})
            {
                bool left=goal==AvatarIKGoal.LeftHand;
                animator.SetIKPositionWeight(goal,weight);
                animator.SetIKRotationWeight(goal,0);
                animator.SetIKPosition(goal,prop.TransformPoint(new Vector3(left?-.31f:.31f,-.11f,0)));
                var hint=left?AvatarIKHint.LeftElbow:AvatarIKHint.RightElbow;
                animator.SetIKHintPositionWeight(hint,weight*.85f);
                animator.SetIKHintPosition(hint,actor.TransformPoint(new Vector3(left?-.4f:.4f,1.05f,.15f)));
            }
            animator.SetLookAtWeight(weight*.45f,.1f,.7f,.5f,.6f);animator.SetLookAtPosition(prop.position);
        }
        static readonly HumanBodyBones[] LeftFingers={HumanBodyBones.LeftIndexProximal,HumanBodyBones.LeftMiddleProximal,HumanBodyBones.LeftRingProximal,HumanBodyBones.LeftLittleProximal};
        static readonly HumanBodyBones[] RightFingers={HumanBodyBones.RightIndexProximal,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal};
        void LateUpdate()
        {
            if(!animator || !prop || weight<=0)return;
            Grip(true);Grip(false);
        }
        void Grip(bool left)
        {
            var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            if(!hand)return;
            // Actual model axes: mirror palms face inward, both thumbs sit on the screen side.
            hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.LookRotation(-prop.forward,prop.up),weight);
            Vector3 inward=left?prop.right:-prop.right;
            foreach(var bone in left?LeftFingers:RightFingers) {
                var joint=animator.GetBoneTransform(bone);if(!joint)continue;
                Vector3 along=joint.childCount>0?(joint.GetChild(0).position-joint.position).normalized:joint.up;
                Vector3 axis=Vector3.Cross(along,inward).normalized;
                for(int j=0;j<3 && joint;j++) {
                    var next=joint.childCount>0?joint.GetChild(0):null;
                    joint.rotation=Quaternion.AngleAxis((j==0?60f:55f)*weight,axis)*joint.rotation;
                    joint=next;
                }
            }
            var thumb=animator.GetBoneTransform(left?HumanBodyBones.LeftThumbProximal:HumanBodyBones.RightThumbProximal);
            Vector3 thumbDirection=(inward+prop.up*.2f).normalized;
            for(int j=0;j<2 && thumb;j++) {
                var next=thumb.childCount>0?thumb.GetChild(0):null;
                Vector3 along=next?next.position-thumb.position:thumb.up;
                var target=Quaternion.FromToRotation(along,thumbDirection)*thumb.rotation;
                thumb.rotation=Quaternion.Slerp(thumb.rotation,target,weight);thumb=next;
            }
        }
        void OnDestroy(){if(prop)Destroy(prop.gameObject);foreach(var item in owned)if(item)Destroy(item);}
    }
}
