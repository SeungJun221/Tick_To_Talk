using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;

namespace TickToTalk.Editor
{
    public static class InteractionMusicChecks
    {
        public static void ImportMusic()
        {
            foreach(var path in new[]{"Assets/TickToTalk/Resources/Music/Lobby_LockedGlass.wav","Assets/TickToTalk/Resources/Music/Gameplay_CountdownCircuit.wav"}) {
                var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.CompressedInMemory;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.65f;
                importer.defaultSampleSettings=settings;importer.forceToMono=false;importer.SaveAndReimport();
            }
        }
        public static async void Run()
        {
            var s=NetworkSession.Instance;var prefs=s.GetComponent<GamePreferences>();float original=prefs.Music;
            int count=0;Action<bool,string> check=(ok,message)=>{count++;if(!ok)throw new Exception(message);};
            System.IO.File.WriteAllText("Logs/InteractionMusicChecks.txt","RUNNING");
            try {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var update=typeof(NetworkSession).GetMethod("Update",flags);
                var pos=(Vector2[])typeof(NetworkSession).GetField("positions",flags).GetValue(s);
                s.StartTestMode();s.Command("ready");typeof(NetworkSession).GetField("autoStartAt",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(s,Time.realtimeSinceStartupAsDouble-1);typeof(NetworkSession).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(s,null);pos[0]=new Vector2(5.6f,0);update.Invoke(s,null);
                check(s.InteractionRoom==1,"B room not selected");
                check(s.InteractionView(1).display==s.Rules.Bombs[1].Display[0],"B terminal shows A puzzle");
                s.SubmitInteraction(1,s.Rules.Bombs[1].Answers[0],0);
                check(s.Rules.Bombs[1].Stage==1 && s.Rules.Bombs[0].Stage==0,"B answer advanced wrong bomb");
                s.SubmitInteraction(0,s.Rules.Bombs[0].Answers[0],0);
                check(s.Rules.Bombs[0].Stage==0,"Remote bomb accepted out-of-range input");
                pos[0]=new Vector2(4,2.8f);update.Invoke(s,null);
                check(s.Near(NetworkSession.Manual(s.InteractionRoom)) && s.InteractionView(1).manual==s.Rules.Bombs[0].Manual[0],"B manual unavailable or wrong");
                pos[0]=new Vector2(-5.6f,0);update.Invoke(s,null);
                s.SubmitInteraction(0,s.Rules.Bombs[0].Answers[0],0);
                check(s.Rules.Bombs[0].Stage==1 && s.Rules.Bombs[1].Stage==1,"A bomb stopped working");
                foreach(int room in new[]{0,1}) {
                    pos[0]=new Vector2(room==0?-2.25f:2.25f,0);update.Invoke(s,null);
                                        s.Command("walkie","1");s.Command("talk");update.Invoke(s,null);check(s.Rules.Speaker==0,"Microphone in room "+room+" rejected/released");s.Command("release");
                }
                var lobby=s.transform.Find("Lobby music").GetComponent<AudioSource>();
                var game=s.transform.Find("Gameplay music").GetComponent<AudioSource>();
                check(lobby.clip && game.clip && lobby.clip!=game.clip && lobby.clip.length>30 && game.clip.length>30,"Separate music assets missing");
                prefs.Music=.4f;await Task.Delay(1900);
                check(game.isPlaying && game.volume>.39f && lobby.volume<.01f,"Gameplay music did not fade in");
                prefs.Music=0;prefs.ApplyAudio();check(game.volume==0 && lobby.volume==0,"Music mute failed");
                prefs.Music=.4f;prefs.ApplyAudio();s.Disconnect();await Task.Delay(1900);
                check(lobby.isPlaying && lobby.volume>.39f && game.volume<.01f,"Lobby music did not return");
                check(s.InteractionView(1)==s.View,"Test-only opposite puzzle exposed after leaving test");
                System.IO.File.WriteAllText("Logs/InteractionMusicChecks.txt","PASS "+count+" checks: A/B terminal and submission isolation, range rejection, B manual, both microphones, distinct music assets, gameplay/lobby crossfade, music mute, test-only data guard.");
            } catch(Exception e) {System.IO.File.WriteAllText("Logs/InteractionMusicChecks.txt","FAIL "+e);Debug.LogException(e);}
            finally {prefs.Music=original;prefs.ApplyAudio();if(s.IsTestMode)s.Disconnect();}
        }
    }
}
