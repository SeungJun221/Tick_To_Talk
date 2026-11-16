#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using UnityEngine;
namespace TickToTalk
{
    public sealed class TabletNetworkSmoke : MonoBehaviour
    {
        float started,round=-1;bool opened,closed,sawBoth,sawRemotePose,sawRemotePutAway,done;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install(){if(Environment.GetCommandLineArgs().Contains("-tablet-smoke"))new GameObject("Tablet network verification").AddComponent<TabletNetworkSmoke>();}
        void Finish(){Application.Quit();}
        void Start(){started=Time.realtimeSinceStartup;}
        void Update()
        {
            if(done)return;var s=NetworkSession.Instance;if(!s)return;
            if(Time.realtimeSinceStartup-started>150){Debug.LogError("TTT_TABLET_SMOKE_FAIL timeout "+s.Status);done=true;Application.Quit();return;}
            var v=s.View;if(v==null)return;
            if(v.phase==0){s.Command("ready");return;}
            if(v.phase!=1)return;
            if(round<0)round=Time.realtimeSinceStartup;
            var p=s.GetComponent<PrisonPresentation>();
            if(!opened){p.OpenTablet();opened=true;}
            sawBoth|=v.tabletA&&v.tabletB;
            var remote=(v.slot==0?p.playerB:p.playerA).GetComponentInChildren<TabletPose>();
            sawRemotePose|=remote&&remote.HoldWeight>.95f&&remote.Prop.gameObject.activeSelf;
            if(Time.realtimeSinceStartup-round>6&&!closed){p.CloseTablet();closed=true;}
            if(closed && !v.tabletA&&!v.tabletB && remote && remote.HoldWeight>0f && remote.HoldWeight<=.5f)
                sawRemotePutAway|=!remote.Prop.gameObject.activeSelf;
            if(closed && Time.realtimeSinceStartup-round>9 && !v.tabletA&&!v.tabletB)
            {
                done=true;
                if(sawBoth&&sawRemotePose&&sawRemotePutAway)Debug.Log("TTT_TABLET_SMOKE_PASS slot="+v.slot+" both hold flags received; remote tablet put away during lowering; both close flags received");
                else Debug.LogError("TTT_TABLET_SMOKE_FAIL flags="+sawBoth+" remotePose="+sawRemotePose+" remotePutAway="+sawRemotePutAway);
                Invoke(nameof(Finish),10);
            }
        }
    }
}
#endif
