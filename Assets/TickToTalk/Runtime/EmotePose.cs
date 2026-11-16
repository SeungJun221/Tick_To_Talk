using UnityEngine;

namespace TickToTalk
{
    public sealed class EmotePose : MonoBehaviour
    {
        Animator animator;
        Transform actor;
        int slot;
        int emote;
        int fadingEmote;
        float remaining;
        float weight;
        public int CurrentEmote => emote;
        public float PoseWeight => weight;

        public void Initialize(Transform root, int playerSlot)
        {
            actor = root;
            slot = playerSlot;
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            var session = NetworkSession.Instance;
            var view = session ? session.View : null;
            if (!actor || view == null || view.phase != 1)
            {
                emote = 0;
                remaining = 0;
            }
            else
            {
                emote = slot == 0 ? view.emoteA : view.emoteB;
                remaining = slot == 0 ? view.emoteRemainingA : view.emoteRemainingB;
            }
            if (emote != 0) fadingEmote = emote;
            else if (view == null || view.phase != 1) fadingEmote = 0;
            weight = Mathf.MoveTowards(weight, emote != 0 ? 1 : 0, Time.deltaTime * 8);
        }

        void OnAnimatorIK(int layer)
        {
            if (!animator || !actor || weight <= 0 || fadingEmote == 0) return;
            float progress = Mathf.Clamp01((2.5f - remaining) / 2.5f);
            float wave = Mathf.Sin(progress * Mathf.PI * 8);
            Vector3 left = Vector3.zero, right = Vector3.zero;
            switch (fadingEmote)
            {
                case 1: // Greeting
                    right = new Vector3(.48f + wave * .05f, 2.03f, .26f);
                    break;
                case 2: // Cheer
                    left = new Vector3(-.5f, 2.12f + wave * .05f, .26f);
                    right = new Vector3(.5f, 2.12f - wave * .05f, .26f);
                    break;
                case 3: // Disapproval gesture
                    right = new Vector3(.37f, 1.38f, .54f);
                    break;
                default: return;
            }
            SetHand(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, left, -1);
            SetHand(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, right, 1);
            if (fadingEmote == 3)
            {
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, weight);
                animator.SetIKRotation(AvatarIKGoal.RightHand, actor.rotation * Quaternion.LookRotation(Vector3.down, Vector3.left));
            }
        }

        // Apply after Animator IK: close all four fingers, while the thumb points down.
        void LateUpdate()
        {
            if (!animator || !actor || fadingEmote != 3 || weight <= 0) return;
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            if(!hand)return;
            hand.rotation=Quaternion.Slerp(hand.rotation,actor.rotation*Quaternion.LookRotation(Vector3.down,Vector3.left),weight);
            var fingers=new[]{HumanBodyBones.RightIndexProximal,HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightRingProximal,HumanBodyBones.RightLittleProximal};
            foreach(var finger in fingers) {
                var joint=animator.GetBoneTransform(finger);
                if(!joint)continue;
                Vector3 along=joint.childCount>0?(joint.GetChild(0).position-joint.position).normalized:joint.up;
                Vector3 axis=Vector3.Cross(along,hand.right).normalized;
                for(int j=0;j<3 && joint;j++) {
                    var child=joint.childCount>0?joint.GetChild(0):null;
                    joint.rotation=Quaternion.AngleAxis((j==0?80f:95f)*weight,axis)*joint.rotation;
                    joint=child;
                }
            }
            var thumb=animator.GetBoneTransform(HumanBodyBones.RightThumbProximal);
            if(thumb && thumb.childCount>0) {
                var target=Quaternion.FromToRotation(thumb.GetChild(0).position-thumb.position,-actor.up)*thumb.rotation;
                thumb.rotation=Quaternion.Slerp(thumb.rotation,target,weight);
                var tip=thumb.GetChild(0);
                if(tip) {
                    target=Quaternion.FromToRotation(tip.childCount>0?tip.GetChild(0).position-tip.position:tip.up,-actor.up)*tip.rotation;
                    tip.rotation=Quaternion.Slerp(tip.rotation,target,weight);
                }
            }
        }

        void SetHand(AvatarIKGoal goal, AvatarIKHint hint, Vector3 position, int side)
        {
            if (position == Vector3.zero) return;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKPosition(goal, actor.TransformPoint(position));
            animator.SetIKHintPositionWeight(hint, weight * .75f);
            animator.SetIKHintPosition(hint, actor.TransformPoint(new Vector3(side * .7f, 1.4f, -.1f)));
        }
    }
}
