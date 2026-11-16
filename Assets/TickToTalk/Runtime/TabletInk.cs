using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TickToTalk
{
    // Local vector strokes plus a raster preview. No drawing data is sent to the network.
    public sealed class TabletInk : IDisposable
    {
        public const int Width=1200,Height=480,MaxPoints=100000;
        public static readonly Color32[] Palette={new Color32(24,25,29,255),new Color32(224,76,65,255),new Color32(233,137,69,255),new Color32(240,191,57,255),new Color32(110,190,67,255),new Color32(84,172,159,255),new Color32(92,145,221,255),new Color32(139,95,213,255),new Color32(218,69,127,255),new Color32(255,255,255,255)};
        [Serializable] public sealed class Stroke { public int color; public float width; public bool erase; public List<Vector2> points=new List<Vector2>(); }
        [Serializable] public sealed class Document { public int version=1; public List<Stroke> strokes=new List<Stroke>(); }
        Document document=new Document();
        readonly List<List<Stroke>> undo=new List<List<Stroke>>(),redo=new List<List<Stroke>>();
        readonly Color32[] pixels=new Color32[Width*Height];
        Texture2D texture;
        Stroke active;
        int pointCount;
        bool upload;
        public bool Drawing=>active!=null;
        public bool CanUndo=>undo.Count>0;
        public bool CanRedo=>redo.Count>0;
        public int StrokeCount=>document.strokes.Count;
        public int Revision { get; private set; }
        public bool Full=>pointCount>=MaxPoints || document.strokes.Count>=2000;
        public TabletInk(){texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){name="Private tablet handwriting",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};Rebuild();}
        public Texture2D Texture {get{if(upload){texture.SetPixels32(pixels);texture.Apply(false);upload=false;}return texture;}}
        public Color32 Pixel(int x,int y)=>pixels[(Height-1-y)*Width+x];
        void Remember(){undo.Add(new List<Stroke>(document.strokes));if(undo.Count>32)undo.RemoveAt(0);redo.Clear();}
        public bool Begin(Vector2 point,int color,float width,bool erase)
        {
            End();if(Full)return false;Remember();
            active=new Stroke{color=Mathf.Clamp(color,0,Palette.Length-1),width=Mathf.Clamp(width,1,64),erase=erase};document.strokes.Add(active);Add(point);return true;
        }
        public void Add(Vector2 point)
        {
            if(active==null || pointCount>=MaxPoints)return;
            point=new Vector2(Mathf.Clamp(point.x,0,Width-1),Mathf.Clamp(point.y,0,Height-1));
            if(active.points.Count>0){var previous=active.points[active.points.Count-1];if((point-previous).sqrMagnitude<.25f)return;Segment(previous,point,active);}
            else Dot(point,active);
            active.points.Add(point);pointCount++;upload=true;
        }
        public void End(){if(active==null)return;active=null;Revision++;}
        public void Undo(){End();if(!CanUndo)return;redo.Add(new List<Stroke>(document.strokes));document.strokes=new List<Stroke>(undo[undo.Count-1]);undo.RemoveAt(undo.Count-1);Rebuild();Revision++;}
        public void Redo(){End();if(!CanRedo)return;undo.Add(new List<Stroke>(document.strokes));document.strokes=new List<Stroke>(redo[redo.Count-1]);redo.RemoveAt(redo.Count-1);Rebuild();Revision++;}
        public void Clear(){End();if(document.strokes.Count==0)return;Remember();document.strokes.Clear();Rebuild();Revision++;}
        void Rebuild()
        {
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(255,255,255,255);pointCount=0;
            foreach(var stroke in document.strokes){pointCount+=stroke.points.Count;for(int i=0;i<stroke.points.Count;i++){if(i==0)Dot(stroke.points[i],stroke);else Segment(stroke.points[i-1],stroke.points[i],stroke);}}upload=true;
        }
        void Segment(Vector2 a,Vector2 b,Stroke stroke)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/Mathf.Max(.5f,stroke.width*.2f)));
            for(int i=1;i<=steps;i++)Dot(Vector2.Lerp(a,b,(float)i/steps),stroke);
        }
        void Dot(Vector2 point,Stroke stroke)
        {
            float radius=stroke.width*.5f;Color color=stroke.erase?Color.white:(Color)Palette[stroke.color];
            int x0=Mathf.Max(0,Mathf.FloorToInt(point.x-radius-1)),x1=Mathf.Min(Width-1,Mathf.CeilToInt(point.x+radius+1));
            int y0=Mathf.Max(0,Mathf.FloorToInt(point.y-radius-1)),y1=Mathf.Min(Height-1,Mathf.CeilToInt(point.y+radius+1));
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
                float alpha=Mathf.Clamp01(radius+.5f-Vector2.Distance(new Vector2(x,y),point));if(alpha<=0)continue;
                int index=(Height-1-y)*Width+x;pixels[index]=(Color32)Color.Lerp((Color)pixels[index],color,alpha);
            }
        }
        public string ToJson(){End();return JsonUtility.ToJson(document);}
        public bool LoadJson(string json)
        {
            try{
                if(string.IsNullOrEmpty(json)||json.Length>8000000)return false;
                var parsed=JsonUtility.FromJson<Document>(json);if(parsed==null||parsed.version!=1||parsed.strokes==null||parsed.strokes.Count>2000)return false;
                int total=0;foreach(var stroke in parsed.strokes){if(stroke==null||stroke.points==null||stroke.color<0||stroke.color>=Palette.Length||!float.IsFinite(stroke.width)||stroke.width<1||stroke.width>64)return false;
                    total+=stroke.points.Count;if(total>MaxPoints)return false;
                    foreach(var point in stroke.points)if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||point.x<0||point.x>=Width||point.y<0||point.y>=Height)return false;}
                active=null;document=parsed;undo.Clear();redo.Clear();Rebuild();Revision++;return true;
            }catch(Exception){return false;}
        }
        public bool Load(string path)
        {
            foreach(var candidate in new[]{path,path+".bak"})try{if(File.Exists(candidate)&&new FileInfo(candidate).Length<=8000000&&LoadJson(File.ReadAllText(candidate)))return true;}catch(IOException){}catch(UnauthorizedAccessException){}
            return false;
        }
        public bool Save(string path)
        {
            try{Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";File.WriteAllText(temp,ToJson());if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);return true;}
            catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}
        }
        public void Dispose(){if(texture){if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);texture=null;}}
    }
}
