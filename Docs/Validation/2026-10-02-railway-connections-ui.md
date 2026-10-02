# 철도 명시적 연결·건설 도우미·전용 스프라이트 적용

2026-10-02. 사용자 요청: 철도 구조/UI 수정과 테스트 계속, 기존 UI 스타일에 맞춘 전용 스프라이트 생성·적용, 테스트 UI 사용 금지.

## 적용 범위

- `RailConnection`: 역 ID, 양끝 연결구, 순서가 고정된 셀 경로를 화물 설정과 분리. 연결된 선로는 옆 칸에 다른 선로가 생겨도 자동 분기하지 않는다. 손상된 연결은 원래 경로 복구와 수동 재시작을 요구한다.
- Snapshot `connectionVersion=1`: 노선 미배정 연결과 모든 설치 선로의 건물 ID/셀 포함. 중복 연결구·셀·잘못된 ID·단절 및 노선/물리 연결 불일치를 검증한다. 그래프 없는 구저장은 확정된 왕복 노선의 순서를 먼저 복원한다. 맵 컨테이너 v3/콘텐츠 지문은 변경하지 않았다.
- 역 선택 → 연결구 자동/직접 지정 → 경로 미리보기 → 확정. 신규/재사용 칸, 연결구, 거리, 회전, 지불 기지, 동일 건설 Quote를 표시한다. 탐색은 요청 시 실행하며 프레임마다 전체 맵을 검색하지 않는다.
- 탐색 비용 순서: 신규 칸, 회전, 길이. 확장 40,000 상태 제한, 탐색 실패/한도/취소 상태 분리. 건설은 신규 512칸 제한. 기존 연결/노선이 소유한 선로는 침범하지 않는다.
- 실제 `WorldEditSession`/`ConstructionPurchase`/`PlacementSession` 사용. 최종 연결 검증을 설치 트랜잭션 안에 포함해 실패하면 신규 건물·점유·재고를 되돌린다. 새 비용 정책은 만들지 않았다(선로 철1/칸, 역 철10/개 유지).
- 지도 클릭을 철도 초안으로 전달하고, Esc/우클릭을 초안 닫기에 연결했다. 기존 저장 uGUI 계층과 영구 콜백을 사용한다.

## 실제 UI 자산

내장 `image_gen`으로 기존 `ReferenceHUD/Panel.png`, `Card.png`, `Tools.png`를 스타일 참조하여 새 PNG 3개를 생성했다. 원본 이미지 변경/코드 그림 대체 없이 프로젝트에 복사했다.

- [RailwayPanel.png](../../Assets/EternalSteam/Shared/UI/Railway/RailwayPanel.png): 짙은 청록 바탕, 금속색 이중 테두리, 금색 모서리. 9-slice.
- [RailwayButton.png](../../Assets/EternalSteam/Shared/UI/Railway/RailwayButton.png): 같은 계열의 전용 버튼 바탕. 9-slice, 기존 컬러 전환 상태 적용.
- [RailwayIcon.png](../../Assets/EternalSteam/Shared/UI/Railway/RailwayIcon.png): 철도 제목의 기관차 아이콘.
- [최종 프롬프트 원문](../Measurements/2026-10-02-railway-connections/sprite-prompts.json). 생성 방식: 내장 도구, CLI/API 키 사용 없음.
- Unity 공식 `com.unity.2d.sprite@1.0.0`을 UPM으로 추가했다. Sprite Editor data provider의 capability 검사를 통과한 뒤 알파 영역 Rect와 border를 설정했다. PNG 픽셀은 후처리하지 않았다.

`StartRegionSandbox`와 `OpenWorldSandbox`의 저장 Canvas에서 각각 철도 버튼 **76개**와 패널에 전용 자산을 적용했다. `ShowDevelopmentControls=false`로 자원 무한/낮·밤 전환/개발 패널을 기본 게임 화면에서 숨겼다. 새 정적 패널이나 버튼을 런타임에 생성하지 않는다. 씬 이름의 Sandbox는 기존 파일명이다.

