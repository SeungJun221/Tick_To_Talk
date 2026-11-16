using UnityEngine;
namespace TickToTalk {
 public sealed partial class WalkiePose {
  Transform firstPerson,firstFill;
  void LabelRadio(string text,Vector3 position,float size,Color color){
   var go=new GameObject(text);go.transform.SetParent(handset,false);go.transform.localPosition=position;
   var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=48;label.characterSize=size*.22f;label.color=color;label.anchor=TextAnchor.MiddleLeft;
   var textMaterial=new Material(Shader.Find("TickToTalk/WorldText"));textMaterial.mainTexture=label.font.material.mainTexture;go.GetComponent<Renderer>().sharedMaterial=textMaterial;owned.Add(textMaterial);
  }
  void LateUpdate()
  {
   GripRadio();
   if(firstPerson)firstPerson.gameObject.SetActive(false);
  }
 }
}
