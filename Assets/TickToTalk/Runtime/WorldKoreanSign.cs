using UnityEngine;
namespace TickToTalk
{
    [RequireComponent(typeof(TextMesh))]
    public sealed class WorldKoreanSign : MonoBehaviour
    {
        Font runtimeFont;
        Material runtimeMaterial;
        void Start()
        {
            var text=GetComponent<TextMesh>();
            runtimeFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Noto Sans CJK KR","Arial"},72);
            text.font=runtimeFont;text.fontSize=72;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            runtimeMaterial=new Material(Shader.Find("TickToTalk/WorldText"));
            GetComponent<Renderer>().sharedMaterial=runtimeMaterial;
            Font.textureRebuilt+=Rebuilt;runtimeFont.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);Rebuilt(runtimeFont);
        }
        void Rebuilt(Font font){if(font==runtimeFont && runtimeMaterial)runtimeMaterial.mainTexture=runtimeFont.material.mainTexture;}
        void OnDestroy(){Font.textureRebuilt-=Rebuilt;if(runtimeMaterial)Destroy(runtimeMaterial);if(runtimeFont)Destroy(runtimeFont);}
    }
}
