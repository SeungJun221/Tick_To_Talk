using UnityEngine;
namespace TickToTalk {
 public sealed partial class NetworkSession {
  public bool TestSolvePuzzle(int room,bool wholeBomb=false){
   // A local-only test operation. No network command exposes this bypass.
   if(!IsTestMode || (net && net.IsListening) || Rules.Phase!=1 || Rules.Bombs==null || room<0 || room>1)return false;
   Rules.Tick(Now);if(Rules.Phase!=1)return false;
   var bomb=Rules.Bombs[room];int count=wholeBomb?4:1;
   for(int i=0;i<count && Rules.Phase==1 && !bomb.Armed;i++)notices[0]=Rules.Submit(room,bomb.Answers[bomb.Stage],Now);
   View=CreateView(0);return true;
  }
 }
 public sealed partial class PrisonPresentation {
  void DrawDeveloperTools(){
   Label(160,195,960,45,"테스트 모드 / 원격 퍼즐 해제",title);
   if(session.Rules.Phase!=4)Label(160,245,960,55,"방이나 폭탄 위치와 관계없이 해제합니다. 최종 단계는 양쪽 해제가 필요합니다.",small);
   GUI.enabled=session.Rules.Phase==1;
   for(int room=0;room<2;room++){
    float y=310+room*105;var bomb=session.Rules.Bombs==null?null:session.Rules.Bombs[room];
    string progress=bomb==null?"대기":bomb.Armed?"최종 차단 준비":(bomb.Stage+1)+"단계";
    Label(180,y+10,250,40,(room==0?"A 방":"B 방")+" · "+progress,body);
    if(Button(440,y,290,"현재 퍼즐 성공"))session.TestSolvePuzzle(room);
    if(Button(760,y,330,"이 방 전체 해제"))session.TestSolvePuzzle(room,true);
   }
   if(Button(440,525,650,"양쪽 방 전체 해제 / 출구 열기")){session.TestSolvePuzzle(0,true);session.TestSolvePuzzle(1,true);}
   GUI.enabled=true;
   if(session.Rules.Phase==4)Label(180,245,900,55,"출구가 열렸습니다. 설정을 닫고 직접 문을 통과하면 탈출 성공입니다.",small);
  }
 }
}
