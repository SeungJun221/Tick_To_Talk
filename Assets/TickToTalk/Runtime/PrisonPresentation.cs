using UnityEngine;
using UnityEngine.InputSystem;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation : MonoBehaviour
    {
        public Transform playerA, playerB, doorA, doorB;
        public Camera eye;
        public Light alarmA, alarmB;
        public Renderer glass;
        public Transform indicatorA, indicatorB;
        NetworkSession session;
        FailureCinematic failure;
        public bool FailureActive => failure && failure.Active;
        string answer = "", panel = "";
        string roomCode = "";
        bool showSolutions;
        float bombPromptAlpha;
        bool CanControlMovement => session.View!=null && (session.View.phase==1 || session.View.phase==4 || (session.View.phase==0 && GetComponent<CellSequence>() && GetComponent<CellSequence>().StandingReady));
        Vector2 solutionScroll;
        float heading = 90, pitch, nextInput;
        int assigned = -1, oldStage = -1, panelRoom, oldPanelRoom = -1;
        Font font;
        [System.NonSerialized] GUIStyle title, body, small, button, field;
        Texture2D dark, accent;
        Vector3 previousA, previousB;
        Vector3 motionVelocityA, motionVelocityB;
        readonly MotionBuffer framesA = new MotionBuffer(), framesB = new MotionBuffer();
        readonly PlayerMotionState motionA = new PlayerMotionState(), motionB = new PlayerMotionState();
        void Start()
        {
            session = GetComponent<NetworkSession>();
            failure = gameObject.AddComponent<FailureCinematic>();
            title = body = small = button = field = null;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 20);
            dark = Make(new Color(.035f, .055f, .065f, .96f));
            accent = Make(new Color(.12f, .27f, .29f, 1));
            StartFrontEnd();
            StartTablet();
            StartWalkie();
            StartCoroutine(InitializeManualScreens());
            if (!GetComponent<CellSequence>()) gameObject.AddComponent<CellSequence>();
            InitWorldIndicators();
            if (!GetComponent<PrisonMirrors>()) gameObject.AddComponent<PrisonMirrors>();
            if (!GetComponent<PrisonSurveillance>()) gameObject.AddComponent<PrisonSurveillance>();
            foreach (var actor in new[] { playerA, playerB })
                actor.GetComponentInChildren<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            previousA = playerA.position; previousB = playerB.position;
        }
        Texture2D Make(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
        void Update()
        {
            if (!session || session.Dedicated) return;
            if(failure && failure.UpdateSequence(session.View))
            {
                CloseTablet(); CloseEmoteMenu(); CloseInteractionPanel(); LowerWalkie();
                if(SettingsOpen) CloseSettings();
                if(WardrobeOpen) CloseWardrobe();
                assigned = -1; framesA.Clear(); framesB.Clear(); motionA.Reset(); motionB.Reset();
                Cursor.lockState = CursorLockMode.None; Cursor.visible = failure.Finished;
                return;
            }
            UpdateTablet();
            UpdateFrontEnd();
            UpdateEmoteMenu();
            UpdateWalkie();
            var v = session.View;
            if (v == null) { CloseInteractionPanel(); ResetManualScreens(); LowerWalkie(); CloseEmoteMenu(); HideWorldIndicators(); assigned = -1; framesA.Clear(); framesB.Clear(); motionVelocityA = motionVelocityB = Vector3.zero; motionA.Reset(); motionB.Reset(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            if (assigned != v.slot)
            {
                assigned = v.slot; heading = 0;
                foreach (var r in (v.slot == 0 ? playerA : playerB).GetComponentsInChildren<Renderer>()) r.enabled = v.phase == 0;
                foreach (var r in (v.slot == 0 ? playerB : playerA).GetComponentsInChildren<Renderer>()) r.enabled = !session.IsTestMode;
            }
            Animate(playerA, new Vector3(v.ax, v.ay, v.az), v.ayaw, v.arun, motionA, ref previousA, ref motionVelocityA);
            Animate(playerB, new Vector3(v.bx, v.by, v.bz), v.byaw, v.brun, motionB, ref previousB, ref motionVelocityB);
            UpdateWorldIndicators(v);
            UpdateManualScreens(v);
            int interactionRoom = session.InteractionRoom;
            bool showBombPrompt=v.phase==1 && panel=="" && !BlocksGameplay && session.Near(NetworkSession.Bomb(interactionRoom));
            bombPromptAlpha=Mathf.MoveTowards(bombPromptAlpha,showBombPrompt?1f:0f,Time.unscaledDeltaTime*(showBombPrompt?3f:1.2f));
            if(panel=="bomb"&&!session.Near(NetworkSession.Bomb(panelRoom))){CloseInteractionPanel();if(!BlocksGameplay){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}}
            var keyboard = Keyboard.current; var mouse = Mouse.current;

            if(session.IsTestMode) {
                bool control=!BlocksGameplay && panel=="" && Cursor.lockState==CursorLockMode.Locked && keyboard!=null;
                if(control && keyboard.vKey.wasPressedThisFrame) session.ToggleTestFlight();
                session.SetTestVertical(control ? (keyboard.spaceKey.isPressed?1:0)-(keyboard.leftCtrlKey.isPressed||keyboard.rightCtrlKey.isPressed?1:0) : 0);
            }
            if (!BlocksGameplay && v.phase == 1 && keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (panel != "") CloseInteractionPanel();
                else if (session.Near(NetworkSession.Bomb(interactionRoom))) {panelRoom=interactionRoom;panel="bomb";ResetBombTablet();}
                else if (session.Near(new Vector3(interactionRoom==0?-2.22f:2.22f,0,-3.6f),1.8f)) panel="controls";
                Cursor.lockState = panel == "" ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = panel != "";
            }
            if (suppressLookFrames > 0) suppressLookFrames--;
            else if (!BlocksGameplay && panel == "" && CanControlMovement && Cursor.lockState == CursorLockMode.Locked && mouse != null)
            { heading += mouse.delta.ReadValue().x * .12f; pitch = Mathf.Clamp(pitch - mouse.delta.ReadValue().y * .12f, -65, 65); }
            var local = v.slot == 0 ? playerA : playerB;
            var cell = GetComponent<CellSequence>();
            if (v.phase != 0 || !cell || cell.StandingReady)
            {
                eye.transform.position = local.position + Vector3.up * 1.62f;
                eye.transform.rotation = Quaternion.Euler(pitch, heading, 0);
            }
            if (v.phase != 3) showSolutions = false;
            var interaction = session.InteractionView(panelRoom);
            if (interaction.stage != oldStage || panelRoom != oldPanelRoom) { answer=""; oldStage=interaction.stage; oldPanelRoom=panelRoom; }
            if (!BlocksGameplay && CanControlMovement && panel == "" && Cursor.lockState == CursorLockMode.Locked && keyboard != null && keyboard.spaceKey.wasPressedThisFrame && !session.TestFlying) session.Command("jump");
            if (Time.unscaledTime >= nextInput && CanControlMovement)
            {
                nextInput = Time.unscaledTime + .05f;
                var input = Vector2.zero;
                if (!BlocksGameplay && panel == "" && Cursor.lockState == CursorLockMode.Locked && keyboard != null)
                {
                    input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                    input.y = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                }
                Vector3 world = Quaternion.Euler(0, heading, 0) * new Vector3(input.x, 0, input.y);
                session.Command("move", "", new Vector2(world.x, world.z), heading, keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed));
            }
            if (!CanControlMovement) { CloseInteractionPanel(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if(v.phase==4)CloseInteractionPanel();
            float pulse = v.phase == 3 ? 3 : v.remaining < 60 && v.phase == 1 ? 1.5f + Mathf.Sin(Time.time * 5) : .4f;
            if (Time.unscaledTime >= WarningUntil(0)) { alarmA.color = new Color(1, .1f, .04f); alarmA.intensity = pulse; }
            if (Time.unscaledTime >= WarningUntil(1)) { alarmB.color = new Color(1, .1f, .04f); alarmB.intensity = pulse; }
        }
        void Animate(Transform actor, Vector3 target, float angle, bool running, PlayerMotionState motion, ref Vector3 previous, ref Vector3 velocity)
        {
            var frames = actor == playerA ? framesA : framesB;
            frames.Push(target, angle, session.MotionReceivedAt);
            if (!session.IsTestMode) target = frames.Sample(Time.unscaledTime - .1f, out angle);
            actor.position = target;
            actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.Euler(0, angle, 0), Time.deltaTime * 12);
            var anim = actor.GetComponentInChildren<Animator>();
            if (anim && anim.runtimeAnimatorController)
            {
                float speed = Vector2.Distance(new Vector2(actor.position.x, actor.position.z), new Vector2(previous.x, previous.z)) / Mathf.Max(.001f, Time.deltaTime);
                string state = motion.Select(target.y, speed, running, Time.time);
                if (motion.AppliedState != state) {
                    anim.CrossFadeInFixedTime(state, .06f, 0, 0);
                    motion.AppliedState = state;
                }
            }
            previous = actor.position;
        }
        void Styles()
        {
            if (title != null && title.fontSize == 30 && title.font == font) return;
            title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true }; title.normal.textColor = new Color(.88f, .95f, .95f);
            body = new GUIStyle(title) { fontSize = 19, fontStyle = FontStyle.Normal }; body.normal.textColor = new Color(.78f, .85f, .86f);
            small = new GUIStyle(body) { fontSize = 15 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 19, padding = new RectOffset(15, 15, 10, 10) };
            button.normal.background = accent; button.normal.textColor = Color.white;
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 24, padding = new RectOffset(12, 12, 8, 8) };
        }
        void Box(Rect rect) { GUI.DrawTexture(rect, dark); }
        void Label(float x, float y, float w, float h, string text, GUIStyle style = null) { GUI.Label(new Rect(x, y, w, h), text, style ?? body); }
        bool Button(float x, float y, float w, string text) { bool clicked=GUI.Button(new Rect(x,y,w,46),text,button); if(clicked && preferences)preferences.Click(); return clicked; }
        void OnGUI()
        {
            if (!session) return;
            GUI.enabled = true;
            GUI.color = Color.white;
            GUI.contentColor = Color.white;
            GUI.backgroundColor = Color.white;
            Styles();
            if(failure && failure.Active && !failure.Finished) { failure.DrawOverlay(font); return; }
            DrawManualScreens();
            if(SettingsOpen || WardrobeOpen) { GUI.color=new Color(.025f,.035f,.04f,1); GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture); GUI.color=Color.white; }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            if(DrawFrontEnd()) return;
            var v = session.View;
            if (v == null)
            {
                Box(new Rect(70, 100, 600, 470));
                Label(105, 130, 530, 85, "Tick To Talk", new GUIStyle(title){fontSize=46});
                Label(105, 210, 530, 60, session.Status, small);
                GUI.enabled = !session.OnlineBusy;
                if (Button(105, 285, 530, "온라인 방 만들기")) session.CreateOnlineRoom();
                Label(105, 350, 530, 30, "친구의 접속 코드 (영문·숫자)", small);
                roomCode = GUI.TextField(new Rect(105, 390, 335, 47), roomCode, 12, field).ToUpperInvariant();
                if (Button(455, 390, 180, "코드로 참가")) session.JoinOnlineRoom(roomCode);
                if(Button(105,480,530,"뒤로 가기")) {session.Disconnect();connectionMenu=false;}
                if(Button(865,635,350,"테스트 모드 · 혼자 시작")) {session.StartTestMode();Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
                GUI.enabled = true;
                if (session.OnlineBusy && Button(105,590,530,"연결 취소")) session.Disconnect();
                return;
            }
            if (showSolutions && v.phase == 3) {
                Box(new Rect(150, 115, 980, 530));
                solutionScroll = GUI.BeginScrollView(new Rect(175, 140, 930, 420), solutionScroll, new Rect(0,0,900,620));
                Label(0, 0, 890, 620, v.solutions); GUI.EndScrollView();
                if (Button(450, 580, 380, "정답 닫기")) showSolutions = false;
                return;
            }
            if (v.phase == 1)
            {
                var timerStyle = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 34 };
                timerStyle.normal.textColor = v.remaining <= 30 ? new Color(1f, .32f, .28f) : Color.white;
                int seconds = Mathf.CeilToInt(v.remaining);
                Label(465, 15, 350, 56, (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"), timerStyle);
            }
            if (session.IsTestMode) Label(25, 665, 150, 35, "TEST", small);
            if (v.phase == 0)
            {
                // Keep the cell visible throughout standing and automatic departure.
                if (!session.IsTestMode && v.count < 2 && session.UsingRelay && !string.IsNullOrEmpty(session.JoinCode))
                {
                    Label(30, 25, 390, 45, "접속 코드  " + session.JoinCode, body);
                }
                return;
            }
            if (v.phase == 2 || v.phase == 3)
            {
                Box(new Rect(320, 195, 640, 425));
                Label(355, 225, 570, 60, v.phase == 2 ? "탈출 성공" : "탈출 실패", title);
                Label(355, 295, 570, 100, v.phase == 2 ? "서로의 목소리가 두 사람을 살렸습니다." : "시간 초과, 현재 폭탄 오답 3회 또는 연결 종료.\n다시 준비하면 새로운 퍼즐이 생성됩니다.");
                if (Button(355, 445, 270, "다시 시작 준비")) session.Command("ready");
                if (Button(650, 445, 270, "연결 종료")) session.Disconnect();
                if (v.phase == 3 && Button(355, 510, 565, "두 폭탄의 정답 · 풀이 보기")) showSolutions = true;
                return;
            }

            if (TabletOpen) { DrawTablet(); DrawWalkieTabletOverlay(); return; }
            if (emoteMenuOpen) { DrawEmoteMenu(); return; }
            if (panel != "")
            {
                if(panel=="controls") {
                    Box(new Rect(250,145,780,490));
                    Label(285,175,710,50,"조작 안내",title);
                    Label(285,245,710,130,"T  태블릿 열기 / 닫기\n마우스 왼쪽 버튼을 누른 채 필기\n색상 · 굵기 · 지우개 선택 / 메모 자동 저장");
                    Label(285,395,710,120,"`  감정표현 메뉴 (숫자 1 왼쪽 키)\nCaps Lock  누르는 동안 무전기 송신\n상대 발언 중 자동 대기 · 끝나면 바로 송신\n원하는 표현을 마우스로 클릭");
                    Label(285,565,710,35,"E: 안내 닫기",small);return;
                }
                
                var puzzle=session.InteractionView(panelRoom);
                string bombTitle=(session.IsTestMode?(panelRoom==0?"A 구역 / ":"B 구역 / "):"")+"폭탄 / 보안 단말";
                Box(new Rect(230,80,820,630));
                Label(265,98,750,42,bombTitle,title);
                Label(265,140,750,180,puzzle.display);
                string panelStatus=(puzzle.armed?"동시 차단 잔여 "+puzzle.armRemaining.ToString("0.0")+"초   ·   ":"")
                    +"완료한 내 모듈 정답: "+puzzle.history+"   ·   E: 닫기";
                Label(265,325,750,28,panelStatus,small);
                answer=GUI.TextField(new Rect(265,365,455,48),answer,6,field);
                if(Button(740,365,275,puzzle.armed?"동시 차단 대기":"6자리 제출"))
                    session.SubmitInteraction(panelRoom,answer,puzzle.stage);
                DrawBombTablet(bombTitle);
                DrawWalkieTabletOverlay();
            }
            else
            {
                Label(632, 350, 25, 25, "+");
                DrawBombInteractionPrompt();
                if (Cursor.lockState != CursorLockMode.Locked && Button(480, 475, 320, "클릭하여 시점 조작")) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
                DrawWalkieTabletOverlay();
            }
        }
        void VoiceControls(float x, float y) {
            var voice = GetComponent<TableVoice>();
            if (Button(x, y, 530, "입력: " + voice.SelectedDevice + " (클릭하여 변경)")) voice.NextDevice();
            Label(x, y+47, 550, 28, voice.Diagnostics, small);
        }
        void OnDestroy() { DisposeManualScreens(); DisposeTablet(); DisposeWalkie(); DisposeEmoteMenu(); DisposeWorldIndicators(); DisposeFrontEnd(); Cursor.lockState = CursorLockMode.None; Cursor.visible = true; if (dark) Destroy(dark); if (accent) Destroy(accent); if (font) Destroy(font); }
        void DrawBombInteractionPrompt()
        {
            if(bombPromptAlpha<=.001f)return;
            float visibility=Mathf.SmoothStep(0,1,bombPromptAlpha);
            float y=Mathf.Lerp(625f,565f,visibility);
            Color previousColor=GUI.color;
            GUI.color=new Color(1,1,1,visibility*.94f);
            GUI.DrawTexture(new Rect(405,y,470,62),dark);
            var promptStyle=new GUIStyle(body)
            {
                alignment=TextAnchor.MiddleCenter,
                fontSize=20,
                fontStyle=FontStyle.Bold
            };
            promptStyle.normal.textColor=Color.white;
            GUI.Label(new Rect(425,y+7,430,48),"E를 눌러 폭탄 해체 단말과 상호작용",promptStyle);
            GUI.color=previousColor;
        }
        void CloseInteractionPanel()
        {
            if(panel=="bomb")
            {
                ResetBombTablet();
                SaveNotes();
            }
            panel="";
        }
}
}
