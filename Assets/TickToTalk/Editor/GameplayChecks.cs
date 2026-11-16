using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TickToTalk.Editor
{
    public static class GameplayChecks
    {
        static IEnumerator routine;
        static int checks;
        static void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
        public static void Start() {
            checks = 0; routine = Run(); EditorApplication.update += Step;
            System.IO.File.WriteAllText("Logs/GameplayChecks.txt", "RUNNING");
        }
        static void Step() {
            try { if (routine.MoveNext()) return; }
            catch(Exception e) { System.IO.File.WriteAllText("Logs/GameplayChecks.txt", "FAIL " + e); Debug.LogException(e); }
            EditorApplication.update -= Step;
        }
        static IEnumerator Run() {
            var s = NetworkSession.Instance;
            s.GetComponent<PrisonPresentation>().enabled = false;
            s.CreateOnlineRoom();
            double deadline = EditorApplication.timeSinceStartup + 45;
            while (s.View == null && EditorApplication.timeSinceStartup < deadline) yield return null;
            Check(s.View != null && s.IsRoomHost, "Actual Relay host start");
            s.Rules.Start(1827, Time.realtimeSinceStartupAsDouble);
            var positions = (Vector2[])typeof(NetworkSession).GetField("positions", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
            double until = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < until) yield return null;
            positions[0] = new Vector2(-4,-2);
            s.Command("move", "", new Vector2(0,1), 0, false);
            double began = Time.realtimeSinceStartupAsDouble;
            until = EditorApplication.timeSinceStartup + .18; while (EditorApplication.timeSinceStartup < until) yield return null;
            double elapsed = Time.realtimeSinceStartupAsDouble - began;
            float walk = (positions[0].y + 2) / (float)elapsed;
            s.Command("move", "", Vector2.zero);
            positions[0] = new Vector2(-4,-2);
            s.Command("move", "", new Vector2(0,1), 0, true);
            began = Time.realtimeSinceStartupAsDouble;
            until = EditorApplication.timeSinceStartup + .18; while (EditorApplication.timeSinceStartup < until) yield return null;
            elapsed = Time.realtimeSinceStartupAsDouble - began;
            float run = (positions[0].y + 2) / (float)elapsed;
            s.Command("move", "", Vector2.zero);
            Check(walk > 2 && walk < 4.3f && run > walk * 1.35f && run < 7, "Sprint speed " + walk + "/" + run);
            positions[0] = new Vector2(-5.5f,0);
            until = EditorApplication.timeSinceStartup + .2; while (EditorApplication.timeSinceStartup < until) yield return null;
            s.Command("submit", s.Rules.Bombs[0].Answers[0]);
            until = EditorApplication.timeSinceStartup + .15; while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(s.Rules.Bombs[0].Stage == 1 && s.Rules.Strikes == 0, "Correct answer accepted through network");
            Check(s.View.history.Contains(s.Rules.Bombs[0].Answers[0]) && string.IsNullOrEmpty(s.View.solutions), "History only, unreached answers hidden");
            var b = s.Rules.Bombs[0];
            s.Rules.Tick(s.Rules.EndAt + 1);
            until = EditorApplication.timeSinceStartup + .15; while (EditorApplication.timeSinceStartup < until) yield return null;
            Check(s.View.phase == 3 && s.View.solutions.Contains(b.Answers[3]) && s.View.solutions.Contains(s.Rules.Bombs[1].Answers[3]), "Both answer sets disclosed after explosion");
            s.Disconnect();
            System.IO.File.WriteAllText("Logs/GameplayChecks.txt", "PASS " + checks + " checks: Relay host, walk/sprint " + walk + "/" + run + ", real command correct-answer acceptance, active-round answer secrecy, failed-round answers.");
        }
    }
}
