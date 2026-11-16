using System;
using System.Linq;
using UnityEngine;

namespace TickToTalk
{
    public sealed partial class NetworkSession
    {
        public int InteractionRoom => IsTestMode ? (positions[0].x >= 0 ? 1 : 0) : (View?.slot ?? 0);
        public PlayerView InteractionView(int room) => IsTestMode && room >= 0 && room < 2 ? CreateView(room) : View;
        public void SubmitInteraction(int room, string answer, int stage)
        {
            if (!IsTestMode) { Command("submit", answer); return; }
            if (room < 0 || room > 1 || Rules.Phase != 1 || !ServerNear(0, Bomb(room)) || Now - lastSubmit[room] < 1) return;
            if (stage != Rules.Bombs[room].Stage) { notices[0] = "단계가 바뀌었습니다. 새 문제를 확인하세요."; return; }
            if (answer == null || answer.Length != 6 || answer.Any(c => c < '0' || c > '9')) return;
            lastSubmit[room] = Now;
            notices[0] = (room == 0 ? "A" : "B") + " 폭탄 · " + Rules.Submit(room, answer, Now);
        }
        bool AtAssignedMicrophone(int player)
        {
            return ServerNear(player, Mic(IsTestMode ? InteractionRoom : player));
        }
    }
}
