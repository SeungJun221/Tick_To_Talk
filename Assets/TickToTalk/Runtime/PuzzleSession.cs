using System;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]

namespace TickToTalk
{
    // This state exists on the dedicated server only. Never serialize it to a client.
    public sealed class BombPuzzle
    {
        public readonly string[] Display = new string[4];
        public readonly string[] Manual = new string[4];
        internal readonly string[] Answers = new string[4];
        public int Stage;
        public readonly int[] Mistakes = new int[4];
        public bool Armed;
        public double ArmedAt;
        static string Digits(int[] v) => string.Concat(v.Select(x => x.ToString()));
        public BombPuzzle(Random r)
        {
            int[] map = Enumerable.Range(0, 10).OrderBy(_ => r.Next()).ToArray();
            int[] glyphs = Enumerable.Range(0, 6).Select(_ => r.Next(10)).ToArray();
            int shift = r.Next(1, 10);
            bool reverse = r.Next(2) == 1;
            var decoded = glyphs.Select(g => (map[g] + shift) % 10).ToArray();
            if (reverse) Array.Reverse(decoded);
            Answers[0] = Digits(decoded);
            Display[0] = "01 / 치환 암호\n기호: " + string.Join("  ", glyphs.Select(g => ((char)('A' + g)).ToString())) + "\n상대에게 기호를 왼쪽부터 읽어 주세요.\n6자리 해독 결과를 입력하세요.";
            Manual[0] = "01 / 상대 암호 해독표\n" + string.Join("   ", map.Select((v, i) => ((char)('A' + i)) + "=" + v)) + "\n각 숫자에 " + shift + "을 더한 뒤 일의 자리만 사용.\n" + (reverse ? "마지막으로 6자리 전체의 순서를 뒤집는다." : "마지막 순서는 그대로 유지한다.");
            int[] wireGlyphs = Enumerable.Range(0, 6).OrderBy(_ => r.Next()).ToArray();
            int[] sequence = Enumerable.Range(0, 6).OrderBy(_ => r.Next()).ToArray();
            Answers[1] = Digits(sequence.Select(g => Array.IndexOf(wireGlyphs, g) + 1).ToArray());
            Display[1] = "02 / 배선 순서\n" + string.Join("   ", wireGlyphs.Select((v, i) => (i + 1) + ":" + (char)('K' + v))) + "\n상대가 알려주는 문자 순서대로 단자 번호를 입력하세요.\n예: 2번 단자에 K가 있으면 K는 2입니다.\n계산이나 순서 뒤집기는 없습니다.";
            Manual[1] = "02 / 상대 배선 순서\n처리 순서: " + string.Join(" → ", sequence.Select(g => ((char)('K' + g)).ToString())) + "\n위 문자 6개를 순서대로 상대에게 읽어 주세요.\n상대는 자기 폭탄에서 각 문자의 단자 번호를 찾아 입력합니다.";
            int[] grid = Enumerable.Range(0, 9).Select(_ => r.Next(10)).ToArray();
            int[] route = Enumerable.Range(0, 9).OrderBy(_ => r.Next()).Take(6).ToArray();
            Answers[2] = Digits(route.Select(v => grid[v]).ToArray());
            Display[2] = "03 / 좌표 찾기\n열 →  1   2   3\n" + string.Join("\n", Enumerable.Range(0, 3).Select(row => (row + 1) + "행   " + string.Join("   ", grid.Skip(row * 3).Take(3)))) + "\n상대가 알려주는 (행,열)의 숫자를 순서대로 입력하세요.\n숫자를 더하거나 이전 정답을 사용할 필요는 없습니다.";
            Manual[2] = "03 / 상대 좌표 경로\n(행,열): " + string.Join(" → ", route.Select(v => "(" + (v / 3 + 1) + "," + (v % 3 + 1) + ")")) + "\n행은 위에서 아래로, 열은 왼쪽에서 오른쪽으로 셉니다.\n예: (2,3)은 두 번째 줄의 세 번째 숫자입니다.\n좌표 6개를 순서대로 읽어 주세요. 칸의 숫자를 그대로 입력합니다.";
            int[] key = Enumerable.Range(0, 6).Select(_ => r.Next(10)).ToArray();
            Answers[3] = Digits(key);
            Display[3] = "04 / 동시 차단\n상대 방 매뉴얼에 있는 내 차단 코드 6자리를 입력하세요.\n계산은 없습니다. 앞자리 0도 입력하세요.\n두 사람 모두 코드를 받은 뒤 제출하세요.\n첫 제출 후 15초 안에 상대도 제출하면 마지막 출구가 열립니다.";
            Manual[3] = "04 / 상대 최종 차단 코드: " + Digits(key) + "\n이 코드는 상대방 폭탄용입니다. 숫자 그대로 읽어 주세요.\n내 코드는 상대방 방에 있습니다. 서로 코드를 먼저 전달하세요.\n15초가 지나면 준비만 해제됩니다. 같은 코드로 다시 제출하세요.";
        }
        public string SolutionReport() => string.Join("\n", Answers.Select((a,i) => "0" + (i+1) + " 정답: " + a)) +
            "\n풀이: ① 치환값+보정, 지시 시 역순 ② 지시된 문자 순서의 단자 번호\n③ 지시된 (행,열)의 숫자를 그대로 연결 ④ 상대가 알려준 차단 코드 그대로 입력";
        public bool Check(string answer) => answer != null && answer == Answers[Stage];
    }

