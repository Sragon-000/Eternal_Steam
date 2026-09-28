# 실제 Unity 심층 테스트

2026-09-28. 사용자가 실제 Unity 프로젝트를 열어 심층 테스트를 요청했다.


## 후속 실제 실행 결과 — Rosetta 설치 후

사용자 승인으로 Rosetta 2 설치를 완료하고 x86_64 실행을 확인했다. 사용자가 연 Unity 6000.3.14f1에 Unity CLI로 연결했으며, 현재 씬의 미저장 변경은 사용자 승인 후 저장했다.

- 실제 에디터 컴파일 완료, 오류 없음. StartRegionSandbox의 GameplayCanvas, GameplayEventSystem, ConstructionPresentation을 확인했다. HUD 텍스트 80개·버튼 59개·카탈로그 25개, 참조 누락·잘못된 버튼 콜백 0개.
- EditMode 1차: 89개 중 88개 통과. HudLayoutTests의 분리된 VisualElement에 보낸 NavigationSubmitEvent가 클릭을 실행하지 못했다. 테스트를 실제 EditorWindow 패널에 연결하고 finally에서 닫도록 수정했다.
- 재컴파일 후 EditMode 89/89 통과, 실패·건너뜀 0개. 두 현재 씬의 Canvas 계층 검사 포함. [결과 JSON](2026-09-28-live-editmode-results.json)
- StartRegionSandbox 실제 Play: VerifyCanvasHud, VerifyBuildAreaHologram, VerifyWorldGridVisibility, VerifyStartLoop 모두 통과. 121셀·9청크·정지/숨김 캐시·청크 3개 재사용, 발전/생산/방어, 편집/일시정지, 야간 소환, 보스 처치와 오브 완성을 검사했다. [Play 결과](2026-09-28-live-play-results.json)
- 검증 중 저장 경로를 `/tmp/eternal-live-play-20260928-a`로 격리했다. 종료 후 환경변수를 이전 값으로 복원했고, Play 종료·원본 씬 열린 상태·dirty=false를 확인했다.
- 종료 전 Console 오류 0개. Editor 저장 후 7개 씬 정적 참조 검사도 재통과했다.
- 사용자 승인에 따른 씬 저장으로 Unity 직렬화 변경이 발생했고, 동적 한글 폰트 에셋도 변경됐다. 이 변경은 유지했으며 아직 커밋하지 않았다.

### 화면 확인과 남은 검증

[실제 Game 화면](2026-09-28-canvas-game-view.png)에서 Canvas·한글·미니맵 표시를 확인했다. **1340×908 캡처에서 왼쪽 자원 목록과 조작 안내가 겹친다. 레이아웃 보완이 필요하다.** CLI screenshot 명령은 카메라만 렌더링하여 Overlay Canvas가 빠지므로, 화면 증거는 Game 탭을 연 뒤 ScreenCapture로 별도 수집했다.

이번 Play 검사는 코드로 명령/게임 동작을 실행한 결과다. 마우스 실입력·다중 해상도·현재 Canvas의 저장/재로드 왕복·수량 입력·스크롤·미니맵 드래그·GC/프레임 성능 측정은 완료하지 않았다. OpenWorldSandbox는 저장 계층 테스트만 실행했으며 Play 검증 대상은 StartRegionSandbox다.

## 아래는 환경 복구 전 실행 시도 기록

## 실행 결과

- Unity 6000.3.14f1을 실제 프로젝트 경로와 전용 로그 경로로 GUI 실행했다. sandbox 밖 실행도 시도했다.
- `unity status`: 연결 가능한 Editor 0개. `pipeline list`: 프로젝트 실행 항목은 있지만 서버 연결 불가, Safe Mode 보고 없음.
- GUI 실행 프로세스는 생겼으나 `/tmp/eternal-deep-editor.log` 및 프로젝트 import 결과는 생성되지 않았다. Unity 앱 UI 조회는 timeout이었다.
- 시스템 확인: `arch -x86_64 /usr/bin/true`가 `Bad CPU type in executable`로 실패. `pkgutil --pkg-info com.apple.pkg.RosettaUpdateAuto`도 설치 기록 없음. CPU는 arm64다. 이전 Unity의 Rosetta 오류가 실제 의존성 누락임을 확인했다.
- Rosetta 2 설치 진행 여부를 사용자에게 요청했다. 설치/약관 확인 전에는 의존성 설치를 진행하지 않았다.

**당시 판정: 실행 환경 차단. Unity import, NUnit, Play, 화면/입력 검사는 실행되지 않았다. 통과 결과가 아니다.**

## 준비한 후속 검사

1. GUI Editor를 정상 실행하고 import·Console·씬 직렬화 오류를 먼저 확인/수정한다.
2. CanvasHierarchyTests, BuildAreaCoverageTests, UpgradeQueryTests 및 건설·가동·회수·전력·저장 EditMode 회귀를 실행하고 실제 결과 XML을 보관한다.
3. Play 진입 전에 ETERNAL_STEAM_SAVE_TEST_ROOT를 별도 임시 경로로 지정한다. 실제 제품 저장을 테스트 데이터로 사용하지 않는다.
4. 신규 세션에서 VerifyCanvasHud, VerifyBuildAreaHologram, VerifyWorldGridVisibility를 실행한다.
5. 별도 신규 세션에서 VerifyStartLoop의 기지·정면 121셀·발전/자원/방어·편집/일시정지·야간 소환·보스/오브를 확인한다.
6. 목표 해상도별 화면, 한글, 포인터 차단, 수량 입력, 카탈로그/메뉴 스크롤, 미니맵 이동, 저장/복원과 강화 회귀를 확인한다.

`Tools/VerifyStartLoop.cs`는 이전 UI Toolkit 전용 HUD 조회가 현재 Canvas 씬에서 실패하는 문제가 있어 Canvas 경로를 추가했다. 기존 검증 씬의 UI Toolkit 경로도 유지했다. Unity 번들 Roslyn으로 관련 검증 도구를 재컴파일했으며 이는 Play 실행을 대체하지 않는다.
