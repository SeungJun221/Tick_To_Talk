using System;
using System.Linq;
using System.Text.RegularExpressions;
using TickToTalk;

int checks = 0;
void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
string Digits(System.Collections.Generic.IEnumerable<int> items) => string.Concat(items);
for (int seed = 0; seed < 2000; seed++)
{
    var rules = new RoundRules(); rules.Start(seed, 0);
    foreach (var b in rules.Bombs)
    {
        // Derive the solution exclusively from the two public player-facing texts.
        var mapping = Regex.Matches(b.Manual[0], @"([A-J])=(\d)").ToDictionary(m => m.Groups[1].Value[0], m => int.Parse(m.Groups[2].Value));
        int shift = int.Parse(Regex.Match(b.Manual[0], @"숫자에 (\d)").Groups[1].Value);
        var glyphs = Regex.Matches(b.Display[0].Split('\n')[1], "[A-J]").Select(m => m.Value[0]).ToArray();
        int[] cipher = glyphs.Select(g => (mapping[g] + shift) % 10).ToArray();
        if (b.Manual[0].Contains("뒤집는다")) Array.Reverse(cipher);
        Check(Digits(cipher) == b.Answers[0], "Cipher is not solvable from the displayed manual");
        var terminals = Regex.Matches(b.Display[1], @"(\d):([K-P])").ToDictionary(m => m.Groups[2].Value, m => int.Parse(m.Groups[1].Value));
        var sequence = Regex.Matches(b.Manual[1].Split('\n')[1], "[K-P]").Select(m => m.Value).ToArray();
        Check(sequence.Length == 6 && sequence.Distinct().Count() == 6, "Six unique wire instructions");
        Check(Digits(sequence.Select(g => terminals[g])) == b.Answers[1], "Wire order mismatch");
        var cells = Regex.Matches(b.Display[2], @"\d행\s+(\d)\s+(\d)\s+(\d)").SelectMany(m => Enumerable.Range(1, 3).Select(i => int.Parse(m.Groups[i].Value))).ToArray();
        var route = Regex.Matches(b.Manual[2].Split('\n')[1], @"\((\d),(\d)\)").Select(m => (int.Parse(m.Groups[1].Value) - 1) * 3 + int.Parse(m.Groups[2].Value) - 1).ToArray();
        Check(cells.Length == 9 && route.Length == 6, "Grid and route size");
        Check(Digits(route.Select(cell => cells[cell])) == b.Answers[2], "Coordinate lookup mismatch");
        var key = Regex.Match(b.Manual[3], @"차단 코드: (\d{6})").Groups[1].Value;
        Check(key == b.Answers[3], "Final key mismatch");
        Check(b.Answers.All(answer => answer.Length == 6 && answer.All(char.IsDigit)), "Six digit answers including leading zero");
    }
    Check(rules.Talk(0, 0), "Token start"); Check(!rules.Talk(1, 1), "Half duplex"); rules.Tick(10);
    Check(rules.Speaker == -1 && !rules.Talk(0, 19.99), "Cooldown"); Check(rules.Talk(1, 20), "Cooldown expiry");
    rules.Release(21); Check(!rules.Talk(0, 30.99), "Early release cooldown");
    for (int s = 0; s < 2; s++) for (int stage = 0; stage < 3; stage++) rules.Submit(s, rules.Bombs[s].Answers[stage], 35 + stage);
    rules.Submit(0, rules.Bombs[0].Answers[3], 40); rules.Tick(55.1); Check(!rules.Bombs[0].Armed, "Arm timeout");
    rules.Submit(0, rules.Bombs[0].Answers[3], 56); rules.Submit(1, rules.Bombs[1].Answers[3], 71); Check(rules.Phase == 2, "Win at 15s boundary");
    rules.Start(seed, 0); rules.Submit(0, "bad", 1); rules.Submit(1, "bad", 2); rules.Submit(0, "bad", 3); Check(rules.Phase == 3 && rules.EndAt == 240, "Three strikes");
    rules.Start(seed, 0); rules.Submit(0, "bad", 290); Check(rules.Phase == 3, "Penalty expires timer");
    rules.Start(seed, 0); rules.Tick(300); Check(rules.Phase == 3 && !rules.Talk(0, 301), "Timeout");
}
Console.WriteLine($"PASS: {checks} independent puzzle and round assertions across 2000 seeds.");
