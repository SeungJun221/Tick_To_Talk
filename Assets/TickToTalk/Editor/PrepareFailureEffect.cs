using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TickToTalk.Editor
{
    public static class PrepareFailureEffect
    {
        [MenuItem("Tick To Talk/Prepare WAR FX Failure Explosion")]
        public static void Run()
        {
            const string source="Assets/JMO Assets/WarFX/_Effects/Explosions/WFX_Explosion.prefab";
            const string output="Assets/TickToTalk/Resources/Effects/FailureExplosion.prefab";
            const string materials="Assets/TickToTalk/Materials/FailureEffects";
            Directory.CreateDirectory(Path.GetDirectoryName(output)); Directory.CreateDirectory(materials); AssetDatabase.Refresh();
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if(!original)throw new InvalidOperationException("Import WAR FX from Package Manager first.");
            var shader=Shader.Find("TickToTalk/Failure Particles URP");
            if(!shader)throw new InvalidOperationException("Failure particle shader missing.");
            var effect=UnityEngine.Object.Instantiate(original);
            try
            {
                effect.name="FailureExplosion"; effect.transform.localScale=Vector3.one*.8f;
                // The cinematic owns cleanup and the light flash; keep the vendor prefab untouched.
                foreach(var behaviour in effect.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(behaviour);
                foreach(var light in effect.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
                var converted=new Dictionary<Material,Material>();
                var report=new List<string>();
                foreach(var renderer in effect.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    var slots=renderer.sharedMaterials;
                    for(int i=0;i<slots.Length;i++)
                    {
                        var old=slots[i];if(!old)continue;
                        if(!converted.TryGetValue(old,out var mat))
                        {
                            string path=materials+"/"+old.name+" URP.mat";
                            mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
                            string kind=old.shader.name;
                            bool multiply=kind.Contains("Multiply"), additive=kind.Contains("Additive"), alphaOnly=kind.Contains("Alpha8");
                            mat.SetTexture("_MainTex",old.HasProperty("_MainTex")?old.GetTexture("_MainTex"):old.mainTexture);
                            mat.SetColor("_TintColor",old.HasProperty("_TintColor")?old.GetColor("_TintColor"):new Color(.5f,.5f,.5f,.5f));
                            mat.SetFloat("_AlphaOnly",alphaOnly?1:0);mat.SetFloat("_Multiply",multiply?1:0);
                            mat.SetFloat("_SrcBlend",(float)(multiply?(alphaOnly?BlendMode.Zero:BlendMode.DstColor):additive&&alphaOnly?BlendMode.OneMinusDstColor:BlendMode.SrcAlpha));
                            mat.SetFloat("_DstBlend",(float)(multiply?BlendMode.SrcColor:additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));
                            EditorUtility.SetDirty(mat); converted.Add(old,mat);
                            report.Add(old.name+" | "+kind+" | "+AssetDatabase.GetAssetPath(mat.GetTexture("_MainTex")));
                        }
                        slots[i]=mat;
                    }
                    renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
                foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main=ps.main; main.loop=false;main.playOnAwake=true;main.useUnscaledTime=true;
                    main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                }
                PrefabUtility.SaveAsPrefabAsset(effect,output);AssetDatabase.SaveAssets();
                File.WriteAllLines("Logs/FailureEffectSetup.txt",report);
                Debug.Log("TTT_FAILURE_EFFECT_READY "+output);
            }
            finally { UnityEngine.Object.DestroyImmediate(effect); }
        }
    }
}
