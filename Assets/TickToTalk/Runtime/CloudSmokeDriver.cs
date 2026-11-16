#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;

namespace TickToTalk
{
    // Opt-in automated test. This class is excluded from the release player.
    public sealed class CloudSmokeDriver : MonoBehaviour
    {
        float start, next;
        bool sawA, sawRelease, sawB, reported, jumped, sawJump;
        float peakHeight;
        uint sequence;
        string requestedLook;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Environment.GetCommandLineArgs().Contains("-network-smoke"))
                new GameObject("Cloud smoke test").AddComponent<CloudSmokeDriver>();
        }
        void Start()
        {
            start = Time.realtimeSinceStartup;
            var s = NetworkSession.Instance;
            if (s) { s.GetComponent<PrisonPresentation>().enabled = false; s.GetComponent<TableVoice>().enabled = false; }
        }
        void LateUpdate()
        {
            if (reported) return;
            var s = NetworkSession.Instance;
            if (!s) return;
            var v = s.View;
            if (Time.realtimeSinceStartup - start > 150)
            { Debug.LogError("TTT_CLOUD_SMOKE_FAIL timeout " + s.Status); reported = true; return; }
            if (v == null || Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + .05f;
            if (v.phase == 0) {
                var catalog=Resources.Load<WardrobeCatalog>("WardrobeCatalog");
                if(requestedLook==null) {var choices=catalog.Defaults(); int face=Array.FindIndex(catalog.categories,c=>c.slot=="Faces"); choices[face]=v.slot==0?1:2; requestedLook=catalog.Encode(choices); s.Command("look",requestedLook); return;}
                if((v.slot==0?v.lookA:v.lookB)!=requestedLook) {s.Command("look",requestedLook);return;}
                s.Command("ready"); return;
            }
            if (v.phase != 1) { Debug.LogError("TTT_CLOUD_SMOKE_FAIL unexpected round end"); reported = true; return; }
            if (!jumped) { s.Command("jump"); jumped = true; }
            peakHeight = Mathf.Max(peakHeight, v.slot == 0 ? v.ay : v.by);
            if (peakHeight > .5f && (v.slot == 0 ? v.ay : v.by) == 0) sawJump = true;
            if (!string.IsNullOrEmpty(v.solutions)) { Debug.LogError("TTT_CLOUD_SMOKE_FAIL answer leak"); reported = true; return; }
            if(string.IsNullOrEmpty(v.lookA)||string.IsNullOrEmpty(v.lookB)||v.lookA==v.lookB) {Debug.LogError("TTT_CLOUD_SMOKE_FAIL appearance synchronization");reported=true;return;}
            float x = v.slot == 0 ? v.ax : v.bx;
            float target = v.slot == 0 ? -2.25f : 2.25f;
            s.Command("move", "", new Vector2(Mathf.Abs(x - target) > .1f ? Mathf.Sign(target - x) : 0, 0));
            if (Mathf.Abs(x - target) < .2f)
            {
                s.Command("submit", "000000"); // Must be rejected at the microphone, away from the bomb.
                s.Command("walkie", "1");
                if (v.slot == 0 && !sawA || v.slot == 1 && sawRelease) s.Command("talk");
            }
            if (v.strikes != 0) { Debug.LogError("TTT_CLOUD_SMOKE_FAIL distance validation"); reported = true; return; }
            if (v.speaker == 0) sawA = true;
            if (v.slot == 0 && sawA && !sawRelease && Time.realtimeSinceStartup - start > 7) s.Command("release");
            if (sawA && v.speaker == -1) sawRelease = true;
            if (sawRelease && v.speaker == 1) sawB = true;
            if (v.speaker == v.slot)
            {
                var pcm = new short[320];
                for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(Math.Sin(i * .1) * 3000);
                s.SendVoice(pcm, sequence++);
            }
            if (sawA && sawRelease && sawB && sequence > 30 && sawJump && s.GetComponent<TableVoice>().ReceivedPackets > 10)
            { Debug.Log("TTT_CLOUD_SMOKE_PASS slot=" + v.slot + " audio=" + s.GetComponent<TableVoice>().ReceivedPackets + " jump=" + peakHeight + " appearance=PASS"); reported = true; }
        }
    }
}
#endif
