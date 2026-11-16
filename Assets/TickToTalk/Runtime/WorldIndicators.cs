using UnityEngine;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        static readonly Color IndicatorOff = new Color(.055f, .075f, .085f);
        static readonly Color IndicatorRed = new Color(1f, .08f, .055f);
        static readonly Color IndicatorGreen = new Color(.1f, 1f, .28f);
        Material[] indicatorMaterials;
        Transform[] indicators;
        readonly System.Collections.Generic.List<Renderer> sectorSuccessLights = new System.Collections.Generic.List<Renderer>();
        readonly System.Collections.Generic.List<Material> sectorSuccessMaterials = new System.Collections.Generic.List<Material>();
        int[] lastStrikes;
        float[] warningUntil;
        AudioSource[] sirens;
        AudioClip sirenClip;
        GamePreferences soundPreferences;

        void InitWorldIndicators()
        {
            indicators = new[] { indicatorA, indicatorB };
            foreach (var light in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (light.name.StartsWith("Sector success light "))
                {
                    sectorSuccessLights.Add(light);
                    sectorSuccessMaterials.Add(light.material);
                }
            indicatorMaterials = new Material[24];
            lastStrikes = new int[2];
            warningUntil = new float[2];
            sirens = new AudioSource[2];
            soundPreferences = GetComponent<GamePreferences>();
            sirenClip = MakeSiren();
            for (int room = 0; room < 2; room++)
            {
                var sirenObject = new GameObject("Wrong answer siren " + room);
                sirenObject.transform.SetParent(transform);
                sirenObject.transform.position = new Vector3(room == 0 ? -4f : 4f, 2.3f, 0);
                var siren = sirenObject.AddComponent<AudioSource>();
                siren.playOnAwake = false;
                siren.spatialBlend = 1f;
                siren.minDistance = 1.5f;
                siren.maxDistance = 18f;
                siren.rolloffMode = AudioRolloffMode.Linear;
                sirens[room] = siren;
                if (indicators[room])
                {
                    for (int i = 0; i < 12; i++)
                    {
                        var renderer = indicators[room].GetChild(i).GetComponent<Renderer>();
                        indicatorMaterials[room * 12 + i] = renderer.material;
                        SetIndicator(indicatorMaterials[room * 12 + i], IndicatorOff);
                    }
                    indicators[room].gameObject.SetActive(false);
                }
            }
        }

        static AudioClip MakeSiren()
        {
            const int rate = 24000;
            var samples = new float[rate];
            double phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float pitch = 650f + 280f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);
                phase += 2.0 * System.Math.PI * pitch / rate;
                float envelope = Mathf.Min(1f, t * 30f) * Mathf.Min(1f, (1f - t) * 8f);
                samples[i] = .23f * envelope * Mathf.Sin((float)phase);
            }
            var clip = AudioClip.Create("Wrong answer warning", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        static void SetIndicator(Material material, Color color)
        {
            if (!material) return;
            material.color = color;
            material.SetColor("_EmissionColor", color == IndicatorOff ? Color.black : color * 1.8f);
        }

        void HideWorldIndicators()
        {
            if (indicators == null) return;
            if (alarmA) alarmA.intensity = 0;
            if (alarmB) alarmB.intensity = 0;
            foreach (var group in indicators) if (group) group.gameObject.SetActive(false);
            foreach (var siren in sirens) if (siren) siren.Stop();
            for (int room = 0; room < 2; room++) { lastStrikes[room] = 0; warningUntil[room] = 0; }
            foreach (var light in sectorSuccessLights) if (light) light.gameObject.SetActive(false);
        }

        float WarningUntil(int room) => warningUntil == null ? 0 : warningUntil[room];

        void UpdateWorldIndicators(PlayerView view)
        {
            if (indicators == null) return;
            bool active = view.phase == 1 || view.phase == 2 || view.phase == 3 || view.phase == 4;
            int[] strikes = { view.strikesA, view.strikesB };
            int[] mistakes = { view.mistakesA, view.mistakesB };
            int[] stages = { view.stageA, view.stageB };
            for (int i = 0; i < sectorSuccessLights.Count; i++)
            {
                var light = sectorSuccessLights[i];
                if (!light) continue;
                int sector = light.name["Sector success light ".Length] - '1';
                bool lit = active && (view.phase == 2 || view.phase == 4 || (light.name.EndsWith("A") ? view.stageA : view.stageB) > sector);
                light.gameObject.SetActive(active);
                SetIndicator(sectorSuccessMaterials[i], lit ? IndicatorGreen : IndicatorOff);
            }
            for (int room = 0; room < 2; room++)
            {
                if (view.phase == 1 || view.phase == 3)
                {
                    if (strikes[room] > lastStrikes[room])
                    {
                        warningUntil[room] = Time.unscaledTime + 1.4f;
                        if (sirens[room] && sirenClip)
                        {
                            sirens[room].volume = soundPreferences ? soundPreferences.UI : .7f;
                            sirens[room].PlayOneShot(sirenClip);
                        }
                    }
                    lastStrikes[room] = strikes[room];
                }
                else { lastStrikes[room] = strikes[room]; warningUntil[room] = 0; }
                var alarm = room == 0 ? alarmA : alarmB;
                if (alarm && Time.unscaledTime < warningUntil[room])
                {
                    alarm.color = IndicatorRed;
                    alarm.intensity = 9f;
                }
                var group = indicators[room];
                if (group)
                {
                    group.gameObject.SetActive(active);
                    if (active)
                    {
                        for (int sector = 0; sector < 4; sector++)
                            for (int i = 0; i < 3; i++)
                                SetIndicator(indicatorMaterials[room * 12 + sector * 3 + i],
                                    sector == stages[room] && i < mistakes[room] ? IndicatorRed : IndicatorOff);
                    }
                }
            }
        }

        void DisposeWorldIndicators()
        {
            if (indicatorMaterials != null) foreach (var material in indicatorMaterials) if (material) Destroy(material);
            foreach (var material in sectorSuccessMaterials) if (material) Destroy(material);
            sectorSuccessMaterials.Clear();
            sectorSuccessLights.Clear();
            if (sirenClip) Destroy(sirenClip);
            if (sirens != null) foreach (var siren in sirens) if (siren) Destroy(siren.gameObject);
        }
    }
}
