using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TickToTalk
{
    public sealed partial class NetworkSession : MonoBehaviour
    {
        public static NetworkSession Instance;
        [NonSerialized] public PlayerView View;
        public string Status = "온라인 방을 만들거나 접속 코드를 입력하세요.";
        public bool Dedicated { get; private set; }
        [NonSerialized] public RoundRules Rules = new RoundRules();
        NetworkManager net;
        readonly ulong?[] clients = new ulong?[2];
        readonly bool[] ready = new bool[2];
        readonly Vector2[] positions = { new Vector2(-4, -5.7f), new Vector2(4, -5.7f) };
        readonly Vector2[] inputs = new Vector2[2];
        readonly float[] yaw = { 0, 0 };
        readonly double[] lastInput = new double[2], lastSubmit = { -10, -10 }, voiceWindow = new double[2];
        readonly int[] voicePackets = new int[2];
        readonly string[] notices = { "", "" };
        readonly float[] heights = new float[2], verticalSpeed = new float[2];
        readonly bool[] sprinting = new bool[2];
        readonly bool[] tablets = new bool[2];
        readonly bool[] walkies = new bool[2];
        readonly int[] emotes = new int[2];
        readonly double[] emoteUntil = new double[2];
        const double EmoteDuration = 2.5;
        readonly string[] looks = new string[2];
        WardrobeCatalog wardrobe;
        double nextSnapshot;
        int snapshotSequence;
        int inputSequence;
        readonly int[] acceptedInput = new int[2];
        int viewSequence;
        int lastMotionSequence;
        float lastMotionReceived;
        public float MotionReceivedAt => lastMotionReceived;
        double nextMotion;
        double Now => Time.realtimeSinceStartupAsDouble;
        public bool Connected => net && net.IsConnectedClient;
        void OnEnable() { Instance = this; }
        public static Vector3 Mic(int slot) => new Vector3(slot == 0 ? -1.2f : 1.2f, 0, 0);
        public static Vector3 Bomb(int slot) => new Vector3(slot == 0 ? -7.1f : 7.1f, 0, PuzzleSector(slot)*9);
        public static Vector3 Manual(int slot) => new Vector3(slot == 0 ? -4 : 4, 0, 3.6f+PuzzleSector(1-slot)*9);
        public bool Near(Vector3 target, float range = 2.2f)
        {
            if (View == null) return false;
            var p = View.slot == 0 ? new Vector3(View.ax, 0, View.az) : new Vector3(View.bx, 0, View.bz);
            return Vector3.Distance(p, target) < range;
        }
        void Awake()
        {
            Instance = this;
            View = null;
            Status = "온라인 방을 만들거나 친구의 접속 코드를 입력하세요.";
            wardrobe=Resources.Load<WardrobeCatalog>("WardrobeCatalog");
            looks[0]=looks[1]=wardrobe ? wardrobe.Encode(wardrobe.Defaults()) : "";
            Application.runInBackground = true;
            net = gameObject.AddComponent<NetworkManager>();
            var transport = gameObject.AddComponent<UnityTransport>();
            net.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false };
            net.ConnectionApprovalCallback = Approve;
            net.OnClientConnectedCallback += Joined;
            net.OnClientDisconnectCallback += Left;
            net.OnTransportFailure += TransportFailed;
            gameObject.AddComponent<TableVoice>();
        }
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Contains("-relay-host")) { CreateOnlineRoom(); return; }
            int join = Array.IndexOf(args, "-join-code");
            if (join >= 0 && join + 1 < args.Length) JoinOnlineRoom(args[join + 1]);
        }

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = Rules.Phase == 0 && net.ConnectedClientsIds.Count < 2;
            response.CreatePlayerObject = false;
            response.Pending = false;
            response.Reason = "두 플레이어가 이미 접속했거나 라운드가 진행 중입니다.";
        }
        void Register()
        {
            net.CustomMessagingManager.RegisterNamedMessageHandler("ttt.cmd", OnCommand);
            net.CustomMessagingManager.RegisterNamedMessageHandler("ttt.input", OnCommand);
            net.CustomMessagingManager.RegisterNamedMessageHandler("ttt.view", OnView);
            net.CustomMessagingManager.RegisterNamedMessageHandler("ttt.motion", OnMotion);
            net.CustomMessagingManager.RegisterNamedMessageHandler("ttt.voice", OnVoice);
            net.CustomMessagingManager.RegisterNamedMessageHandler("ttt.walkieSound", OnWalkieSound);
        }
        public void Disconnect()
        {
            IsTestMode = false; TestFlying = false; flightVertical = 0; tablets[0]=tablets[1]=false; walkies[0]=walkies[1]=false; emotes[0]=emotes[1]=0;
            connectionAttempt++; OnlineBusy = false; JoinCode = ""; UsingRelay = false;
            net.Shutdown(); View = null; lastMotionSequence = 0; lastMotionReceived = 0; GetComponent<TableVoice>().ResetPlayback(); Status = "접속을 종료했습니다.";
        }
        void Joined(ulong id)
        {
            if (!net.IsServer) { Status = "접속 완료. 플레이어 배정을 기다리는 중…"; return; }
            int s = clients[0].HasValue ? 1 : 0;
            if (clients[s].HasValue) { net.DisconnectClient(id); return; }
            clients[s] = id; ready[s] = false; emotes[s]=0; walkies[s]=false; positions[s] = new Vector2(s == 0 ? -4 : 4, -5.7f);
            Debug.Log("TTT_JOIN " + s);
        }
        void Left(ulong id)
        {
            if (!net.IsServer) { View = null; JoinCode = ""; GetComponent<TableVoice>().ResetPlayback(); Status = "방 연결이 종료되었습니다. 방장이 나갔거나 연결이 끊겼습니다. " + net.DisconnectReason; return; }
            int s = Slot(id);
            if (s < 0) return;
            clients[s] = null; ready[s] = false; tablets[s]=false; walkies[s]=false; emotes[s]=0; inputs[s] = Vector2.zero;
            if (Rules.Phase == 1 || Rules.Phase == 4) { Rules.Phase = 3; Rules.Speaker = -1; notices[1 - s] = "상대방 연결이 끊겨 라운드가 종료되었습니다."; }
            if (!clients.Any(c => c.HasValue)) Rules = new RoundRules();
        }
        int Slot(ulong id) => clients[0] == id ? 0 : clients[1] == id ? 1 : -1;
        bool ServerNear(int s, Vector3 target) => Vector2.Distance(positions[s], new Vector2(target.x, target.z)) < 2.2f;
        public void Command(string kind, string answer = "", Vector2 move = default, float angle = 0, bool sprint = false)
        {
            if (!Connected && !IsTestMode) return;
            var command = new ClientCommand { kind = kind, answer = answer, x = move.x, z = move.y, yaw = angle, sprint = sprint, inputSequence = ++inputSequence, stage = View?.stage ?? -1 };
            if(IsTestMode) { HandleCommand(0,command); return; }
            SendJson(kind == "move" ? "ttt.input" : "ttt.cmd", NetworkManager.ServerClientId, JsonUtility.ToJson(command));
        }
        void SendJson(string channel, ulong to, string json)
        {
            using (var writer = new FastBufferWriter(16384, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                var delivery = channel == "ttt.input" ? NetworkDelivery.Unreliable : channel == "ttt.view" ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.ReliableSequenced;
                net.CustomMessagingManager.SendNamedMessage(channel, to, writer, delivery);
            }
        }
        void OnCommand(ulong sender, FastBufferReader reader)
        {
            if (!net.IsServer || reader.Length > 2048) return;
            int s = Slot(sender); if (s < 0) return;
            try
            {
                reader.ReadValueSafe(out string json);
                var c = JsonUtility.FromJson<ClientCommand>(json);
                HandleCommand(s,c);
            }
            catch (Exception) { /* Malformed peer input cannot mutate the authoritative state. */ }
        }
        void HandleCommand(int s, ClientCommand c)
        {
                if (c == null) return;
                if(c.kind == "move") { if(c.inputSequence <= acceptedInput[s]) return; acceptedInput[s] = c.inputSequence; }
                if(c.kind=="look" && Rules.Phase!=1 && wardrobe && wardrobe.IsValid(c.answer)) {looks[s]=c.answer; return;}
                if (c.kind == "ready" && (Rules.Phase == 0 || Rules.Phase == 2 || Rules.Phase == 3))
                {
                    if(Rules.Phase == 3 && Rules.Exploded && Now - Rules.FailedAt < RoundRules.FailureSequenceSeconds) return;
                    ready[s] = true;
                    if (IsTestMode || (ready[0] && ready[1] && clients.All(v => v.HasValue)))
                    {
                        if(Rules.Phase==0){if(autoStartAt<0)autoStartAt=Now+2;}
                        else StartPreparedRound();
                    }
                }
                if(c.kind=="walkie")
                {
                    walkies[s] = Rules.Phase == 1 && c.answer == "1";
                    if (!walkies[s] && Rules.Speaker == s) Rules.Release(Now);
                    return;
                }
                if(c.kind=="tablet") { tablets[s]=Rules.Phase==1 && c.answer=="1"; if(tablets[s]) {inputs[s]=Vector2.zero;sprinting[s]=false;emotes[s]=0;} return; }
                if(c.kind=="emote")
                {
                    if(Rules.Phase==1 && !tablets[s] && int.TryParse(c.answer,out int selected) && selected>=1 && selected<=3)
                    {emotes[s]=selected;emoteUntil[s]=Now+EmoteDuration;}
                    return;
                }
                if (!CanMovePlayer(s)) return;
                if (Rules.Phase==0 && c.kind!="move" && c.kind!="jump") return;
                if(tablets[s] && (c.kind=="move" || c.kind=="jump" || c.kind=="submit"))return;
                if (c.kind == "move" && float.IsFinite(c.x) && float.IsFinite(c.z) && float.IsFinite(c.yaw))
                { inputs[s] = Vector2.ClampMagnitude(new Vector2(c.x, c.z), 1); yaw[s] = c.yaw % 360; lastInput[s] = Now; sprinting[s] = c.sprint; }
                if (c.kind == "jump" && !TestFlying && heights[s] <= GroundHeight(s) + .02f) verticalSpeed[s] = 5.5f;
                if (c.kind == "talk" && walkies[s] && Rules.Talk(s, Now)) BroadcastWalkieSound();
                if (c.kind == "release" && Rules.Speaker == s) Rules.Release(Now);
                if (c.kind == "submit" && ServerNear(s, Bomb(s)) && Now - lastSubmit[s] >= 1)
                {
                    lastSubmit[s] = Now;
                    if (c.stage != Rules.Bombs[s].Stage) { notices[s] = "단계가 바뀌었습니다. 새 문제를 확인하세요."; return; }
                    if (c.answer == null || c.answer.Length != 6 || c.answer.Any(ch => ch < '0' || ch > '9')) return;
                    notices[s] = Rules.Submit(s, c.answer, Now);
                }
        }
        void BroadcastWalkieSound()
        {
            // A reliable event preserves even a press/release between two snapshots.
            if(IsTestMode) { GetComponent<GamePreferences>()?.PlayWalkie(); return; }
            for(int s = 0; s < clients.Length; s++)
                if(clients[s].HasValue) SendJson("ttt.walkieSound", clients[s].Value, "");
        }
        void OnWalkieSound(ulong sender, FastBufferReader reader)
        {
            if(!net.IsClient || sender != NetworkManager.ServerClientId) return;
            GetComponent<GamePreferences>()?.PlayWalkie();
        }
        void OnView(ulong sender, FastBufferReader reader)
        {
            if (!net.IsClient || sender != NetworkManager.ServerClientId || reader.Length > 16384) return;
            reader.ReadValueSafe(out string json);
            var incoming = JsonUtility.FromJson<PlayerView>(json);
            if (incoming == null || (View != null && incoming.sequence <= View.sequence)) return;
            bool firstView = View == null;
            if (!firstView && lastMotionSequence > 0)
            {
                incoming.ax = View.ax; incoming.ay = View.ay; incoming.az = View.az; incoming.ayaw = View.ayaw;
                incoming.bx = View.bx; incoming.by = View.by; incoming.bz = View.bz; incoming.byaw = View.byaw;
                incoming.arun = View.arun; incoming.brun = View.brun;
            }
            View = incoming;
            if (firstView) {Debug.Log("TTT_CLIENT_ASSIGNED " + View.slot); if(wardrobe)Command("look",PlayerPrefs.GetString("TTT_Outfit",wardrobe.Encode(wardrobe.Defaults())));}
        }
        void OnMotion(ulong sender, FastBufferReader reader)
        {
            if (!net.IsClient || sender != NetworkManager.ServerClientId || View == null || reader.Length > 256) return;
            reader.ReadValueSafe(out int number);
            if (number <= lastMotionSequence) return;
            lastMotionSequence = number;
            reader.ReadValueSafe(out float ax); reader.ReadValueSafe(out float ay); reader.ReadValueSafe(out float az); reader.ReadValueSafe(out float ayaw);
            reader.ReadValueSafe(out float bx); reader.ReadValueSafe(out float by); reader.ReadValueSafe(out float bz); reader.ReadValueSafe(out float byaw);
            reader.ReadValueSafe(out bool arun); reader.ReadValueSafe(out bool brun);
            View.ax = ax; View.ay = ay; View.az = az; View.ayaw = ayaw;
            View.bx = bx; View.by = by; View.bz = bz; View.byaw = byaw;
            View.arun = arun; View.brun = brun;
            lastMotionReceived = Time.unscaledTime;
        }
        void Update()
        {
            if (!net.IsServer && !IsTestMode) return;
            UpdateEscapeFlow();
            Rules.Tick(Now);
            for (int s = 0; s < 2; s++) if(Rules.Phase!=1 || Now>=emoteUntil[s])emotes[s]=0;
            for (int s = 0; s < 2; s++)
            {
                if(IsTestMode && s==0 && CanMovePlayer(s)) {if(TestFlying)MoveTestFlight(Time.deltaTime);else MoveTestGround(Time.deltaTime);continue;}
                if(CanMovePlayer(s))MoveGround(s,Time.deltaTime,true);
                else if(Rules.Phase != 3) {heights[s]=0;verticalSpeed[s]=0;}
            }
            UpdateEscapeFlow();
            if (Rules.Speaker >= 0 && !walkies[Rules.Speaker]) Rules.Release(Now);
            if(IsTestMode) {View=CreateView(0);lastMotionReceived=Time.unscaledTime;return;}
            if (Now >= nextMotion)
            {
                nextMotion = Now + .05;
                int motionNumber = ++snapshotSequence;
                for (int s = 0; s < 2; s++) if (clients[s].HasValue) SendMotion(clients[s].Value, motionNumber);
            }
            if (Now < nextSnapshot) return;
            nextSnapshot = Now + .2;
            viewSequence++;
            for (int s = 0; s < 2; s++) if (clients[s].HasValue)
            {
                var v = CreateView(s);
                SendJson("ttt.view", clients[s].Value, JsonUtility.ToJson(v));
            }
        }
        void SendMotion(ulong to, int number)
        {
            using (var writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteValueSafe(number);
                writer.WriteValueSafe(positions[0].x); writer.WriteValueSafe(heights[0]); writer.WriteValueSafe(positions[0].y); writer.WriteValueSafe(yaw[0]);
                writer.WriteValueSafe(positions[1].x); writer.WriteValueSafe(heights[1]); writer.WriteValueSafe(positions[1].y); writer.WriteValueSafe(yaw[1]);
                writer.WriteValueSafe(sprinting[0]); writer.WriteValueSafe(sprinting[1]);
                net.CustomMessagingManager.SendNamedMessage("ttt.motion", to, writer, NetworkDelivery.Unreliable);
            }
        }
        PlayerView CreateView(int s)
        {
                var b = Rules.Bombs == null ? null : Rules.Bombs[s];
                var other = Rules.Bombs == null ? null : Rules.Bombs[1 - s];
                return new PlayerView {
                    exploded=Rules.Exploded, failureElapsed=Rules.Exploded?(float)Math.Max(0,Now-Rules.FailedAt):0,
                    departureOpen=departureOpen, gateLift=gateLift, autoStartRemaining=autoStartAt<0?-1:(float)Math.Max(0,autoStartAt-Now),
                    lookA=looks[0], lookB=looks[1], tabletA=Rules.Phase==1 && tablets[0], tabletB=Rules.Phase==1 && tablets[1],
                    walkieA=Rules.Phase==1 && walkies[0], walkieB=Rules.Phase==1 && walkies[1],
                    emoteA=Rules.Phase==1?emotes[0]:0, emoteB=Rules.Phase==1?emotes[1]:0,
                    emoteRemainingA=Rules.Phase==1&&emotes[0]!=0?(float)Math.Max(0,emoteUntil[0]-Now):0,
                    emoteRemainingB=Rules.Phase==1&&emotes[1]!=0?(float)Math.Max(0,emoteUntil[1]-Now):0,
                    sequence = viewSequence, slot = s, phase = Rules.Phase, count = IsTestMode ? 1 : clients.Count(c => c.HasValue), readyA = ready[0], readyB = ready[1],
                    remaining = (float)Math.Max(0, Rules.EndAt - Now), strikes = Rules.StrikesByRoom[s],
                    strikesA=Rules.StrikesByRoom[0], strikesB=Rules.StrikesByRoom[1],
                    mistakesA=Rules.Bombs == null ? 0 : Rules.Bombs[0].Mistakes[Rules.Bombs[0].Stage],
                    mistakesB=Rules.Bombs == null ? 0 : Rules.Bombs[1].Mistakes[Rules.Bombs[1].Stage],
                    stageA=Rules.Bombs == null ? 0 : Rules.Bombs[0].Stage,
                    stageB=Rules.Bombs == null ? 0 : Rules.Bombs[1].Stage,
                    speaker = Rules.Speaker,
                    stage = b?.Stage ?? 0, partnerStage = other?.Stage ?? 0, armed = b?.Armed ?? false,
                    armRemaining = b != null && b.Armed ? (float)Math.Max(0, RoundRules.FinalWindowSeconds - (Now - b.ArmedAt)) : 0,
                    ay = heights[0], by = heights[1], arun = sprinting[0], brun = sprinting[1],
                    ax = positions[0].x, az = positions[0].y, ayaw = yaw[0], bx = positions[1].x, bz = positions[1].y, byaw = yaw[1],
                    history = b == null ? "" : string.Join(" / ", b.Answers.Take(b.Stage).Select((a,i) => (i+1) + ": " + a)),
                    solutions = Rules.Phase == 3 && b != null ? "A 폭탄\n" + Rules.Bombs[0].SolutionReport() + "\n\nB 폭탄\n" + Rules.Bombs[1].SolutionReport() : "",
                    display = b?.Display[b.Stage] ?? "", manual = other == null ? "" : other.Manual[other.Stage], notice = notices[s]
                };
        }
        public void SendVoice(short[] pcm, uint sequence)
        {
            if (!Connected || View == null || View.speaker != View.slot) return;
            using (var writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteValueSafe(sequence);
                foreach (short sample in pcm) writer.WriteValueSafe(sample);
                net.CustomMessagingManager.SendNamedMessage("ttt.voice", NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
        }
        void OnVoice(ulong sender, FastBufferReader reader)
        {
            // 20 ms, 16 kHz, mono PCM16: 320 samples + sequence number.
            if (reader.Length - reader.Position != 644) return;
            if (net.IsServer)
            {
                int s = Slot(sender);
                if (s < 0 || Rules.Phase != 1 || Rules.Speaker != s || !walkies[s]) return;
                if (Now - voiceWindow[s] >= 1) { voiceWindow[s] = Now; voicePackets[s] = 0; }
                if (++voicePackets[s] > 65 || !clients[1 - s].HasValue) return;
                reader.ReadValueSafe(out uint seq);
                if (net.IsHost && clients[1 - s] == net.LocalClientId)
                {
                    var samples = new float[320];
                    for (int i = 0; i < samples.Length; i++) { reader.ReadValueSafe(out short sample); samples[i] = sample / 32768f; }
                    GetComponent<TableVoice>().Receive(samples, seq);
                    return;
                }
                using (var writer = new FastBufferWriter(1024, Allocator.Temp))
                {
                    writer.WriteValueSafe(seq);
                    for (int i = 0; i < 320; i++) { reader.ReadValueSafe(out short sample); writer.WriteValueSafe(sample); }
                    net.CustomMessagingManager.SendNamedMessage("ttt.voice", clients[1 - s].Value, writer, NetworkDelivery.Unreliable);
                }
            }
            // Server already validates speaker, distance and talk deadline. A delayed UI snapshot must not discard the first syllable.
            else if (sender == NetworkManager.ServerClientId && View != null && View.phase == 1)
            {
                reader.ReadValueSafe(out uint seq);
                var samples = new float[320];
                for (int i = 0; i < samples.Length; i++) { reader.ReadValueSafe(out short v); samples[i] = v / 32768f; }
                GetComponent<TableVoice>().Receive(samples, seq);
            }
        }
        void OnDestroy()
        {
            connectionAttempt++;
            if (net) { net.OnClientConnectedCallback -= Joined; net.OnClientDisconnectCallback -= Left; net.OnTransportFailure -= TransportFailed; }
            if (Instance == this) Instance = null;
        }
    }
}
