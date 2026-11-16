using System.Collections.Generic;
using UnityEngine;

namespace TickToTalk
{
    public sealed class TableVoice : MonoBehaviour
    {
        public bool MonitorRequested;
        volatile float receiveGain = 1;
        public string DeviceStatus = "마이크 대기";
        public int ReceivedPackets { get; private set; }
        public int SentPackets { get; private set; }
        public float InputLevel { get; private set; }
        public long PlayedSamples { get; private set; }
        public string SelectedDevice => string.IsNullOrEmpty(selected) ? "시스템 기본 장치" : selected;
        public string Diagnostics => DeviceStatus + " · 입력 " + (InputLevel * 100).ToString("0") + "% · 송신 " + SentPackets + " / 수신 " + ReceivedPackets;
        string selected, activeDevice;
        AudioClip capture, carrier;
        AudioSource speaker;
        int readPosition, outputRate = 48000;
        uint sequence, nextSequence;
        float lastArrival;
        int frameOffset=320, missingFrames;
        readonly float[] lastFrame=new float[320];
        bool haveSequence, buffered;
        float held;
        double phase;
        readonly Dictionary<uint,float[]> buffer = new Dictionary<uint,float[]>();
        readonly object gate = new object();
        readonly float[] chunk = new float[320];
        readonly short[] pcm = new short[320];
        void Start()
        {
            selected = PlayerPrefs.GetString("TTT_Microphone", "");
            outputRate = AudioSettings.outputSampleRate;
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 0; speaker.volume = 1; speaker.mute = false;
            speaker.priority = 0; speaker.bypassEffects = true;
            // A non-streaming carrier keeps the audio filter active without a one-second clip prefetch.
            carrier = AudioClip.Create("Intercom output", 1024, 1, outputRate, false);
            speaker.clip = carrier; speaker.loop = true; speaker.Play();
        }
        public void NextDevice()
        {
            var devices = Microphone.devices;
            int i = System.Array.IndexOf(devices, selected);
            selected = i + 1 < devices.Length ? devices[i + 1] : "";
            StopCapture(); PlayerPrefs.SetString("TTT_Microphone", selected); PlayerPrefs.Save();
        }
        void Update()
        {
            var session = GetComponent<NetworkSession>();
            if (!session) return;
            var preferences=GetComponent<GamePreferences>();
            receiveGain=preferences ? preferences.Voice : 1;
            float inputGain=2.5f * (preferences ? preferences.Microphone : 1);
            var v = session.View;
            var presentation=GetComponent<PrisonPresentation>();
            bool holding = v != null && (v.slot == 0 ? v.walkieA : v.walkieB);
            if(presentation)holding=presentation.WalkieHeld;
            bool transmit = (!presentation || !presentation.SettingsOpen) && v != null && v.phase == 1 && holding && v.speaker == v.slot;
            // Capture in the lobby as well so players can check their device before the timer starts.
            bool monitor = MonitorRequested || (v != null && v.phase == 1);
            if (!monitor) { StopCapture(); return; }
            if (!capture)
            {
                if (Microphone.devices.Length == 0) { DeviceStatus = "입력 장치 없음: Windows 마이크 권한 확인"; return; }
                activeDevice = string.IsNullOrEmpty(selected) ? null : selected;
                if (activeDevice != null && System.Array.IndexOf(Microphone.devices, activeDevice) < 0) activeDevice = null;
                capture = Microphone.Start(activeDevice, true, 1, 16000); readPosition = 0;
                if (!capture) { DeviceStatus = "마이크 시작 실패: 입력 장치/권한 확인"; return; }
            }
            DeviceStatus = transmit ? "송신 중" : "입력 확인 (전송 안 함)";
            int writePosition = Microphone.GetPosition(activeDevice);
            if (writePosition < 0) { DeviceStatus = "마이크 입력 대기"; return; }
            int available = (writePosition - readPosition + capture.samples) % capture.samples;
            if(available > 3200) { readPosition=(writePosition-1920+capture.samples)%capture.samples; available=1920; }
            int blocks = 0;
            while (available >= 320 && blocks++ < 10)
            {
                capture.GetData(chunk, readPosition);
                float peak = 0;
                for (int i = 0; i < pcm.Length; i++) { peak = Mathf.Max(peak, Mathf.Abs(chunk[i]*inputGain)); pcm[i] = (short)(Mathf.Clamp(chunk[i]*inputGain, -1, 1) * 32767); }
                InputLevel = Mathf.Max(peak, InputLevel * .85f);
                if (transmit) { session.SendVoice(pcm, sequence++); SentPackets++; }
                readPosition = (readPosition + 320) % capture.samples; available -= 320;
            }
        }
        void StopCapture()
        {
            if (capture) { Microphone.End(activeDevice); Destroy(capture); capture = null; }
            InputLevel = 0; DeviceStatus = "마이크 대기";
        }
        public void Receive(float[] samples, uint seq)
        {
            lock (gate)
            {
                if(samples == null || samples.Length != 320)return;
                float now=Time.realtimeSinceStartup;
                if(!haveSequence || now-lastArrival>.6f) {
                    buffer.Clear(); nextSequence=seq; haveSequence=true; buffered=false;
                    frameOffset=320; missingFrames=0; held=0; phase=0;
                }
                lastArrival=now;
                if(unchecked((int)(seq-nextSequence))<0 || buffer.ContainsKey(seq))return;
                // Trim latency after a long main-thread stall without discarding all new audio.
                if(unchecked((int)(seq-nextSequence))>15) {
                    buffer.Clear();nextSequence=seq;buffered=false;frameOffset=320;missingFrames=0;
                }
                buffer.Add(seq,samples); ReceivedPackets++;
            }
        }
        public void ResetPlayback()
        {
            StopCapture();
            lock (gate) { buffer.Clear(); buffered = false; held = 0; phase = 0; haveSequence = false; frameOffset=320; missingFrames=0; ReceivedPackets = 0; SentPackets = 0; PlayedSamples = 0; }
        }
        // Invoked on the output audio thread: convert 16 kHz mono into the device's rate/channels.
        void OnAudioFilterRead(float[] data, int channels) { RenderAudio(data, channels); }
        public void RenderAudio(float[] data, int channels)
        {
            lock (gate)
            {
                if (!buffered && buffer.Count >= 6) { buffered = true; phase = 1; }
                double step = 16000.0 / System.Math.Max(1, outputRate);
                for (int i = 0; i < data.Length; i += channels) {
                    if(buffered) {
                        while(phase>=1) {
                            phase-=1;
                            if(frameOffset>=320) {
                                float[] packet;
                                if(buffer.TryGetValue(nextSequence,out packet)) {
                                    buffer.Remove(nextSequence); System.Array.Copy(packet,lastFrame,320);missingFrames=0;
                                } else {
                                    missingFrames++;
                                    for(int j=0;j<320;j++)lastFrame[j]*=.65f;
                                }
                                nextSequence++;frameOffset=0;
                                if(missingFrames>6 && buffer.Count==0){buffered=false;haveSequence=false;held=0;break;}
                            }
                            held=lastFrame[frameOffset++];PlayedSamples++;
                        }
                        phase+=step;
                    } else held=0;
                    for(int ch=0;ch<channels;ch++)data[i+ch]=held*receiveGain;
                }
            }
        }
        void OnAudioConfigurationChanged(bool changed) { outputRate = AudioSettings.outputSampleRate; }
        void OnDisable() { StopCapture(); lock (gate) { buffer.Clear(); buffered = false; } }
        void OnDestroy() { if (carrier) Destroy(carrier); }
    }
}
