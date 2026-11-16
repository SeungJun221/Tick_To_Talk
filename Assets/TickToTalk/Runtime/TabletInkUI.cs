using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TickToTalk
{
    public sealed partial class PrisonPresentation
    {
        TabletInk ink;
        int inkColor,inkControl,savedInkRevision;
        float penSize=3;
        bool eraser,inkSaveFailed;
        static readonly Rect InkArea=new Rect(340,280,732,293);
        string InkPath=>Path.Combine(Application.persistentDataPath,"tablet-handwriting.json");
        void StartInk(){ink=new TabletInk();ink.Load(InkPath);savedInkRevision=ink.Revision;}
        bool InkDirty=>ink!=null&&ink.Revision!=savedInkRevision;
        void SaveNotes()
        {
            if(ink==null)return;ink.End();if(!InkDirty)return;
            inkSaveFailed=!ink.Save(InkPath);if(!inkSaveFailed)savedInkRevision=ink.Revision;
            saveNotesAt=Time.unscaledTime+3;
        }
        void EndInk()
        {
            if(ink==null)return;
            if(ink.Drawing){ink.End();saveNotesAt=Time.unscaledTime+.75f;}
            if(inkControl!=0&&GUIUtility.hotControl==inkControl)GUIUtility.hotControl=0;
        }
        void OnApplicationFocus(bool focus){if(!focus){EndInk();SaveNotes();}}
        void OnApplicationPause(bool paused){if(paused){EndInk();SaveNotes();}}
        public static Vector2 TabletPointer(Vector2 screenPosition,int screenWidth,int screenHeight)
        {
            float scale=Mathf.Min(screenWidth/1280f,screenHeight/720f);
            return new Vector2((screenPosition.x-(screenWidth-1280*scale)*.5f)/scale,(screenHeight-screenPosition.y-(screenHeight-720*scale)*.5f)/scale);
        }
        Vector2 InkPoint(Vector2 point,Rect area)
        {
            return new Vector2(
                (point.x-area.x)/area.width*(TabletInk.Width-1),
                (point.y-area.y)/area.height*(TabletInk.Height-1));
        }
        void HandleInkInput(Rect area)
        {
            inkControl=GUIUtility.GetControlID("TabletPenCanvas".GetHashCode(),FocusType.Passive);
            var mouse=Mouse.current;
            var e=Event.current;
            if(mouse==null||clearNotes)return;
            var point=TabletPointer(mouse.position.ReadValue(),Screen.width,Screen.height);
            bool inside=area.Contains(point);
            if(e.type==EventType.MouseDown&&e.button==0&&inside)
            {
                GUI.FocusControl(null);
                if(ink.Begin(InkPoint(point,area),inkColor,penSize*(eraser?6:1.6f),eraser))
                    GUIUtility.hotControl=inkControl;
                e.Use();
            }
            if(GUIUtility.hotControl!=inkControl)return;
            if(e.type==EventType.MouseDrag&&e.button==0)
            {
                if(inside)
                {
                    if(!ink.Drawing)ink.Begin(InkPoint(point,area),inkColor,penSize*(eraser?6:1.6f),eraser);
                    else ink.Add(InkPoint(point,area));
                }
                else if(ink.Drawing)
                {
                    ink.End();
                    saveNotesAt=Time.unscaledTime+.75f;
                }
                e.Use();
            }
            if(e.type==EventType.MouseUp&&e.button==0)
            {
                if(inside&&ink.Drawing)ink.Add(InkPoint(point,area));
                EndInk();
                e.Use();
            }
        }
        void DrawInkCanvas(Rect area,float yOffset=0)
        {
            var screenArea=new Rect(area.x,area.y+yOffset,area.width,area.height);
            GUI.DrawTexture(area,ink.Texture,ScaleMode.StretchToFill);
            HandleInkInput(screenArea);
            if(Event.current.type==EventType.Repaint&&Mouse.current!=null)
            {
                var point=TabletPointer(Mouse.current.position.ReadValue(),Screen.width,Screen.height);
                if(screenArea.Contains(point)&&!clearNotes)
                {
                    var localPoint=new Vector2(point.x,point.y-yOffset);
                    float size=eraser?penSize*3.6f:Mathf.Max(4,penSize);
                    var previous=GUI.color;
                    GUI.color=new Color(.2f,.23f,.35f,.35f);
                    GUI.DrawTexture(new Rect(localPoint.x-size*.5f,localPoint.y-size*.5f,size,size),roundCorner);
                    GUI.color=previous;
                }
            }
        }

        bool InkButton(Rect rect,string text,bool selected=false)
        {
            var old=GUI.backgroundColor;GUI.backgroundColor=selected?new Color(.35f,.4f,.75f):new Color(.83f,.84f,.9f);
            var style=new GUIStyle(GUI.skin.button){font=font,fontSize=17};bool clicked=GUI.Button(rect,text,style);GUI.backgroundColor=old;return clicked;
        }
        void DrawInkPage(GUIStyle inkStyle,GUIStyle fine,float yOffset=0)
        {
            bool enabled=GUI.enabled;GUI.enabled=!clearNotes;
            TabletRect(new Rect(185,204,140,420),new Color(.86f,.87f,.92f),12);
            Label(204,220,112,30,"메모장",inkStyle);
            if(InkButton(new Rect(202,261,53,36),"펜",!eraser)){EndInk();eraser=false;}
            if(InkButton(new Rect(260,261,53,36),"지우개",eraser)){EndInk();eraser=true;}
            for(int i=0;i<TabletInk.Palette.Length;i++)
            {
                var rect=new Rect(208+(i%2)*58,315+(i/2)*44,36,36);
                if(i==inkColor&&!eraser){var old=GUI.color;GUI.color=new Color(.18f,.2f,.34f);GUI.DrawTexture(new Rect(rect.x-4,rect.y-4,44,44),roundCorner);GUI.color=old;}
                var before=GUI.color;GUI.color=TabletInk.Palette[i];GUI.DrawTexture(rect,roundCorner);GUI.color=before;
                if(GUI.Button(rect,GUIContent.none,GUIStyle.none)){EndInk();inkColor=i;eraser=false;}
            }
            Label(204,548,115,25,(eraser?"지우개":"펜")+" 굵기 "+Mathf.RoundToInt(penSize),fine);
            penSize=GUI.HorizontalSlider(new Rect(205,581,104,20),penSize,1,10);
            Label(342,223,460,40,"파트너의 단서",new GUIStyle(inkStyle){fontSize=27,fontStyle=FontStyle.Bold});
            Label(343,253,620,24,"왼쪽 버튼을 누른 채 필기하세요.",fine);
            GUI.enabled=!clearNotes&&ink.CanUndo;
            if(InkButton(new Rect(821,223,117,33),"실행 취소")){EndInk();ink.Undo();}
            GUI.enabled=!clearNotes&&ink.CanRedo;
            if(InkButton(new Rect(945,223,125,33),"다시 실행")){EndInk();ink.Redo();}
            GUI.enabled=!clearNotes;
            DrawInkCanvas(InkArea,yOffset);
            string status=ink.Full?"필기 용량이 찼습니다. 지우고 새로 적어 주세요.":inkSaveFailed?"저장 실패 · 다시 저장을 시도합니다.":InkDirty||ink.Drawing?"저장 중…":"자동 저장됨 · 이 기기에만 보관";
            Label(342,588,570,26,status,fine);
            if(InkButton(new Rect(923,582,149,32),"모두 지우기")){EndInk();clearNotes=true;}
            GUI.enabled=enabled;
        }
    }
}