[실제 3840×2160 게임 렌더](../Measurements/2026-10-02-railway-connections/connection-ui-4k.png) · [저장 UI 검사](../Measurements/2026-10-02-railway-connections/saved-ui.json) · [Play UI 검사](../Measurements/2026-10-02-railway-connections/styled-play-ui.json).

## 재현 조건과 결과

- Unity 6000.3.14f1, macOS, 실제 `StartRegionSandbox` 씬. 무한 자원 OFF. 시계 일시정지 후 정상 철도 Tick을 명시적으로 진행한다.
- 검증용 서브 기지를 설치해 두 역의 작동 범위를 확보하고, 메인 재고에 철1,000/석탄100을 주입했다. 이 준비는 자연 생산 플레이로 세지 않는다. 역/선로 건설 비용은 정상 API로 결제한다.
- 신규 선로3칸 + 기존2칸 = 연결5칸, 추가 철3 차감. 연결 생성만으로 열차를 만들지 않는다. 자원 부족·지불 기지 변경·기존 미리보기 셀의 추가 점유·중복 확인에서 추가 연결/부분 설치/중복 차감 없음.
- 미배정 연결 저장과 귀환 운행 저장의 실제 씬 재로드를 검증했다. `savedUtc`만 정규화한 스냅샷 전체 비교를 사용한다. 별도 저장 디렉터리와 원본 JSON을 보존했다.
- 실제 포인터로 생성 스프라이트의 취소 버튼 클릭: 초안 종료, 철975/연결1 유지. Game 창 포커스 후 실제 Esc: 초안 false, 패널 false. 지도 목적역 변경은 실제 `OpenWorldInput.ClickWorld` 경로로 검사했다.
- EditMode: 최초 **256/260** → fixture 수정 후 **260/260**. 스프라이트 적용 후도 **260/260**. 입력 수정까지 포함한 최종 재실행도 **260/260, 15.57초, 실패0/건너뜀0**이다.
- 새 회귀: 평행 선로 오연결 방지, 미배정 연결 저장, 구그래프 이행, 손상 ID/연결구/단절 거부, 원래 경로 유지, 500×500 탐색 결정성, 재사용 우선, 한도·실패·취소 구분, 설치 후 콜백 거부/예외 시 원자적 롤백. 기존 249개 회귀를 포함한다.

원본: [최종 EditMode](../Measurements/2026-10-02-railway-connections/editmode-final-result.json), [Play 실측](../Measurements/2026-10-02-railway-connections/play-measurements.json), [운행 재로드](../Measurements/2026-10-02-railway-connections/play-styled-reload-running.json), [포인터 취소](../Measurements/2026-10-02-railway-connections/pointer-cancel.json), [Esc](../Measurements/2026-10-02-railway-connections/keyboard-cancel.json), [순수 탐색 실측](../Measurements/2026-10-02-railway-connections/planner-performance.json).

순수 탐색 실측은 실제 Terrain/배치 규칙 비용을 포함하지 않는 500×500 합성 격자다. warm-up 1회 후 각 20회. 빈 격자 median 12.20ms/p95 17.00ms, 벽 격자 median 8.29ms/p95 14.49ms이며 각각 1,498/1,248 상태를 확장했다. 경로 999칸은 코어 탐색 실측이고 UI의 512 신규 칸 건설 한도와 구분한다. 실제 씬의 5칸 미리보기 시간은 `play-measurements.json`에 별도로 기록했다. 이 수치로 전체 게임 프레임 시간이나 자연 생산 밸런스를 판정하지 않는다.

## 발견한 실패와 처리

