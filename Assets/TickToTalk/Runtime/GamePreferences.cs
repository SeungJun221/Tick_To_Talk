using UnityEngine;

namespace TickToTalk
{
    public sealed class GamePreferences : MonoBehaviour
    {
        public float Master = 1, Music = .35f, UI = .7f, Voice = 1, Microphone = 1;
        public bool Fullscreen;
        AudioSource music, gameplayMusic, ui, effects;
        AudioClip door, walkie;
        bool departureWasOpen;
        int previousPhase = -1, openSectors;
        float gameplayBlend;
        bool wasGameplay;
        AudioClip ambience, gameplay, click;
        void Awake()
        {
            Master = Load("Master", 1); Music = Load("Music", .35f); UI = Load("UI", .7f);
            Voice = Load("Voice", 1); Microphone = Load("Mic", 1);
            Fullscreen = PlayerPrefs.GetInt("TTT_Fullscreen", Screen.fullScreen ? 1 : 0) == 1;
            music = MakeSource("Lobby music"); gameplayMusic=MakeSource("Gameplay music"); ui = MakeSource("UI sounds");
            effects = MakeSource("Gameplay effects");
            door = Resources.Load<AudioClip>("Music/effect/door");
            walkie = Resources.Load<AudioClip>("Music/effect/Walkie Talkie Sound Effects - All Sound Effects_[cut_0sec]");
            ambience=Resources.Load<AudioClip>("Music/Lobby_LockedGlass");
            gameplay=Resources.Load<AudioClip>("Music/Gameplay_CountdownCircuit");
            music.clip=ambience; music.loop=true;
            gameplayMusic.clip=gameplay; gameplayMusic.loop=true;
            if(ambience)music.Play();
            var tick = new float[2400];
            for (int i=0; i<tick.Length; i++) tick[i] = .13f*Mathf.Sin(2*Mathf.PI*660*i/24000)*Mathf.Exp(-i/330f);
            click = AudioClip.Create("Menu click", tick.Length, 1, 24000, false); click.SetData(tick, 0);

            ApplyAudio(); ApplyDisplay();
        }
        AudioSource MakeSource(string name) { var go = new GameObject(name); go.transform.SetParent(transform); var a = go.AddComponent<AudioSource>(); a.spatialBlend=0; a.playOnAwake=false; return a; }
        static float Load(string key,float fallback) => Mathf.Clamp01(PlayerPrefs.GetFloat("TTT_"+key, fallback));
        public void ApplyAudio() {
            AudioListener.volume=Master;
            var session=GetComponent<NetworkSession>();
            float duck=session && session.View!=null && session.View.speaker>=0 ? .45f : 1;
            if(music) music.volume=Music*(1-gameplayBlend)*duck;
            if(gameplayMusic) gameplayMusic.volume=Music*gameplayBlend*duck;
            if(ui) ui.volume=UI;
            if(effects) effects.volume=UI;
        }
        public void ApplyDisplay() {
            if (Application.isEditor) return;
            if (Fullscreen) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            else Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
        }
        public void Click() { if(ui && click) ui.PlayOneShot(click); }
        public void PlayWalkie() { if(effects && walkie) effects.PlayOneShot(walkie, .5f); }
        void UpdateDoorSounds(PlayerView view)
        {
            if(view == null)
            {
                departureWasOpen = false; openSectors = 0; previousPhase = -1;
                if(effects) effects.Stop();
                return;
            }
            bool newRound = view.phase == 1 && previousPhase != 1;
            bool departure = view.phase == 1 && view.departureOpen;
            int completed = view.phase == 4 || view.phase == 2 ? 4 : Mathf.Min(view.stageA, view.stageB);
            if(newRound) { departureWasOpen = false; openSectors = 0; }
            // Each pair of gates opens together; play one cue on each player's machine.
            if(departure && !departureWasOpen && effects && door) effects.PlayOneShot(door);
            for(int stage = openSectors; stage < completed; stage++)
                if(effects && door) effects.PlayOneShot(door);
            departureWasOpen = departure;
            openSectors = completed;
            previousPhase = view.phase;
        }
        public void Save() {
            PlayerPrefs.SetFloat("TTT_Master",Master); PlayerPrefs.SetFloat("TTT_Music",Music); PlayerPrefs.SetFloat("TTT_UI",UI);
            PlayerPrefs.SetFloat("TTT_Voice",Voice); PlayerPrefs.SetFloat("TTT_Mic",Microphone);
            PlayerPrefs.SetInt("TTT_Fullscreen",Fullscreen?1:0); PlayerPrefs.Save(); ApplyAudio();
        }
        public void Defaults() { Master=Voice=Microphone=1; Music=.35f; UI=.7f; Fullscreen=true; ApplyAudio(); ApplyDisplay(); Save(); }
        void Update() {
            var session=GetComponent<NetworkSession>();
            bool active=session && session.View!=null && (session.View.phase==1 || session.View.phase==4);
            if(active!=wasGameplay) {
                var entering=active?gameplayMusic:music;
                if(entering && entering.clip) {entering.time=0;entering.Play();}
                wasGameplay=active;
            }
            gameplayBlend=Mathf.MoveTowards(gameplayBlend,active?1:0,Time.unscaledDeltaTime/1.5f);
            ApplyAudio();
            UpdateDoorSounds(session ? session.View : null);
            if(gameplayBlend==1 && music && music.isPlaying)music.Stop();
            if(gameplayBlend==0 && gameplayMusic && gameplayMusic.isPlaying)gameplayMusic.Stop();
        }
        void OnDestroy() { if(click) Destroy(click); }

    }
}
