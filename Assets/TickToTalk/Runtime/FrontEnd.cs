using UnityEngine;
using UnityEngine.InputSystem;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        public bool SettingsOpen { get; private set; }
        public bool WardrobeOpen { get; private set; }
        bool connectionMenu;
        int settingsTab, audioTab;
        GamePreferences preferences;
        WardrobeCatalog catalog;
        int[] outfit;
        string seenA, seenB;
        Transform preview;
        Camera previewCamera;
        RenderTexture previewTexture;
        Vector2 wardrobeScroll;
        bool BlocksGameplay => SettingsOpen || WardrobeOpen || TabletOpen || emoteMenuOpen;
        void StartFrontEnd()
        {
            preferences=GetComponent<GamePreferences>();
            if(!preferences) preferences=gameObject.AddComponent<GamePreferences>();
            catalog=Resources.Load<WardrobeCatalog>("WardrobeCatalog");
            if(catalog) outfit=catalog.Decode(PlayerPrefs.GetString("TTT_Outfit",catalog.Encode(catalog.Defaults())));
            eye.cullingMask &= ~((1<<31)|(1<<30));
        }
        void UpdateFrontEnd()
        {
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame) {
                if(emoteMenuOpen) CloseEmoteMenu();
                else if(TabletOpen) CloseTablet();
                else if(WardrobeOpen) CloseWardrobe();
                else if(SettingsOpen) CloseSettings();
                else OpenSettings();
            }
            var v=session.View;
            if(v!=null && v.phase==1 && WardrobeOpen) CloseWardrobe();
            if(v!=null && catalog) {
                if(seenA!=v.lookA) { catalog.Apply(playerA,catalog.Decode(v.lookA),v.slot!=0); seenA=v.lookA; }
                if(seenB!=v.lookB) { catalog.Apply(playerB,catalog.Decode(v.lookB),v.slot!=1); seenB=v.lookB; }
            } else { seenA=seenB=null; }
        }
        public void OpenSettings()
        {
            CloseTablet(); CloseEmoteMenu(); CloseInteractionPanel(); SettingsOpen=true; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            session.Command("move"); session.SetTestVertical(0); session.Command("release");
        }
        public void CloseSettings()
        {
            preferences.Save(); SettingsOpen=false; GetComponent<TableVoice>().MonitorRequested=false;
            if(CanControlMovement) { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
        }
        bool DrawFrontEnd()
        {
            if(SettingsOpen) { DrawSettings(); return true; }
            if(WardrobeOpen) { DrawWardrobe(); return true; }
            if(session.View!=null || connectionMenu) return false;
            Box(new Rect(75,110,520,510));
            Label(135,145,900,85,"Tick To Talk",new GUIStyle(title){fontSize=46});
            if(Button(140,340,400,"게임 시작")) connectionMenu=true;
            if(Button(140,402,400,"설정")) OpenSettings();
            if(Button(140,464,400,"캐릭터 꾸미기")) OpenWardrobe();
            if(Button(140,526,400,"게임 종료")) QuitGame();
            return true;
        }
        public void QuitGame() {
            preferences.Save(); session.Disconnect();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        void Row(float y,string name) { GUI.color=new Color(.19f,.21f,.22f,1); GUI.DrawTexture(new Rect(160,y,960,56),Texture2D.whiteTexture); GUI.color=Color.white; Label(185,y+14,330,32,name); }
        float VolumeRow(float y,string name,float value)
        {
            Row(y,name); float result=GUI.HorizontalSlider(new Rect(650,y+24,330,22),value,0,1);
            Label(1010,y+13,90,32,Mathf.RoundToInt(result*100).ToString()); return result;
        }
        void DrawSettings()
        {
            Box(new Rect(0,0,1280,720)); Label(490,40,300,60,"설정",new GUIStyle(title){alignment=TextAnchor.MiddleCenter});
            float tabsLeft=session.IsTestMode?285:410;
            if(Button(tabsLeft,115,220,settingsTab==0?"● 그래픽":"그래픽")) {settingsTab=0; GetComponent<TableVoice>().MonitorRequested=false;}
            if(Button(tabsLeft+240,115,220,settingsTab==1?"● 오디오":"오디오")) settingsTab=1;
            if(!session.IsTestMode && settingsTab==2)settingsTab=0;
            if(session.IsTestMode && Button(tabsLeft+480,115,230,settingsTab==2?"● 개발자 도구":"개발자 도구")){settingsTab=2;GetComponent<TableVoice>().MonitorRequested=false;}
            if(settingsTab==2 && session.IsTestMode)DrawDeveloperTools();
            else if(settingsTab==0) {
                Label(160,208,700,40,"디스플레이 설정",body); Row(270,"디스플레이 모드");
                if(Button(650,275,210,preferences.Fullscreen?"● 전체 화면":"전체 화면")) {preferences.Fullscreen=true;preferences.ApplyDisplay();}
                if(Button(880,275,210,!preferences.Fullscreen?"● 창모드":"창모드")) {preferences.Fullscreen=false;preferences.ApplyDisplay();}
                Label(185,350,900,60,"전체 화면: 모니터 해상도에 맞춤  ·  창모드: 1280 × 720",small);
            } else {
                if(Button(160,190,230,audioTab==0?"● 사운드":"사운드")) {audioTab=0;GetComponent<TableVoice>().MonitorRequested=false;}
                if(Button(405,190,230,audioTab==1?"● 보이스 채팅":"보이스 채팅")) audioTab=1;
                if(audioTab==0) {
                    preferences.Master=VolumeRow(265,"마스터 볼륨",preferences.Master);
                    preferences.Music=VolumeRow(335,"음악 볼륨",preferences.Music);
                    preferences.UI=VolumeRow(405,"UI 볼륨",preferences.UI);
                    Label(160,490,960,40,"로비와 인게임 음악에 함께 적용됩니다.",small);
                } else {
                    var voice=GetComponent<TableVoice>();
                    preferences.Voice=VolumeRow(255,"보이스 채팅 볼륨",preferences.Voice);
                    preferences.Microphone=VolumeRow(320,"마이크 볼륨",preferences.Microphone);
                    if(Button(160,395,960,"입력 장치: "+voice.SelectedDevice+"  ›")) voice.NextDevice();
                    if(Button(160,462,300,voice.MonitorRequested?"마이크 테스트 종료":"마이크 입력 테스트")) voice.MonitorRequested=!voice.MonitorRequested;
                    GUI.color=new Color(.15f,.18f,.18f); GUI.DrawTexture(new Rect(490,477,500,20),Texture2D.whiteTexture);
                    GUI.color=new Color(.4f,.8f,.65f); GUI.DrawTexture(new Rect(490,477,500*Mathf.Clamp01(voice.InputLevel),20),Texture2D.whiteTexture); GUI.color=Color.white;
                    Label(1010,467,100,36,(voice.InputLevel*100).ToString("0")+"%",small);
                    Label(160,531,960,48,"테스트 음성은 상대에게 전송하지 않습니다. 게임에서는 Caps Lock을 누르는 동안 무전기를 사용합니다. 상대가 말할 때는 자동으로 송신을 기다립니다.",small);
                }
                preferences.ApplyAudio();
            }
            float footerLeft=session.View!=null?160:210; float footerWidth=session.View!=null?225:270; float footerStep=footerWidth+20;
            if(Button(footerLeft,630,footerWidth,"적용 / 저장")) preferences.Save();
            if(Button(footerLeft+footerStep,630,footerWidth,"뒤로 가기")) CloseSettings();
            if(Button(footerLeft+footerStep*2,630,footerWidth,"기본값")) preferences.Defaults();
            if(session.View!=null && session.View.phase==1 && !session.IsTestMode) Label(160,575,770,30,"온라인 게임은 설정 중에도 제한시간이 계속 흐릅니다.",small);
            if(session.View!=null && Button(footerLeft+footerStep*3,630,footerWidth,"로비로 가기"))
            {
                CloseSettings(); session.Disconnect(); connectionMenu=true;
            }
        }
        public void OpenWardrobe()
        {
            if(!catalog) return;
            WardrobeOpen=true; Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
            if(!preview) {
                preview=Instantiate(catalog.basePrefab).transform; preview.name="Wardrobe preview"; preview.position=new Vector3(100,0,100); preview.rotation=Quaternion.identity;
                foreach(var t in preview.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=31;
                previewTexture=new RenderTexture(520,600,24);
                previewCamera=new GameObject("Wardrobe preview camera").AddComponent<Camera>();
                previewCamera.transform.position=new Vector3(100,1.1f,103.5f); previewCamera.transform.LookAt(new Vector3(100,1.05f,100));
                previewCamera.cullingMask=1<<31; previewCamera.targetTexture=previewTexture; previewCamera.clearFlags=CameraClearFlags.SolidColor; previewCamera.backgroundColor=new Color(.055f,.07f,.075f); previewCamera.fieldOfView=34;
                var light=new GameObject("Wardrobe preview light").AddComponent<Light>(); light.transform.SetParent(previewCamera.transform,false); light.type=LightType.Directional;light.cullingMask=1<<31;light.intensity=2;
            }
            preview.gameObject.SetActive(true); previewCamera.enabled=true; catalog.Apply(preview,outfit);
        }
        void CloseWardrobe() { WardrobeOpen=false; if(preview)preview.gameObject.SetActive(false); if(previewCamera)previewCamera.enabled=false; }
        void DrawWardrobe()
        {
            Box(new Rect(0,0,1280,720)); Label(60,30,900,50,"캐릭터 꾸미기",title);
            GUI.DrawTexture(new Rect(55,95,470,540),previewTexture,ScaleMode.ScaleToFit);
            if(Button(100,640,160,"↶ 회전")) preview.Rotate(0,-30,0);
            if(Button(280,640,160,"회전 ↷")) preview.Rotate(0,30,0);
            wardrobeScroll=GUI.BeginScrollView(new Rect(560,100,660,450),wardrobeScroll,new Rect(0,0,630,catalog.categories.Length*64));
            for(int i=0;i<catalog.categories.Length;i++) {
                var c=catalog.categories[i]; float y=i*64;
                Label(0,y+10,170,42,c.label,body);
                if(Button(170,y,55,"‹")) ChangeWear(i,-1);
                Label(235,y+12,300,42,outfit[i]<0?"미착용":WearLabel(c,outfit[i]),small);
                if(Button(560,y,55,"›")) ChangeWear(i,1);
            }
            GUI.EndScrollView();
            if(Button(560,640,200,"외형 저장")) SaveWear();
            if(Button(780,640,200,"기본 외형")) {outfit=catalog.Defaults();catalog.Apply(preview,outfit);}
            if(Button(1000,640,200,"뒤로 가기")) {SaveWear();CloseWardrobe();}
        }
        static string WearLabel(WearCategory category,int choice) {
            var option=category.options[choice];
            if(category.slot=="Body") return "기본 체형";
            if(category.slot=="Faces") return option.name.Contains("angry")?"화난 표정":option.name.Contains("happy")?"웃는 표정":"평온한 표정";
            if(option.name.Contains("Clown")) return "광대 코";
            if(option.name.Contains("Headphones")) return "헤드폰";
            if(option.name.Contains("Pacifier")) return "공갈젖꼭지";
            string label=option.group=="Outfit"?"일체형 의상":option.group=="Mascots"?"마스코트":option.group=="Socks"?"양말":option.name.Contains("Mustache")?"콧수염":category.label;
            return label+" "+(choice+1);
        }
        void ChangeWear(int i,int direction) {
            var c=catalog.categories[i]; int min=c.required?0:-1;
            outfit[i]+=direction; if(outfit[i]>=c.options.Length)outfit[i]=min; if(outfit[i]<min)outfit[i]=c.options.Length-1;
            if(c.slot!="FullBody" && c.slot!="Body" && c.slot!="Faces") {int full=System.Array.FindIndex(catalog.categories,x=>x.slot=="FullBody");if(full>=0)outfit[full]=-1;}
            catalog.Apply(preview,outfit);
        }
        void SaveWear() {var encoded=catalog.Encode(outfit); PlayerPrefs.SetString("TTT_Outfit",encoded);PlayerPrefs.Save();session.Command("look",encoded);}
        void DisposeFrontEnd() { if(preview)Destroy(preview.gameObject);if(previewCamera)Destroy(previewCamera.gameObject);if(previewTexture){previewTexture.Release();Destroy(previewTexture);} }
    }
}
