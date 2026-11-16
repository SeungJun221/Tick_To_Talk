using UnityEngine;
using UnityEngine.InputSystem;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        public bool WalkieHeld { get; private set; }
        WalkiePose walkieA, walkieB;
        float nextWalkieTalkRequest;

        void StartWalkie()
        {
            walkieA = playerA.GetComponentInChildren<Animator>().gameObject.AddComponent<WalkiePose>();
            walkieA.Initialize(playerA, 0);
            walkieB = playerB.GetComponentInChildren<Animator>().gameObject.AddComponent<WalkiePose>();
            walkieB.Initialize(playerB, 1);
        }

        void LowerWalkie()
        {
            bool wasHeld=WalkieHeld;
            WalkieHeld=false;
            nextWalkieTalkRequest=0;
            if(wasHeld&&session)session.Command("walkie","0");
        }

        void UpdateWalkie()
        {
            var v=session.View;
            if(v==null||v.phase!=1)
            {
                LowerWalkie();
                return;
            }

            var key=Keyboard.current;
            bool canUse=!SettingsOpen&&!WardrobeOpen&&
                (panel=="bomb"||TabletOpen||(panel==""&&Cursor.lockState==CursorLockMode.Locked));
            ApplyWalkiePushToTalk(v,canUse&&key!=null&&key.capsLockKey.isPressed);
        }

        void ApplyWalkiePushToTalk(PlayerView v,bool pressed)
        {
            if(!pressed)
            {
                LowerWalkie();
                return;
            }

            if(!WalkieHeld)
            {
                WalkieHeld=true;
                nextWalkieTalkRequest=0;
                session.Command("walkie","1");
            }

            if(v.speaker>=0&&v.speaker!=v.slot)
            {
                nextWalkieTalkRequest=0;
                return;
            }

            if(v.speaker<0&&Time.unscaledTime>=nextWalkieTalkRequest)
            {
                session.Command("talk");
                nextWalkieTalkRequest=Time.unscaledTime+.25f;
            }
        }

        void DrawWalkieTabletOverlay()
        {
            var v=session.View;
            if(!WalkieHeld||v==null)return;

            Color status=v.speaker==v.slot
                ?new Color(1f,.16f,.09f)
                :v.speaker>=0?new Color(1f,.55f,.09f):new Color(.1f,.75f,.28f);
            string state=v.speaker==v.slot?"TX":v.speaker>=0?"WAIT":"READY";

            TabletRect(new Rect(40,340,98,255),new Color(0,0,0,.55f),16);
            TabletRect(new Rect(44,334,90,250),new Color(.12f,.13f,.14f),15);
            TabletRect(new Rect(50,342,78,234),new Color(.025f,.03f,.035f),12);
            TabletRect(new Rect(58,270,17,92),new Color(.035f,.04f,.045f),6);
            TabletRect(new Rect(55,326,25,28),new Color(.01f,.012f,.014f),7);
            TabletRect(new Rect(97,326,25,20),new Color(.01f,.012f,.014f),7);
            TabletRect(new Rect(58,372,62,68),new Color(.008f,.01f,.012f),9);
            for(int row=0;row<5;row++)for(int col=0;col<5;col++)
                TabletRect(new Rect(66+col*11,382+row*10,4,4),new Color(.2f,.21f,.22f),2);
            TabletRect(new Rect(58,453,62,52),new Color(.02f,.12f,.15f),6);
            TabletRect(new Rect(63,459,52,8),status,4);
            var lcd=new GUIStyle(small){alignment=TextAnchor.MiddleCenter,fontSize=13,fontStyle=FontStyle.Bold};
            lcd.normal.textColor=new Color(.72f,.95f,.96f);
            Label(60,470,58,28,state,lcd);
            TabletRect(new Rect(64,520,50,13),new Color(.18f,.19f,.2f),4);
            TabletRect(new Rect(64,541,50,13),new Color(.18f,.19f,.2f),4);
            TabletRect(new Rect(128,392,8,55),new Color(.12f,.13f,.14f),3);
        }



        void DisposeWalkie()
        {
            WalkieHeld = false;
        }
    }
}
