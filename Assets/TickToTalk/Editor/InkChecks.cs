using System;
using System.IO;
using UnityEngine;
namespace TickToTalk.Editor
{
    public static class InkChecks
    {
        [UnityEditor.MenuItem("Tick To Talk/Verify Tablet Ink")]
        public static void Run()
        {
            int count=0;Action<bool,string> check=(ok,message)=>{count++;if(!ok)throw new Exception(message);};
            var ink=new TabletInk();var restored=new TabletInk();
            string path=Path.Combine(Application.temporaryCachePath,"TTT_InkCheck_"+Guid.NewGuid()+".json");
            try
            {
                ink.Begin(new Vector2(20,30),0,6,false);ink.Add(new Vector2(1100,30));ink.End();
                for(int x=30;x<1100;x+=37)check(ink.Pixel(x,30).r<50,"Fast stroke has a gap");
                ink.Begin(new Vector2(200,80),1,10,false);ink.Add(new Vector2(600,80));ink.End();
                check(ink.Pixel(400,80).r==TabletInk.Palette[1].r,"Pen color incorrect");
                ink.Begin(new Vector2(500,30),0,40,true);ink.End();check(ink.Pixel(500,30).r==255,"Eraser did not restore white paper");
                ink.Undo();check(ink.Pixel(500,30).r<50,"Undo eraser failed");ink.Redo();check(ink.Pixel(500,30).r==255,"Redo eraser failed");
                string json=ink.ToJson();check(restored.LoadJson(json),"JSON restore failed");check(restored.Pixel(400,80).r==TabletInk.Palette[1].r&&restored.Pixel(500,30).r==255,"Saved strokes differ");
                int before=restored.StrokeCount;check(!restored.LoadJson("{\"version\":99}")&&restored.StrokeCount==before,"Invalid save destroyed notes");
                ink.Clear();check(ink.Pixel(400,80).r==255&&ink.StrokeCount==0,"Clear failed");ink.Undo();check(ink.Pixel(400,80).g==TabletInk.Palette[1].g,"Undo clear failed");
                ink.Undo();ink.Begin(new Vector2(-200,-10),6,5,false);ink.Add(new Vector2(-200,200));ink.End();check(!ink.CanRedo&&ink.Pixel(0,100).b==TabletInk.Palette[6].b,"New stroke/edge clipping failed");
                check(ink.Save(path),"File save failed");int saved=ink.StrokeCount;
                ink.Begin(new Vector2(300,200),4,8,false);ink.End();check(ink.Save(path),"Atomic replace failed");
                File.WriteAllText(path,"damaged save");check(restored.Load(path)&&restored.StrokeCount==saved,"Backup recovery failed");
                foreach(var size in new[]{new Vector2Int(846,400),new Vector2Int(1280,720),new Vector2Int(2880,1800)})
                {
                    float scale=Mathf.Min(size.x/1280f,size.y/720f);var target=new Vector2(706,426);
                    var screen=new Vector2((size.x-1280*scale)*.5f+target.x*scale,size.y-((size.y-720*scale)*.5f+target.y*scale));
                    check(Vector2.Distance(PrisonPresentation.TabletPointer(screen,size.x,size.y),target)<.01f,"Mouse/canvas alignment at "+size);
                }
                System.IO.File.WriteAllText("Logs/InkChecks.txt","PASS "+count+" checks: continuous fast strokes, color, eraser, undo/redo/clear, clipped edges, JSON restore, atomic file replacement, corrupt-file backup recovery, mouse mapping at 3 resolutions.");
            }
            finally{ink.Dispose();restored.Dispose();foreach(var file in new[]{path,path+".bak",path+".tmp"})if(File.Exists(file))File.Delete(file);}
        }
    }
}
