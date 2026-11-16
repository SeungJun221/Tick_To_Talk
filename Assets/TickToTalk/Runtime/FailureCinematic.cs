using System.Collections.Generic;
using UnityEngine;

namespace TickToTalk
{
    [DefaultExecutionOrder(1100)]
    public sealed class FailureCinematic : MonoBehaviour
    {
        public const float BlastDelay = .8f;
        public bool Active { get; private set; }
        public bool Finished => Active && elapsed >= RoundRules.FailureSequenceSeconds;
        public int ObservedRoom { get; private set; }
        public int BlastCount { get; private set; }
        PrisonPresentation presentation;
        PlayerView lastView;
        float elapsed, receivedAt, originalFov;
        bool blasted;
        Vector3 cameraPosition;
        readonly Vector3[] origins = new Vector3[2];
        readonly FailureRagdoll[] bodies = new FailureRagdoll[2];
        readonly List<GameObject> spawned = new List<GameObject>();
        AudioSource sound;
        AudioClip boom;
        GameObject explosionPrefab;

        void Awake()
        {
            presentation=GetComponent<PrisonPresentation>();
            explosionPrefab=Resources.Load<GameObject>("Effects/FailureExplosion");
            var audioObject=new GameObject("Failure explosion audio"); audioObject.transform.SetParent(transform,false);
            sound=audioObject.AddComponent<AudioSource>(); sound.playOnAwake=false; sound.spatialBlend=0;
            boom=MakeExplosionSound();
        }

        public bool UpdateSequence(PlayerView view)
        {
            if(view==null || view.phase!=3 || !view.exploded)
            {
                if(Active) ResetSequence();
                return false;
            }
            if(!Active) Begin(view);
            if(lastView!=view) { lastView=view; receivedAt=Time.unscaledTime; }
            elapsed=Mathf.Max(elapsed,view.failureElapsed+Time.unscaledTime-receivedAt);
            if(!blasted && elapsed>=BlastDelay) Explode();
            return true;
        }

        void Begin(PlayerView view)
        {
            Active=true; blasted=false; elapsed=0; BlastCount=0;
            lastView=view; receivedAt=Time.unscaledTime;
            originalFov=presentation.eye.fieldOfView;
            ObservedRoom=1-view.slot;
            var actors=new[]{presentation.playerA,presentation.playerB};
            for(int i=0;i<2;i++)
            {
                // Freeze the authoritative failure location, then let only the corpse simulate.
                actors[i].position=i==0?new Vector3(view.ax,view.ay,view.az):new Vector3(view.bx,view.by,view.bz);
                actors[i].rotation=Quaternion.Euler(0,i==0?view.ayaw:view.byaw,0);
                bodies[i]=new FailureRagdoll(actors[i]);
                int sector=Mathf.Clamp(i==0?view.stageA:view.stageB,0,3);
                origins[i]=new Vector3(i==0?-7.1f:7.1f,1.15f,sector*9);
            }
            var target=actors[ObservedRoom].position+Vector3.up;
            int observedSector=Mathf.Clamp(Mathf.RoundToInt(actors[ObservedRoom].position.z/9f),0,3);
            var cctv=GetComponent<PrisonSurveillance>();
            cameraPosition=cctv?cctv.FailureCameraPosition(ObservedRoom,observedSector,target):
                new Vector3(ObservedRoom==0?-6.6f:6.6f,3.32f,2.7f+observedSector*9);
            presentation.eye.fieldOfView=76;
        }

        void Explode()
        {
            blasted=true;
            for(int i=0;i<2;i++)
            {
                if(explosionPrefab)
                {
                    var fx=Instantiate(explosionPrefab,origins[i],Quaternion.identity);
                    fx.name="Failure explosion / Room "+(i==0?"A":"B"); spawned.Add(fx);
                    foreach(var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
                    { var main=ps.main; main.loop=false; main.useUnscaledTime=true; }
                }
                var flash=new GameObject("Explosion flash"); flash.transform.position=origins[i];
                var light=flash.AddComponent<Light>(); light.color=new Color(1,.44f,.1f); light.range=13; light.intensity=18;
                spawned.Add(flash);
                bodies[i].Explode(origins[i]); BlastCount++;
            }
            var prefs=GetComponent<GamePreferences>(); sound.volume=prefs?prefs.UI:.7f;
            sound.PlayOneShot(boom);
        }

        void LateUpdate()
        {
            if(!Active)return;
            var eye=presentation.eye;
            Vector3 target=bodies[ObservedRoom].Focus;
            float shake=blasted?Mathf.Exp(-Mathf.Max(0,elapsed-BlastDelay)*4)*.12f:0;
            eye.transform.position=cameraPosition+new Vector3(Mathf.Sin(elapsed*71),Mathf.Cos(elapsed*53),0)*shake;
            eye.transform.rotation=Quaternion.LookRotation(target-eye.transform.position);
            foreach(var go in spawned)
                if(go && go.TryGetComponent<Light>(out var light))light.intensity=18*Mathf.Exp(-Mathf.Max(0,elapsed-BlastDelay)*8);
            foreach(var body in bodies)body.Show();
        }

        public void DrawOverlay(Font font)
        {
            GUI.matrix=Matrix4x4.identity;
            GUI.color=new Color(.02f,.06f,.07f,.12f);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);
            GUI.color=new Color(0,0,0,.14f);
            for(int y=0;y<Screen.height;y+=5)GUI.DrawTexture(new Rect(0,y,Screen.width,1),Texture2D.whiteTexture);
            GUI.color=Color.white;
            var style=new GUIStyle(GUI.skin.label){font=font,fontSize=22,fontStyle=FontStyle.Bold};
            style.normal.textColor=new Color(.8f,1,.9f);
            GUI.Label(new Rect(32,25,700,40),"CCTV  /  ROOM "+(ObservedRoom==0?"A":"B")+"    ● REC",style);
            GUI.Label(new Rect(32,Screen.height-65,700,40),blasted?"폭발 감지 · 신호 복구 중":"경보 · 폭발 임박",style);
        }

        void ResetSequence()
        {
            Active=false;
            foreach(var body in bodies)body?.Restore();
            foreach(var go in spawned)if(go)Destroy(go); spawned.Clear();
            if(sound)sound.Stop();
            if(presentation && presentation.eye)presentation.eye.fieldOfView=originalFov;
            lastView=null;
        }
        static AudioClip MakeExplosionSound()
        {
            const int rate=24000; var samples=new float[rate*2]; var random=new System.Random(7201);
            float low=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=(float)i/rate;
                float noise=(float)(random.NextDouble()*2-1); low=Mathf.Lerp(low,noise,.12f);
                samples[i]=Mathf.Clamp((noise*.28f*Mathf.Exp(-t*12)+low*1.5f*Mathf.Exp(-t*2.5f)+
                    Mathf.Sin(2*Mathf.PI*(55*t-8*t*t))*.3f*Mathf.Exp(-t*3))*Mathf.Min(1,t*180),-.9f,.9f);
            }
            var clip=AudioClip.Create("Failure explosion",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void OnDestroy(){if(Active)ResetSequence();if(boom)Destroy(boom);if(sound)Destroy(sound.gameObject);}
    }
}