1. 신규 롤백 테스트의 건물 DisplayName/ViewPrefab 누락으로 배치 자체가 거부된 2건: 정상 fixture로 수정하고 Add 성공을 선행 assert했다.
2. 기존 이름 테스트 2건이 새 네트워크에 건물을 등록하기 전에 그래프를 복구: 실제 맵 복구와 동일한 등록→복구 순서로 수정했다. ID/셀 검증은 완화하지 않았다.
3. 첫 Play fixture가 기지 작동 범위 밖의 역을 생성: 비활성 역 진입이 정상 거부됨을 확인하고 범위 내 역으로 수정했다.
4. 사용하지 않은 서브 기지의 빈 재고 장부가 재로드에서 지연 생성되어 전체 JSON 비교가 달라짐: 초기 빈 장부를 fixture에서 명시 초기화했다. 첫 차이 JSON은 보존했고 빈 장부 증가를 자원 손실로 보고하지 않는다.
5. Sprite Editor 패키지 누락으로 저작 스크립트 컴파일 실패: 공식 패키지를 추가하고 API capability 검사 후 적용했다.
6. 키보드 입력 첫 시도에서 창 포커스/초안 상태가 기대와 달랐음: Game 창 포커스와 새 초안을 확인한 후 다시 보내 최종 초안/패널 종료를 기록했다.
7. EditMode 씬 테스트가 Play fixture와 같은 격리 저장 경로를 사용하면서 해당 임시 진행을 아카이브함: 최종 회귀 저장 경로를 별도로 분리했다. 사용자 저장에는 접근하지 않았다. 최종 스프라이트 Play fixture는 새로 만들고 비용·입력·운행 저장 검증을 반복했다.

## 소스 기준 및 재실행

기준 HEAD: `7e6dabe890603bdd183c4680420fd53986d3498e`. 작업 시작 시 다수의 기존 수정/미추적 파일이 존재했다. 본 작업 파일별 시작 SHA-256과 원본, 씬 gzip 원본은 [baseline.json](../Measurements/2026-10-02-railway-connections/baseline.json)에 보존한다. 최종 파일 지문은 `implementation-manifest.json`, 전후 패치는 `diffs/`에 기록한다. 기존 변경을 정리하거나 커밋하지 않았다.

재실행 도구:

- `Tools/AuthorRailwayConnections.cs`, `Tools/AuthorRailwaySprites.cs`: Edit Mode/깨끗한 씬에서 저장 계층·자산 저작.
- `Tools/VerifyRailwayConnections.cs`: 두 씬의 저장 참조, 영구 콜백, 76개 스프라이트 버튼, 테스트 UI 숨김 검사.
- `Tools/VerifyRailwayConnectionsPlay.cs`: 새 격리 fixture `Build` → 실제 `ContinueSaved` → `ReloadAndRun` → 실제 `ContinueSaved` → `ReloadRunning`. 실행 전 테스트 루트 문자열을 새 전용 디렉터리로 지정한다.
- `Tools/MeasureRailPathPlanner.cs`: 순수 탐색 500×500 실측.
- `unity command run_tests --mode editor --filter EternalSteam --filter_type assembly --async_tests true --format json`, 이후 `test_status`의 내부 결과까지 확인.

격리 경로: `/tmp/eternal-connections-20261002`, `/tmp/eternal-connections-20261002-final`, `/tmp/eternal-connections-regression-20261002`. 처음 두 경로와 실패 원본을 삭제하지 않았다.

## 이번 단위에서 완료로 세지 않는 범위

고급 경유점, 연결 해제/재배선 UX, 모호한 구형 미배정 선로 전체의 자동 이행, 다수 역 전용 목록/검색, 상태별 추가 배지/스프라이트 Atlas/공통 프리팹 추출, 1280×720·1920×1080 전수 화면/긴 이름/모달 인수, 자연 생산부터 야간 방어까지 한 회차, 최종 경제 수치 설계는 후속 항목이다. 일반 수동 선로의 신규 배치는 여전히 단순 무분기 경로에서 명시 연결을 확정하는 호환 입력으로 사용한다. T자·교차·공용 선로는 지원하지 않는다.

최종 복구: Play=false, compiling=false, StartRegionSandbox dirty=false, runInBackground=false, 격리 환경 변수=null. [원본 상태](../Measurements/2026-10-02-railway-connections/editor-final.json).
