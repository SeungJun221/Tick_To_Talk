# 구현 및 검증 기록

## 점프·달리기 / 퍼즐·음성 수정

- Space 점프와 Shift 달리기를 서버에서 판정하고 상대 클라이언트에 높이·달리기 상태 동기화.
- Creative Characters FREE의 Idle, Walk, Run, Jump Start/Loop/End 애니메이션 연결.
- IP 입력 UI, 직접 IP 연결 함수, 전용 서버 실행 명령 제거. Relay 코드 접속만 사용.
- 2,000개 시드에서 표시된 문제/힌트를 파싱한 독립 풀이 36,000개 검증 재통과. 계산식 불일치는 발견되지 않음.
- 제출 단계 식별자를 검사해 단계 전환 직전 답안이 다음 문제에 적용되는 상황 방지.
- 완료한 내 단계의 정답 기록과 실패 후 A/B 전체 정답·풀이 화면 추가. 진행 중 미래 정답은 전송하지 않음.
- 기본 마이크/장치 선택, 대기실 입력 확인, 송신·수신 카운터 추가.
- 16 kHz PCM 버퍼를 출력 장치 샘플레이트로 변환하여 OnAudioFilterRead에서 재생. 60ms 초기 버퍼 적용.
- PCM 출력/리샘플링/패킷 순서/종료 시 무음 검증: `Logs/AudioOutputTests.txt`.
- 이전 `RelayBuildStatus.txt` 빌드는 실행 중인 구버전 DLL 잠금으로 실패했음. 최신 결과물은 `Builds/OnlineWindows`로 분리.


## Relay 온라인 방 추가 검증

- Multiplayer Services 2.4.1 / Unity Authentication / Relay DTLS 통합.
- 연결된 실제 Unity 클라우드 프로젝트에서 익명 로그인, 방 생성, 영문·숫자 접속 코드 발급 성공.
- 개발 빌드의 독립 프로세스 두 개가 같은 실제 Relay 방에 접속해 A/B 배정과 라운드 시작 확인.
- 방장 → 참가자 음성 패킷 198개, 참가자 → 방장 11개 이상 수신 확인. 이 검증은 실제 녹음 대신 합성 PCM을 사용함.
- 서버 거리 검사, 마이크 10초 발언·10초 전체 잠금, 상대 발언권 획득 검증 통과.
- 클라우드 온라인 방 생성/코드 참가/코드 복사 UI 추가. 재실행 시 GUIStyle 초기화로 글자가 검게 보이던 문제 수정.
- 테스트 자동 조작 코드는 개발 빌드에만 포함되며 출시용 Windows 빌드에서 제외됨.
- 로그: `Logs/RelayHostTest.log`, `Logs/RelayClientTest.log`, `Logs/RelayBuildStatus.txt`.

## 완료한 검증

- Unity 6000.6.0f1 컴파일 성공. Netcode for GameObjects 2.13.0 사용.
- Windows 클라이언트/전용 서버 실행 파일 빌드 성공.
- 독립 .NET 테스트: 2,000개 랜덤 시드, 36,000개 검사 통과. 정답 내부 배열을 풀이 입력으로 쓰지 않고 화면의 문제와 상대 매뉴얼을 파싱해 4단계의 해답을 독립 계산 후 대조.
- Unity 편집기 규칙 테스트: 2,000개 시드, 34,000개 검사 통과.
- 별도 Windows 전용 서버 프로세스 + Unity 편집기 UDP 테스트 클라이언트 2개로 접속 검증 통과.
- A/B 서로 다른 역할과 비공개 문제/상대 힌트 전달, 서버 이동, 폭탄에서 멀리 떨어진 제출 거부 확인.
- 10초 발언 제한, 10초 전역 잠금, 이후 상대 발언권 획득 확인.
- 합성 PCM 음성 패킷 157개 중계 및 상태 스냅샷 830개 수신 확인.
- 씬 및 메뉴 화면 캡처 검토. 초기 UI 상태, 안내판 중첩, 마이크가 상대를 가리는 시야를 수정.

## 검증 범위의 한계

