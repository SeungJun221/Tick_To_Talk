using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace TickToTalk
{
    public sealed partial class NetworkSession
    {
        public string JoinCode { get; private set; } = "";
        public bool OnlineBusy { get; private set; }
        public bool UsingRelay { get; private set; }
        public bool IsRoomHost => net && net.IsHost;
        int connectionAttempt;
        Task cloudInitialization;

        async Task InitializeCloud()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // Persist one anonymous identity per installation; tests can request separate profiles.
                string profile = "ttt";
                var args = Environment.GetCommandLineArgs();
                int profileIndex = Array.IndexOf(args, "-ugs-profile");
                if (profileIndex >= 0 && profileIndex + 1 < args.Length)
                {
                    string candidate = args[profileIndex + 1];
                    if (candidate.Length > 0 && candidate.Length <= 24 && candidate.All(c => char.IsLetterOrDigit(c) && c < 128)) profile = candidate;
                }
                var options = new InitializationOptions().SetProfile(profile);
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        async Task EnsureCloud()
        {
            if (cloudInitialization == null || cloudInitialization.IsFaulted || cloudInitialization.IsCanceled)
                cloudInitialization = InitializeCloud();
            await cloudInitialization;
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
        public async void CreateOnlineRoom() { await OpenOnlineRoom(true, ""); }
        public async void JoinOnlineRoom(string code) { await OpenOnlineRoom(false, code); }
        public async Task<bool> OpenOnlineRoom(bool create, string code)
        {
            if (IsTestMode || !net || net.IsListening || net.ShutdownInProgress || OnlineBusy) return false;
            code = (code ?? "").Trim().ToUpperInvariant();
            if (!create && (code.Length < 4 || code.Length > 12 || code.Any(c => !((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))))
            { Status = "친구가 공유한 영문·숫자 접속 코드를 확인하세요."; return false; }
            int attempt = ++connectionAttempt;
            OnlineBusy = true; JoinCode = ""; Dedicated = false;
            Status = create ? "Unity Relay에 온라인 방을 만드는 중…" : "접속 코드로 방을 찾는 중…";
            try
            {
                await WithTimeout(EnsureCloud());
                if (!Current(attempt)) return false;
                var transport = GetComponent<UnityTransport>();
                if (create)
                {
                    var allocation = await WithTimeout(RelayService.Instance.CreateAllocationAsync(1));
                    if (!Current(attempt)) return false;
                    string assignedCode = await WithTimeout(RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId));
                    if (!Current(attempt)) return false;
                    ResetRound(); UsingRelay = true;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                    if (!net.StartHost()) throw new InvalidOperationException("방장 연결을 시작하지 못했습니다.");
                    Register(); JoinCode = assignedCode;
                    Status = "온라인 방 생성 완료. 접속 코드를 친구에게 공유하세요.";
                    Debug.Log("TTT_RELAY_HOST_READY");
                    Debug.Log("TTT_RELAY_CODE " + JoinCode);
                }
                else
                {
                    var allocation = await WithTimeout(RelayService.Instance.JoinAllocationAsync(code));
                    if (!Current(attempt)) return false;
                    ResetRound(); UsingRelay = true;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
                    if (!net.StartClient()) throw new InvalidOperationException("참가 연결을 시작하지 못했습니다.");
                    Register(); JoinCode = code; Status = "방장과 연결 중…";
                    Debug.Log("TTT_RELAY_JOIN_STARTED");
                }
                return true;
            }
            catch (Exception e)
            {
                if (!Current(attempt)) return false;
                if (net.IsListening) net.Shutdown();
                JoinCode = ""; UsingRelay = false; View = null;
                Status = FriendlyCloudError(e);
                Debug.LogWarning("TTT_RELAY_ERROR " + e.GetType().Name + ": " + e.Message);
                return false;
            }
            finally { if (this && attempt == connectionAttempt) OnlineBusy = false; }
        }
        bool Current(int attempt) => this && net && attempt == connectionAttempt;
        static async Task WithTimeout(Task task)
        {
            if (await Task.WhenAny(task, Task.Delay(30000)) != task) throw new TimeoutException();
            await task;
        }
        static async Task<T> WithTimeout<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(30000)) != task) throw new TimeoutException();
            return await task;
        }
        static string FriendlyCloudError(Exception e)
        {
            if (e is TimeoutException) return "클라우드 응답 시간이 초과되었습니다. 인터넷 연결을 확인하고 다시 시도하세요.";
            var message = e.Message.ToLowerInvariant();
            if (message.Contains("join code") || message.Contains("not found") || message.Contains("404"))
                return "방을 찾지 못했습니다. 코드가 정확한지, 방장이 게임을 켜 두었는지 확인하세요.";
            if (message.Contains("capacity") || message.Contains("full")) return "이미 두 명이 접속한 방입니다.";
            if (message.Contains("403") || message.Contains("forbidden") || message.Contains("not enabled"))
                return "Unity 클라우드에서 연결을 거절했습니다. 프로젝트의 Authentication·Relay 활성화 상태를 확인하세요.";
            return "온라인 연결 실패: " + e.Message;
        }
        void ResetRound()
        {
            Rules = new RoundRules(); ResetCellFlow(); View = null; nextSnapshot = nextMotion = 0;
            inputSequence = 0; acceptedInput[0] = acceptedInput[1] = 0;
            snapshotSequence = viewSequence = lastMotionSequence = 0; lastMotionReceived = 0;
            for (int s = 0; s < 2; s++)
            {
                looks[s]=wardrobe ? wardrobe.Encode(wardrobe.Defaults()) : "";
                clients[s] = null; ready[s] = false; inputs[s] = Vector2.zero;
                positions[s] = new Vector2(s == 0 ? -4 : 4, -5.7f); yaw[s] = 0;
                tablets[s]=false; walkies[s]=false; heights[s] = verticalSpeed[s] = 0; sprinting[s] = false; lastInput[s] = 0; lastSubmit[s] = -10; voicePackets[s] = 0; voiceWindow[s] = 0; notices[s] = "";
            }
            GetComponent<TableVoice>().ResetPlayback();
        }
        void TransportFailed()
        {
            connectionAttempt++; OnlineBusy = false; JoinCode = ""; UsingRelay = false; View = null;
            Status = "서버 연결을 유지하지 못했습니다. 잠시 후 방을 다시 만들거나 참가하세요.";
            GetComponent<TableVoice>().ResetPlayback();
        }
    }
}