    public sealed class RoundRules
    {
        public const double FinalWindowSeconds = 15;
        public const float FailureSequenceSeconds = 5.5f;
        public bool Exploded;
        public double FailedAt;
        public BombPuzzle[] Bombs;
        public int Phase; // 0 lobby, 1 active, 2 escaped, 3 failed, 4 exit unlocked
        public int Strikes => StrikesByRoom[0] + StrikesByRoom[1];
        public readonly int[] StrikesByRoom = new int[2];
        public double EndAt;
        int lastSharedStage;
        public int Speaker = -1;
        public void Start(int seed, double now)
        {
            var random = new Random(seed);
            Bombs = new[] { new BombPuzzle(random), new BombPuzzle(random) };
            Phase = 1; Exploded = false; FailedAt = 0; StrikesByRoom[0] = StrikesByRoom[1] = 0; lastSharedStage = 0; EndAt = now + 300; Speaker = -1;
        }
        public void Tick(double now)
        {
            if (Phase != 1) return;
            if (now >= EndAt) Fail(now);
            foreach (var b in Bombs) if (b.Armed && now - b.ArmedAt > FinalWindowSeconds) b.Armed = false;
        }
        public bool Talk(int slot, double now)
        {
            Tick(now);
            if (Phase != 1 || Speaker >= 0 || slot < 0 || slot > 1) return false;
            Speaker = slot; return true;
        }
        public void Release(double now)
        {
            Speaker = -1;
        }
        public void Fail(double now)
        {
            if(Phase != 1) return;
            Phase = 3; Speaker = -1; Exploded = true; FailedAt = now;
        }
        public string Submit(int slot, string input, double now)
        {
            Tick(now);
            if (Phase != 1) return "진행 중인 라운드가 아닙니다.";
            var b = Bombs[slot];
            if (b.Armed) return "상대의 최종 차단을 기다리는 중입니다.";
            if (!b.Check(input))
            {
                b.Mistakes[b.Stage]++; StrikesByRoom[slot]++; EndAt -= 20;
                if (b.Mistakes[b.Stage] >= 3 || now >= EndAt) Fail(now);
                return "오답 — 제한시간 20초 감소 / 현재 폭탄 기회 " + (3 - b.Mistakes[b.Stage]) + "/3";
            }
            if (b.Stage < 3)
            {
                b.Stage++;
                int sharedStage = Math.Min(Bombs[0].Stage, Bombs[1].Stage);
                if (sharedStage > lastSharedStage)
                {
                    lastSharedStage = sharedStage;
                    EndAt = now + 300;
                    return "양쪽 폭탄 해제 완료 — 다음 구역의 철창문이 열립니다. 제한시간 5분.";
                }
                return "이 구역 폭탄 해제 완료. 상대방의 해제를 기다리세요.";
            }
            b.Armed = true; b.ArmedAt = now;
            if (Bombs[1 - slot].Armed && now - Bombs[1 - slot].ArmedAt <= FinalWindowSeconds)
            { Phase = 4; Speaker = -1; return "마지막 구역의 출구가 열렸습니다."; }
            return "차단 준비 완료 — 상대가 15초 안에 제출해야 합니다.";
        }
    }
    [Serializable] public class ClientCommand
    {
        public string kind, answer;
        public float x, z, yaw;
        public bool sprint;
        public int stage = -1;
        public int inputSequence;
    }
    [Serializable] public class PlayerView
    {
        public bool departureOpen;
        public bool exploded;
        public float failureElapsed;
        public float gateLift, autoStartRemaining;
        public int sequence, slot, phase, stage, partnerStage, strikes, strikesA, strikesB, mistakesA, mistakesB, speaker, count;
        public int stageA, stageB;
        public bool readyA, readyB, armed, arun, brun, tabletA, tabletB, walkieA, walkieB;
        public int emoteA, emoteB;
        public float emoteRemainingA, emoteRemainingB;
        public float remaining, armRemaining;
        public float ax, ay, az, ayaw, bx, by, bz, byaw;
        public string display, manual, notice, history, solutions, lookA, lookB;
    }
}