- 네트워크 검증은 같은 PC에서 분리된 서버 프로세스와 클라이언트 인스턴스를 사용했습니다. 실제 두 PC 및 외부 인터넷 경로는 미검증입니다.
- 음성 중계 테스트는 합성 PCM을 사용했습니다. 두 사람의 실제 마이크 입력, 헤드폰 출력, 음질과 OS 장치 권한은 실제 장비에서 확인해야 합니다.
- Relay 실제 클라우드 중계는 검증했습니다. 게임 판정은 방장 PC에서 수행하며, 상시 실행하는 클라우드 전용 게임 서버나 Vivox는 배포하지 않았습니다. 직접 IP 모드는 제거했습니다. 아래 전용 서버 검증은 이전 구현의 기록입니다.
- 5분 난이도는 전문가용 계산 퍼즐로 설계했지만, 실제 플레이어의 성공률과 소요시간을 통한 난이도 조정은 아직 하지 않았습니다.

## 외부 캐릭터 팩 수정

`Assets/ithappy/Creative_Characters_FREE/Scripts/Editor/ithappy.Creative_Characters_FREE.Editor.asmdef`를 추가해 캐릭터 커스터마이저의 편집기 코드가 Windows 플레이어 빌드에 들어가지 않도록 했습니다. 원본을 가져온 다른 Unity 프로젝트는 수정하지 않았습니다.

기본 AI Assistant의 `NoSubscription` 메시지와 캐릭터 원본 모델의 오래된 MaterialLocation 경고는 게임 코드의 컴파일 오류가 아닙니다.

씬: `Assets/TickToTalk/Scenes/TickToTalk_Prison.unity`

로그: `Logs/TickToTalk_PuzzleTests.txt`, `Logs/TickToTalk_NetworkTests.txt`, `Logs/TickBuildStatus.txt`

## 최신 실제 실행 검증
- GameplayHost/Client: Relay 두 프로세스 양방향 음성 패킷, 점프 약 0.92m 및 착지 동기화 통과.
- GameplayChecks: 걷기 3.25m/s, 달리기 5.63m/s, 실제 메시지 정답 수락, 진행 중 정답 비공개와 실패 후 공개 통과.
- LiveAudioCallback: Unity 실제 오디오 스레드에서 합성 PCM 3,200개 소비 확인. 물리적 헤드폰 청취는 미검증.
- AnimationTests: 캐릭터 달리기 뼈대 포즈 변화와 점프 상태 재생 확인.

## 첫 치환 퍼즐 및 점프 모션 개정
- 첫 퍼즐 계산에서 위치번호 제거: (해독표 숫자 + 보정값) % 10. 매뉴얼, 실패 풀이, README, 독립 테스트 동시 갱신.
- 새 규칙으로 2,000개 시드 / 36,000개 독립 검증 통과.
- 시각적 높이 보간값으로 점프 단계를 추측하던 방식 제거. 서버의 접지 여부로 도약·공중·착지 상태 전환.
- 도약 0.18초 / 착지 0.24초로 클립 재생 조정. 공중 클립 반복 설정. 원본 에셋 대신 프로젝트 내부 복제 클립 사용.
- Logs/JumpRevisionTests.txt: 20Hz 스냅샷, 20/30/60/144fps, 빠른 재점프·착지·초기화 검사 통과.

## 제목 화면 / 설정 / 커스터마이징

- 제목 화면, 공용 ESC 설정 창, 화면 모드, 마스터/음악/UI/수신 음성/마이크 음량, 입력 장치 및 로컬 마이크 테스트 구현.
- 기존 로비 입력 테스트는 설정의 보이스 채팅 탭으로 이동. 설정 종료 시 테스트 중지.
- FREE 팩 SlotLibrary 전체: 11개 카테고리, 33개 선택지(전신 코스튬 포함), 저장·3D 미리보기·서버 검증·외형 스냅샷 동기화.
- Logs/FrontEndChecks.txt: 75개 검사 통과. 모든 외형 선택지, 잘못된 ID 거부, 실제 휴머노이드 점프 높이, 마스터 음량 적용, 마이크 테스트 정리 및 인게임 설정.
- 점프 클립 RootT 위치 곡선을 동일 기준으로 고정하고 지상 준비/낙하 구간을 제거. 실제 Hips 높이의 추가 이동은 0.18m 미만(이전 약 1m 추가 상승 제거).
- 화면 검토: Captures/FrontEnd_Title.png, Settings_Voice.png, Wardrobe_Final.png.

- 최신 실제 Relay 검사: Logs/FrontEndHost.log, FrontEndClient.log 모두 PASS. A/B 서로 다른 외형 동기화, 점프 약 0.94m, 양방향 합성 PCM 전달(11/198 패킷 이상) 통과.
- 배포 빌드: Logs/FrontEndReleaseBuild.txt SUCCEEDED. 실제 두 PC의 마이크/헤드폰 청취는 미검증.

