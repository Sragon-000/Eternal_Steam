# Unity Play 진입 시 자동 새 게임

> 당시 검증 기록이다. 2026-09-30 실제 소스에서 시작 처리가 누락된 것을 확인해 복구하고 네 가지 Enter Play Mode 조합을 재검증했다. 현행 상태는 [QA-01 재검증](2026-09-30-qa01-auto-new-game.md)을 따른다.

사용자 요청에 따라 Editor의 Play 진입마다 기존 진행 파일을 archive에 보관한 뒤 새 메인 기지를 설치하도록 변경했다. SubsystemRegistration에서 세션 플래그를 초기화하고 첫 Persistence 초기화에서 한 번만 소비한다. 플레이 중 이어하기/새 게임의 씬 재로드에서는 플래그가 다시 켜지지 않는다. 빌드 실행의 기존 저장 복원 정책은 유지한다. archive 실패 시 덮어쓰지 않고 오류 상태로 멈춘다.

수정: OpenWorldSandbox.InitializePersistence/ResetPlaySession, SingleMapPersistence.Initialize(bool).

검증: 5개 어셈블리 독립 컴파일 성공. 실제 Unity Editor에서 임시 저장 경로에 기존 저장과 패배 기록을 준비하고 Play 진입: 메인 기지 설치·Blocked=false·새 저장 성공. 이어하기 후 runId와 메인 기지 ID 동일, archive에 이전 current.json 존재. Play 종료 후 재진입: 다른 runId와 메인 기지 생성 확인. 검증 후 Edit mode로 복귀하고 임시 저장 환경변수를 원복했다. 사용자 저장 파일 및 씬 파일은 저장/수정하지 않았다. 씬은 검증 전부터 dirty였으며 변경 폐기하지 않았다. 도메인 재로드 비활성 설정은 코드 대응만 포함하며 별도 조합 실행 검사는 하지 않았다.
