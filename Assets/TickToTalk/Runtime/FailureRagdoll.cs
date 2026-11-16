using System.Collections.Generic;
using UnityEngine;

namespace TickToTalk
{
    // A local visual ragdoll. Gameplay positions remain frozen on the server until restart.
    public sealed class FailureRagdoll
    {
        readonly Transform actor;
        readonly Animator animator;
        readonly Transform[] transforms;
        readonly Vector3[] positions;
        readonly Quaternion[] rotations;
        readonly Renderer[] renderers;
        readonly bool[] visible;
        readonly List<Behaviour> poses=new List<Behaviour>();
        readonly List<Rigidbody> bodies=new List<Rigidbody>();
        readonly List<Collider> colliders=new List<Collider>();
        readonly List<Joint> joints=new List<Joint>();
        readonly Dictionary<HumanBodyBones,Rigidbody> boneBodies=new Dictionary<HumanBodyBones,Rigidbody>();
        readonly bool animatorWasEnabled;
        Transform hips;
        public Vector3 Focus => hips?hips.position+Vector3.up*.25f:actor.position+Vector3.up;

        public FailureRagdoll(Transform root)
        {
            actor=root; animator=root.GetComponentInChildren<Animator>();
            transforms=root.GetComponentsInChildren<Transform>(true);
            positions=new Vector3[transforms.Length]; rotations=new Quaternion[transforms.Length];
            for(int i=0;i<transforms.Length;i++){positions[i]=transforms[i].localPosition;rotations[i]=transforms[i].localRotation;}
            renderers=root.GetComponentsInChildren<Renderer>(true);visible=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++)visible[i]=renderers[i].enabled;
            if(animator)
            {
                animatorWasEnabled=animator.enabled;
                if(animator.isHuman)hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                animator.enabled=false;
            }
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if(behaviour.enabled && (behaviour is CellIdlePose || behaviour is TabletPose || behaviour is WalkiePose || behaviour is EmotePose))
                {poses.Add(behaviour);behaviour.enabled=false;}
            foreach(var t in transforms)
                if(t.name=="Handheld walkie-talkie" || t.name=="Two-handed tablet")t.gameObject.SetActive(false);
            Show();
        }

        public void Show()
        {
            foreach(var renderer in renderers)
                if(renderer && renderer is SkinnedMeshRenderer)renderer.enabled=true;
        }

        public void Explode(Vector3 origin)
        {
            if(!animator || !animator.isHuman)return;
            Add(HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.LastBone,10,.16f);
            Add(HumanBodyBones.Spine,HumanBodyBones.Neck,HumanBodyBones.Hips,10,.17f);
            Add(HumanBodyBones.Head,HumanBodyBones.LastBone,HumanBodyBones.Spine,4,.12f);
            foreach(bool left in new[]{true,false})
            {
                var upperArm=left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm;
                var lowerArm=left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm;
                var hand=left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand;
                var thigh=left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg;
                var shin=left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg;
                var foot=left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot;
                Add(upperArm,lowerArm,HumanBodyBones.Spine,2,.065f);
                Add(lowerArm,hand,upperArm,1.5f,.055f);
                Add(thigh,shin,HumanBodyBones.Hips,6,.09f);
                Add(shin,foot,thigh,4,.07f);
            }
            for(int i=0;i<colliders.Count;i++)for(int j=i+1;j<colliders.Count;j++)Physics.IgnoreCollision(colliders[i],colliders[j]);
            var away=Vector3.ProjectOnPlane(Focus-origin,Vector3.up).normalized;
            if(away.sqrMagnitude<.1f)away=actor.right;
            foreach(var body in bodies)
            {
                body.AddForce(away*4.5f+Vector3.up*3.2f,ForceMode.VelocityChange);
                body.AddTorque(Vector3.Cross(Vector3.up,away)*3f,ForceMode.VelocityChange);
            }
        }

        void Add(HumanBodyBones bone,HumanBodyBones end,HumanBodyBones parent,float mass,float radius)
        {
            var t=animator.GetBoneTransform(bone);if(!t)return;
            var child=end==HumanBodyBones.LastBone?null:animator.GetBoneTransform(end);
            var delta=child?t.InverseTransformPoint(child.position):Vector3.up*.18f;
            var abs=new Vector3(Mathf.Abs(delta.x),Mathf.Abs(delta.y),Mathf.Abs(delta.z));
            int axis=abs.x>abs.y&&abs.x>abs.z?0:abs.z>abs.y?2:1;
            float scale=Mathf.Max(.01f,Mathf.Abs(t.lossyScale.x));
            var col=t.gameObject.AddComponent<CapsuleCollider>();
            col.direction=axis;col.center=delta*.5f;col.radius=radius/scale;col.height=Mathf.Max(col.radius*2,delta.magnitude);
            colliders.Add(col);
            var body=t.gameObject.AddComponent<Rigidbody>(); body.mass=mass;body.linearDamping=.35f;body.angularDamping=1.6f;
            body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations=12; body.maxAngularVelocity=10;
            bodies.Add(body);boneBodies[bone]=body;
            if(boneBodies.TryGetValue(parent,out var connected))
            {
                var joint=t.gameObject.AddComponent<CharacterJoint>(); joint.connectedBody=connected; joint.enableProjection=true;
                joint.lowTwistLimit=new SoftJointLimit{limit=-35};joint.highTwistLimit=new SoftJointLimit{limit=35};
                joint.swing1Limit=new SoftJointLimit{limit=50};joint.swing2Limit=new SoftJointLimit{limit=35};
                joints.Add(joint);
            }
        }

        public void Restore()
        {
            foreach(var col in colliders)if(col){col.enabled=false;Object.Destroy(col);}
            foreach(var joint in joints)if(joint)Object.Destroy(joint);
            foreach(var body in bodies)if(body){body.isKinematic=true;Object.Destroy(body);}
            for(int i=0;i<transforms.Length;i++)if(transforms[i])
            {transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];}
            for(int i=0;i<renderers.Length;i++)if(renderers[i])renderers[i].enabled=visible[i];
            foreach(var pose in poses)if(pose)pose.enabled=true;
            if(animator){animator.enabled=animatorWasEnabled;if(animatorWasEnabled)animator.Rebind();}
        }
    }
}