## 게임 종료 / 혼자 테스트 / 현재 위치 착지
- 제목 화면 게임 종료, 접속 메뉴 테스트 모드 버튼 추가. 서버/Relay 연결 없이 1인 시작.
- V 비행 전환, WASD 이동, Space 상승, Ctrl 하강, Shift 가속. 테스트 타이머 정지.
- 비행 종료 시 X/Z를 유지하고 아래 표면에 착지. 방별 스폰 위치 제한을 적용하지 않아 B방에서도 계속 보행 가능. 테이블·옥상 표면 및 보행 장애물 검사 적용.
- Logs/SoloModeChecks.txt: 11개 검사 통과(오프라인 시작, 비행 제한, 유리 통과, 상승/하강, 설정 중 정지, 시간 정지, 현재 위치 착지와 B방 보행, 종료 정리, 재시작).
- 기존 온라인 경로 회귀 검사 Logs/GameplayChecks.txt: Relay 생성, 이동/달리기, 실제 정답 제출, 실패 정답 공개 통과.

- 2026-11-07: InteractionMusicChecks 13개 통과: A/B 폭탄 제출 분리, 원격 제출 거부, B 힌트, 양쪽 마이크, 별도 음악 에셋, 전환 및 볼륨 0, 테스트 종료 후 데이터 제한. 접속 메뉴 배치 스크린샷 확인.

- 2026-11-07: A/B 방의 문 옆 거울 플레이 모드 화면 확인(Captures/MirrorA.png, MirrorB.png). 반사 렌더 후 로컬 캐릭터 Renderer가 모두 숨김으로 복원됨을 확인. 거울은 보이는 경우에만 렌더링하고 미리보기 레이어와 거울 면 자체는 반사에서 제외합니다.

- 2026-11-07 난이도 조정: 1단계 규칙 유지. 2단계 문자 순서→단자 번호, 3단계 3×3 좌표 직접 읽기, 4단계 상대 코드 전달 및 15초 제출. 화면 힌트만으로 푸는 독립 검증 2,000 seed / 48,000 assertions 통과. 15초 경계 성공, 초과 시 준비 해제, 300초 타임아웃 검증. 실제 2인 평균 소요 시간은 별도 플레이테스트 필요.

- 2026-11-07 바닥: 색 띠 충돌면 0.0325m가 공중 판정 0.02m를 초과하는 원인 확인. 색 띠/이음선 42개를 충돌 없는 1mm 장식으로 변경하고 씬 및 생성 코드 반영. 양쪽 방 30개 지점의 발 높이 0 및 Idle 상태, 띠 위 점프/착지 검증 통과(Logs/FloorChecks.txt).
- CCTV: 양쪽 천장에 흰색 하우징/검은 렌즈/브래킷 배치. 두 표시등의 점등·소등 및 추적 회전 검증 통과(Logs/SurveillanceChecks.txt), 게임 화면 확인(Captures/CCTV_A.png).

- 2026-11-07 걷기 멈춤: 원본 Idle/Walk/Run의 Loop Time이 꺼져 있어 게임 전용 Loop 클립을 생성하고 컨트롤러 연결. Walk/Run 각각 12초 재생, 렌더러 숨김 상태에서도 8~12초 발 이동과 5회 이상 반복 검증 통과(Logs/LocomotionLoopChecks.txt).
- 거울: 고정 768px에서 화면 너비 기준 2048~3840px, 지원 시 4x MSAA로 변경. 반사 카메라의 동적 해상도 비활성화. 플레이 모드에서 2048x968/4x MSAA 및 화면 확인(Logs/MirrorResolutionChecks.txt).

- 2026-11-07 태블릿: 로컬 14개 검증 통과(개인 메모 한글 저장, 서버 이동/점프 제한, A/B 들기 플래그 직렬화, 양손 목표 도달, 닫기). 실제 Relay 2개 개발 클라이언트 모두 TTT_TABLET_SMOKE_PASS 기록: Logs/TabletHostClean.log, TabletClientClean.log. 두 로그에서 렌더링 중단 경고/예외 없음. 의상 교체 시 렌더러를 비활성화한 상태에서 메시를 교체하고 완료 후 활성화하도록 수정. GPU 설정은 기존 설정 유지.
- 출구/폭탄/상대 힌트 판 영문 텍스트 6개 제거, 양쪽 마이크 안내를 '마이크 / 10초 발언 · 10초 잠금'으로 변경. 한글 글리프 및 플레이 화면 확인. TabletNotebookFinal.png, TabletHold.png, KoreanMicrophoneSign.png 참고.

