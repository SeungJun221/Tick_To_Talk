using UnityEngine;
using UnityEngine.InputSystem;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        public bool TabletOpen { get; private set; }
        bool clearNotes;
        float saveNotesAt;
        Texture2D roundCorner;
        TabletPose tabletA,tabletB;
        const float BombTabletLowerOffset=292f;
        bool bombTabletRaised,bombTabletClearFocus;
        float bombTabletYOffset=BombTabletLowerOffset;
        void StartTablet()
        {
            StartInk();
            tabletA=playerA.GetComponentInChildren<Animator>().gameObject.AddComponent<TabletPose>();tabletA.Initialize(playerA,0);
            tabletB=playerB.GetComponentInChildren<Animator>().gameObject.AddComponent<TabletPose>();tabletB.Initialize(playerB,1);
            playerA.GetComponentInChildren<Animator>().gameObject.AddComponent<EmotePose>().Initialize(playerA,0);
            playerB.GetComponentInChildren<Animator>().gameObject.AddComponent<EmotePose>().Initialize(playerB,1);
            roundCorner=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                float cornerX=Mathf.Max(0,16-Mathf.Min(x,63-x));
                float cornerY=Mathf.Max(0,16-Mathf.Min(y,63-y));
                roundCorner.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16-Mathf.Sqrt(cornerX*cornerX+cornerY*cornerY))));
            }
            roundCorner.Apply();roundCorner.wrapMode=TextureWrapMode.Clamp;roundCorner.filterMode=FilterMode.Bilinear;
        }
        void UpdateTablet()
        {
            if(TabletOpen && (session.View==null || session.View.phase!=1)) CloseTablet();
            if(InkDirty && !ink.Drawing && Time.unscaledTime>=saveNotesAt) SaveNotes();

            var key=Keyboard.current;
            if(key!=null && key.tKey.wasPressedThisFrame && session.View!=null && session.View.phase==1 && !SettingsOpen && !WardrobeOpen)
            {
                if(panel=="bomb")
                    SetBombTabletRaised(!bombTabletRaised);
                else if(panel=="")
                {
                    if(TabletOpen)CloseTablet();
                    else OpenTablet();
                }
            }

            float target=panel=="bomb"&&bombTabletRaised?0:BombTabletLowerOffset;
            bombTabletYOffset=panel=="bomb"
                ?Mathf.MoveTowards(bombTabletYOffset,target,Time.unscaledDeltaTime*1000f)
                :BombTabletLowerOffset;
        }

        void SetBombTabletRaised(bool raised)
        {
            if(bombTabletRaised==raised)return;
            EndInk();
            clearNotes=false;
            bombTabletRaised=raised;
            bombTabletClearFocus=raised;
            Keyboard.current?.SetIMEEnabled(false);
        }

        void ResetBombTablet()
        {
            EndInk();
            clearNotes=false;
            bombTabletRaised=false;
            bombTabletClearFocus=false;
            bombTabletYOffset=BombTabletLowerOffset;
        }

        public void OpenTablet()
        {
            if(session.View==null || session.View.phase!=1)return;
            CloseEmoteMenu();
            CloseInteractionPanel();TabletOpen=true;clearNotes=false;
            session.Command("tablet","1");session.Command("move");session.SetTestVertical(0);
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            Keyboard.current?.SetIMEEnabled(false);
        }
        public void CloseTablet()
        {
            if(!TabletOpen)return;
            EndInk();TabletOpen=false;clearNotes=false;SaveNotes();session.Command("tablet","0");
            Keyboard.current?.SetIMEEnabled(false);
            if(session.View!=null && session.View.phase==1 && !SettingsOpen){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
        }
        void TabletRect(Rect r,Color color,float radius=12)
        {
            var old=GUI.color;GUI.color=color;
            GUI.DrawTexture(new Rect(r.x+radius,r.y,r.width-2*radius,r.height),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x,r.y+radius,r.width,r.height-2*radius),Texture2D.whiteTexture);
            foreach(float x in new[]{r.x,r.xMax-radius})foreach(float y in new[]{r.y,r.yMax-radius})
            {
                var uv=new Rect(x==r.x?0:.75f,y==r.y?.75f:0,.25f,.25f);
                GUI.DrawTextureWithTexCoords(new Rect(x,y,radius,radius),roundCorner,uv);
            }
            GUI.color=old;
        }
        void TabletLine(Vector2 a,Vector2 b,Color color,float thickness=2)
        {
            var matrix=GUI.matrix;var old=GUI.color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);GUI.color=color;
            GUI.DrawTexture(new Rect(a.x,a.y-thickness*.5f,Vector2.Distance(a,b),thickness),Texture2D.whiteTexture);
            GUI.color=old;GUI.matrix=matrix;
        }
        void OfflineIcons(float x,float y)
        {
            var muted=new Color(.72f,.74f,.83f);var red=new Color(1,.45f,.42f);
            for(int ring=1;ring<=3;ring++)for(int n=0;n<8;n++){
                float a=(215+n*13.75f)*Mathf.Deg2Rad,b=(215+(n+1)*13.75f)*Mathf.Deg2Rad;
                TabletLine(new Vector2(x+Mathf.Cos(a)*ring*6,y+Mathf.Sin(a)*ring*6),new Vector2(x+Mathf.Cos(b)*ring*6,y+Mathf.Sin(b)*ring*6),muted);
            }
            TabletLine(new Vector2(x-18,y-23),new Vector2(x+17,y+4),red,3);
            for(int i=0;i<4;i++)TabletRect(new Rect(x+158+i*7,y-5-i*5,4,5+i*5),muted,1);
            TabletLine(new Vector2(x+153,y-23),new Vector2(x+188,y+3),red,3);
        }
        void DrawTablet(float yOffset=0)
        {
            var previousMatrix=GUI.matrix;
            GUI.matrix=previousMatrix*Matrix4x4.Translate(new Vector3(0,yOffset,0));
            try
            {
                TabletRect(new Rect(145,141,998,548),new Color(0,0,0,.65f),25);
                TabletRect(new Rect(145,128,990,542),new Color(.48f,.49f,.53f),24);
                TabletRect(new Rect(149,131,982,535),new Color(.025f,.028f,.035f),21);
                TabletRect(new Rect(166,149,948,497),new Color(.22f,.24f,.43f),12);
                TabletRect(new Rect(614,148,52,12),new Color(.025f,.028f,.035f),4);
                TabletRect(new Rect(635,151,8,8),new Color(.11f,.17f,.23f),4);
                var light=new GUIStyle(small){fontSize=16};light.normal.textColor=new Color(.91f,.92f,.98f);
                Label(190,166,240,25,"LOCAL  /  "+System.DateTime.Now.ToString("HH:mm"),light);
                OfflineIcons(699,186);Label(725,165,135,25,"Wi-Fi × 끊김",light);Label(894,165,135,25,"수신 × 불가",light);
                TabletRect(new Rect(185,204,909,420),new Color(.95f,.95f,.97f),12);
                var inkStyle=new GUIStyle(body){fontSize=21};inkStyle.normal.textColor=new Color(.13f,.15f,.23f);
                var fine=new GUIStyle(inkStyle){fontSize=15};
                DrawInkPage(inkStyle,fine,yOffset);
                if(clearNotes)
                {
                    TabletRect(new Rect(430,338,480,160),new Color(.17f,.19f,.28f),12);
                    Label(455,358,440,35,"메모를 모두 지울까요?",body);
                    if(GUI.Button(new Rect(460,425,190,45),"취소"))clearNotes=false;
                    if(GUI.Button(new Rect(680,425,190,45),"모두 지우기")){this.ink.Clear();SaveNotes();clearNotes=false;}
                }
            }
            finally
            {
                GUI.matrix=previousMatrix;
            }
        }
        void DrawBombTablet(string bombTitle="")
        {
            if(bombTabletClearFocus)
            {
                GUI.FocusControl(null);
                bombTabletClearFocus=false;
            }

            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&Mouse.current!=null)
            {
                var point=TabletPointer(Mouse.current.position.ReadValue(),Screen.width,Screen.height);
                if(!bombTabletRaised)
                {
                    float top=128+bombTabletYOffset;
                    var visibleTablet=new Rect(145,top,998,Mathf.Max(0,720-top));
                    if(visibleTablet.Contains(point))
                    {
                        SetBombTabletRaised(true);
                        e.Use();
                    }
                }
                else if(new Rect(230,80,820,65).Contains(point))
                {
                    SetBombTabletRaised(false);
                    e.Use();
                }
            }

            DrawTablet(bombTabletYOffset);

            Box(new Rect(230,80,820,65));
            Label(265,98,530,42,string.IsNullOrEmpty(bombTitle)?"폭탄 / 보안 단말":bombTitle,title);
            if(bombTabletRaised||bombTabletYOffset<BombTabletLowerOffset)
            {
                var returnStyle=new GUIStyle(small){alignment=TextAnchor.MiddleRight};
                Label(790,98,225,36,"클릭하여 정답판 보기",returnStyle);
            }
        }

        void DisposeTablet(){EndInk();SaveNotes();if(this.ink!=null)this.ink.Dispose();Keyboard.current?.SetIMEEnabled(false);if(roundCorner)Destroy(roundCorner);}
    }
}
