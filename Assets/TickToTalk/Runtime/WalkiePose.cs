using System.Collections.Generic;
using UnityEngine;

namespace TickToTalk
{
    [DefaultExecutionOrder(950)]
    public sealed partial class WalkiePose : MonoBehaviour
    {
        Transform actor, handset, fill;
        Animator animator;
        int slot;
        float weight;
        Material display, charge;
        static readonly Color IndicatorBlockedColor = new Color(1f, .55f, .09f);
        readonly List<Object> owned = new List<Object>();
        public bool Visible => handset && handset.gameObject.activeSelf;

        public void Initialize(Transform root, int playerSlot)
        {
            actor = root;
            slot = playerSlot;
            animator = GetComponent<Animator>();
            handset = new GameObject("Handheld walkie-talkie").transform;
            handset.SetParent(actor, false);
            handset.localPosition = new Vector3(-.36f, 1.4f, .42f);
            handset.localRotation = Quaternion.Euler(-12, 20, 0);
            var shell=MakeMaterial(new Color(.035f,.04f,.045f));
            var trim=MakeMaterial(new Color(.15f,.16f,.17f));
            var black=MakeMaterial(new Color(.008f,.01f,.012f));
            display=MakeMaterial(new Color(.5f,.8f,.87f));
            display.EnableKeyword("_EMISSION");display.SetColor("_EmissionColor",new Color(.2f,.38f,.42f));
            charge=MakeMaterial(new Color(.1f,.75f,.28f));charge.EnableKeyword("_EMISSION");
            Part("Graphite rounded case",Vector3.zero,new Vector3(.14f,.27f,.07f),trim);
            Part("Rubber face",new Vector3(0,0,-.039f),new Vector3(.125f,.254f,.014f),shell);
            Part("Speaker inset",new Vector3(0,.073f,-.048f),new Vector3(.105f,.085f,.006f),black);
            for(int row=0;row<7;row++)for(int col=0;col<9;col++)
                Part("Speaker perforation",new Vector3(-.043f+col*.0105f,.045f+row*.009f,-.052f),new Vector3(.003f,.003f,.002f),trim);
            Part("LCD surround",new Vector3(0,-.006f,-.049f),new Vector3(.11f,.057f,.008f),black);
            Part("Blue LCD",new Vector3(0,-.006f,-.054f),new Vector3(.096f,.043f,.003f),display);
            LabelRadio("CH 02",new Vector3(-.041f,-.013f,-.057f),.019f,Color.black);
            LabelRadio("TICK",new Vector3(-.045f,.105f,-.054f),.01f,Color.white);
            string[] labels={"CH","+","MON","PWR","-","CALL"};
            for(int row=0;row<2;row++)for(int col=0;col<3;col++){
                var pos=new Vector3(-.037f+col*.037f,-.065f-row*.034f,-.05f);
                Part("Key "+labels[row*3+col],pos,new Vector3(.03f,.028f,.009f),trim);
                LabelRadio(labels[row*3+col],pos+new Vector3(-.012f,.004f,-.006f),.007f,Color.white);
            }
            Part("Rubber antenna",new Vector3(-.046f,.237f,.005f),new Vector3(.019f,.22f,.019f),shell);
            Part("Antenna base",new Vector3(-.046f,.143f,.005f),new Vector3(.03f,.038f,.03f),black);
            Part("Top volume dial",new Vector3(.014f,.139f,.005f),new Vector3(.032f,.022f,.032f),black);
            Part("Side PTT key",new Vector3(.075f,.025f,0),new Vector3(.01f,.06f,.03f),black);
            Part("Channel track",new Vector3(0,.007f,-.057f),new Vector3(.086f,.012f,.002f),black);
            fill=Part("Channel status",new Vector3(0,.007f,-.06f),new Vector3(.08f,.008f,.002f),charge);
            handset.gameObject.SetActive(false);
        }

        Material MakeMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.SetFloat("_Smoothness",.22f);
            owned.Add(material);
            return material;
        }

        Transform Part(string name, Vector3 position, Vector3 scale, Material material)
        {
            float width=scale.x,height=scale.y,depth=scale.z;
            var go=new GameObject(name);go.transform.SetParent(handset,false);go.transform.localPosition=position;
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
            var session = NetworkSession.Instance;
            var view = session ? session.View : null;
            bool holding = view != null && view.phase == 1 && (slot == 0 ? view.walkieA : view.walkieB);
            if (view != null && view.slot == slot)
            {
                var presentation = session.GetComponent<PrisonPresentation>();
                if (presentation) holding = presentation.WalkieHeld && view.phase == 1;
            }
            bool tablet = view != null && view.phase == 1 && (slot == 0 ? view.tabletA : view.tabletB);
            if (view != null && view.slot == slot)
            {
                var presentation = session.GetComponent<PrisonPresentation>();
                if (presentation) tablet = presentation.TabletOpen;
            }
            weight = Mathf.MoveTowards(weight, holding && !tablet ? 1 : 0, Time.deltaTime * 7);
            var tabletPose = GetComponent<TabletPose>();
            if (tablet && tabletPose && tabletPose.Prop)
            {
                handset.SetParent(tabletPose.Prop, false);
                handset.localPosition = new Vector3(-.38f, 0, .045f);
                handset.localRotation = Quaternion.Euler(0, 0, -10);
            }
            else
            {
                handset.SetParent(actor, false);
                handset.localPosition = new Vector3(-.36f, 1.4f, .42f);
                handset.localRotation = Quaternion.Euler(-12, 20, 0);
            }
            handset.gameObject.SetActive(holding || weight > .45f);
            foreach (var part in handset.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = view != null && view.slot == slot ? 30 : 0;
            if (view == null || !handset.gameObject.activeSelf) return;
            bool transmitting = view.speaker == slot;
            var color = transmitting ? new Color(1f, .16f, .09f) :
                view.speaker >= 0 ? IndicatorBlockedColor : new Color(.1f, .75f, .28f);
            charge.color = color;
            charge.SetColor("_EmissionColor", color * 1.5f);

        }

        void OnAnimatorIK(int layer)
        {
            if (!animator || !handset || weight <= 0) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, handset.TransformPoint(new Vector3(-.025f, -.17f, -.09f)));
            animator.SetIKRotation(AvatarIKGoal.LeftHand, actor.rotation * Quaternion.Euler(15f, -25f, 70f));
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, weight * .7f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, actor.TransformPoint(new Vector3(-.45f, 1.12f, .12f)));
        }

        void GripRadio()
        {
            if(!animator || !handset || weight<=0)return;
            var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if(!hand)return;
            // Left palm normal is -hand.right. Face it upward and rest the radio base on it.
            // Retain the avatar's relaxed finger pose rather than forcing a clenched grip.
            hand.rotation=Quaternion.Slerp(hand.rotation,Quaternion.LookRotation(-handset.right,handset.forward),weight);
        }

        void OnDestroy()
        {
            if (firstPerson) Destroy(firstPerson.gameObject);
            if (handset) Destroy(handset.gameObject);
            foreach (var resource in owned) if (resource) Destroy(resource);
        }
    }
}