- 2026-11-07 스트리밍 수정: StreamingChecks 9개 통과. 이동 보간, 350→10도 회전 보간, 오래된 위치 거부, 6초 음성의 순서 역전/17개당 1개 패킷 손실, 발언 종료, 재접속 순번 초기화 검증(Logs/StreamingChecks.txt).
- 거울 수정: 갱신 제한 제거, 반사 VP 행렬 및 DirectX 텍스처 Y 방향 보정, 반사 카메라 회전 일치. Captures/MirrorCorrected.png에서 정상 방향 캐릭터 확인. 기존 고해상도 기록 이후 성능을 고려해 1024~2048px/지원 시 4x MSAA로 조정.
- Relay 두 프로세스: StreamingHost.log / StreamingClient.log 모두 TTT_CLOUD_SMOKE_PASS. 이동·점프·외형 동기화와 양방향 음성 패킷 전달 통과. 예외/Rendering stopped 없음. 서로 다른 실제 PC 및 마이크의 청취 검증은 미실시.

- 엄지 아래: 에셋의 2관절 손가락에 맞춰 주먹 굽힘, 엄지 방향과 손목 방향 적용. ThumbRefined.png에서 확인. 엄지 하향 dot > .98, 접힌 손가락 dot < 0, 종료 후 가중치 0 검증.
- 양쪽 게시판 TextMesh 반대 방향을 수정하고 글자 확대. 실제 E 입력으로 A 안내 열기/닫기 및 B 안내 열기 통과. Logs/ThumbBoardChecks.txt, Captures/BoardInstructions.png. 실행 오류 없음.


- 2026-11-08: EscapeChecks 13개 통과. 대기 감옥, 움직이기 시작, 테스트 타이머/시간초과, 두 사람 퇴실 후 닫힘, 닫힌 문 충돌, 해체와 탈출 상태 분리, 한 사람 통과 성공, 재시작 초기화.
- 실제 콜라이더 이동 검사: CellWalkCheck.txt에서 감옥→본방 통과 및 문 닫힘, ActualEscapeCheck.txt에서 출구 z=-4.652778 통과 후 phase=2 확인. SeatedTitleFinal.png / SunlitExit.png 화면 확인.
- 태블릿: 실제 손바닥 양쪽 방향 내적 0.98 이상, 양쪽 캐릭터 손 도달 및 네트워크 들기/놓기 등 TabletChecks 14개 통과. TabletGripFixed.png / TabletGripChecks.txt. 퍼즐 2,000 seed / 54,000 assertions 통과.
- 실제 Relay 두 실행 파일 EscapeHost.log / EscapeClient.log 모두 TTT_ESCAPE_SMOKE_PASS: 두 사람 준비, 입구 개방, 두 사람 퇴실 후 닫힘, 출구 해제, 한 사람만 통과 후 양쪽 성공 동기화. 예외 없음. SoloModeChecks의 비행·설정 중 정지·현재 위치 착지·진행 타이머·재시작 통과. TestTimerBottomLeft.png에서 단일 타이머와 왼쪽 아래 TEST 확인.

- 2026-11-08 자동 시작/개발자 도구: AutoToolsChecks 13개 통과. 실제 일어남 완료 뒤 2초 대기, 타이머 시작, 방별 원격 성공, 단계 분리, 최종 차단 만료 복구, 양쪽 해제 후 출구 개방. 활성 일반 플레이 상태에서 호출 거부 별도 통과.
- 왼손을 무릎 바깥쪽으로 이동: 시작 자세 8초 동안 손목–무릎 최소 거리 0.291m, 화면 확인(LeftHandOutsideKnee.png). DeveloperToolsActive.png에서 테스트 설정 탭/버튼 확인.
- 일반 클라이언트 두 개(준비/이동 자동 입력 드라이버 없이)로 Relay 검사: 방장 단독 대기 중 라운드 미시작, 참가자 접속 및 일어남 완료 뒤 TTT_ROUND_STARTED 확인. AutoHost.log / AutoClient.log, 예외·렌더 중단 없음. 기존 탈출 흐름 13개·태블릿 회귀 14개 통과.
