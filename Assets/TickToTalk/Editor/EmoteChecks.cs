using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace TickToTalk.Editor
{
    public static class EmoteChecks
    {
        [MenuItem("Tick To Talk/Verify Emotes")]
        public static void Run()
        {
            int count = 0;
            Action<bool, string> check = (ok, message) => { count++; if (!ok) throw new Exception(message); };
            var game = new GameObject("Emote verification");
            try
            {
                var session = game.AddComponent<NetworkSession>();
                session.Rules.Start(123, Time.realtimeSinceStartupAsDouble);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var handle = typeof(NetworkSession).GetMethod("HandleCommand", flags);
                var snapshot = typeof(NetworkSession).GetMethod("CreateView", flags);
                Action<int, string> send = (slot, value) => handle.Invoke(session, new object[] { slot, new ClientCommand { kind = "emote", answer = value } });
                Func<int, PlayerView> view = slot => (PlayerView)snapshot.Invoke(session, new object[] { slot });
                send(0, "1");
                check(view(1).emoteA == 1 && view(1).emoteRemainingA > 0, "Partner cannot see greeting");
                send(1, "2");
                var copied = JsonUtility.FromJson<PlayerView>(JsonUtility.ToJson(view(0)));
                check(copied.emoteA == 1 && copied.emoteB == 2, "Two emotes did not survive snapshot serialization");
                send(1, "3");
                check(view(0).emoteB == 3, "Third emote was not received");
                foreach (var invalid in new[] { "0", "4", "garbage" })
                {
                    send(1, invalid);
                    check(view(0).emoteB == 3, "Invalid emote was accepted: " + invalid);
                }
                session.Rules.Phase = 0;
                send(0, "2");
                check(view(1).emoteA == 0 && view(1).emoteB == 0, "Emote leaked into lobby");
                check(PrisonPresentation.EmoteWheelSelection(new Vector2(640, 210)) == 1, "Top segment does not select greeting");
                check(PrisonPresentation.EmoteWheelSelection(new Vector2(790, 440)) == 2, "Right segment does not select cheer");
                check(PrisonPresentation.EmoteWheelSelection(new Vector2(490, 440)) == 3, "Left segment does not select two-hand greeting");
                check(PrisonPresentation.EmoteWheelSelection(new Vector2(640, 360)) == 0, "Wheel centre should not select an emote");
                check(PrisonPresentation.EmoteWheelSelection(new Vector2(1050, 360)) == 0, "Outside wheel should not select an emote");
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/EmoteChecks.txt", "PASS " + count + " checks: network snapshot, three emotes, rejected input, lobby reset and radial wheel hit regions.");
            }
            finally { UnityEngine.Object.DestroyImmediate(game); }
        }
    }
}
