# 실패 폭발 연출

- WAR FX 1.8.1은 Package Manager의 My Assets에서 다운로드·임포트했습니다. 원본은 `Assets/JMO Assets` 아래에 유지합니다.
- 게임 전용 프리팹은 `Resources/Effects/FailureExplosion.prefab`입니다. WAR FX의 `WFX_Explosion`을 기반으로 하며, 현재 URP 환경에 맞춘 머티리얼은 `Materials/FailureEffects`에 있습니다. 기존 씬 구조나 다른 에셋 경로는 변경하지 않습니다.
- 시간 초과 또는 현재 폭탄의 오답 3회 시 서버가 실패 시각을 기록합니다. 플레이어 1은 플레이어 2의 방 CCTV, 플레이어 2는 플레이어 1의 방 CCTV를 봅니다.
- CCTV 전환 후 0.8초에 양쪽 폭탄이 폭발하고 캐릭터가 물리 래그돌로 밀려납니다. 실패 시점부터 5.5초가 지난 뒤 결과 UI와 재시작 버튼을 표시합니다. 서버도 연출 중 준비 요청을 거부합니다.
- 연결 종료에 따른 실패는 폭발 사망으로 처리하지 않습니다. 이미 시작된 폭발은 연결 해제 시 정리됩니다.
- 재시작하면 래그돌 물리 컴포넌트, 캐릭터 포즈, 가시성, 카메라 화각을 복구합니다. 시신은 각 클라이언트의 로컬 물리 연출이며, 세부 관절 움직임은 두 기기에서 조금 다를 수 있습니다.
- 폭발음은 게임에서 생성한 짧은 폭발음을 사용하며 마스터/UI 음량을 따릅니다. WAR FX 원본에는 사운드를 추가하지 않습니다.

## 조정과 검증

- CCTV 전환 후 폭발 지연: `FailureCinematic.BlastDelay`
- 결과 UI 대기 시간: `RoundRules.FailureSequenceSeconds`
- 밀려나는 힘과 관절: `FailureRagdoll`
- 폭발 크기·머티리얼 재생성: `Tick To Talk > Prepare WAR FX Failure Explosion`
- 플레이 모드 통합 검사: `Tick To Talk > Verify Failure Cinematic` (현재 감옥 씬에서 실행, 테스트 후 플레이 모드 종료)
- 검사 결과와 단계별 화면은 `Logs/FailureSequenceChecks.txt`, `Logs/Failure-*.png`에 기록됩니다.
