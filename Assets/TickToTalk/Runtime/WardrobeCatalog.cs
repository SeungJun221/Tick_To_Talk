using System;
using System.Linq;
using UnityEngine;

namespace TickToTalk
{
    [Serializable] public class WearPiece { public string slot; public Mesh mesh; }
    [Serializable] public class WearOption { public string name, group; public WearPiece[] pieces; }
    [Serializable] public class WearCategory { public string slot, label; public bool required; public WearOption[] options; }
    public sealed class WardrobeCatalog : ScriptableObject
    {
        public WearCategory[] categories;
        public GameObject basePrefab;
        public Material material;
        public int[] Defaults() {
            return categories.Select(c=>c.slot=="Body" || c.slot=="Faces" ? 0 : c.slot=="Outerwear" ? Math.Min(1,c.options.Length-1) : c.slot=="Pants" || c.slot=="Shoes" || c.slot=="Hairstyle" ? 0 : -1).ToArray();
        }
        public string Encode(int[] values) => string.Join(",", values);
        public int[] Decode(string data) {
            var values=Defaults(); var parts=(data??"").Split(',');
            if(parts.Length!=values.Length) return values;
            for(int i=0;i<values.Length;i++) if(!int.TryParse(parts[i],out values[i]) || values[i]< (categories[i].required?0:-1) || values[i]>=categories[i].options.Length) return Defaults();
            return values;
        }
        public bool IsValid(string data) => data!=null && data.Length<180 && Encode(Decode(data))==data;
        public void Apply(Transform actor, int[] choices, bool visible=true) {
            var renderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach(var r in renderers) { r.enabled=false; r.sharedMesh=null; }
            int full=Array.FindIndex(categories,c=>c.slot=="FullBody");
            int outer=Array.FindIndex(categories,c=>c.slot=="Outerwear");
            string group=outer>=0 && choices[outer]>=0 ? categories[outer].options[choices[outer]].group : "";
            int hat=Array.FindIndex(categories,c=>c.slot=="Hat");
            bool singleHat=hat>=0 && choices[hat]>=0 && categories[hat].options[choices[hat]].group=="HatSingle";
            for(int i=0;i<categories.Length;i++) {
                var c=categories[i]; if(choices[i]<0) continue;
                if(full>=0 && choices[full]>=0 && c.slot!="FullBody" && c.slot!="Body" && c.slot!="Faces") continue;
                if((group=="Outfit" && c.slot=="Pants") || (group=="Mascots" && (c.slot=="Hat"||c.slot=="Hairstyle")) || (singleHat && c.slot=="Hairstyle")) continue;
                foreach(var piece in c.options[choices[i]].pieces) {
                    var renderer=renderers.FirstOrDefault(r=>r.name.StartsWith(piece.slot,StringComparison.Ordinal));
                    if(!renderer) continue;
                    renderer.sharedMesh=piece.mesh; renderer.sharedMaterial=material; renderer.updateWhenOffscreen=true;
                    renderer.localBounds=new Bounds(Vector3.up, new Vector3(4,5,4));
                }
            }
            foreach(var r in renderers) r.enabled=visible;
        }
    }
}
