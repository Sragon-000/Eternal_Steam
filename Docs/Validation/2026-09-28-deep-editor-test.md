# 실제 Unity 심층 테스트 실행 시도

2026-09-28. 사용자가 실제 Unity 프로젝트를 열어 심층 테스트를 요청했다.

## 실행 결과

- Unity 6000.3.14f1을 실제 프로젝트 경로와 전용 로그 경로로 GUI 실행했다. sandbox 밖 실행도 시도했다.
- `unity status`: 연결 가능한 Editor 0개. `pipeline list`: 프로젝트 실행 항목은 있지만 서버 연결 불가, Safe Mode 보고 없음.
- GUI 실행 프로세스는 생겼으나 `/tmp/eternal-deep-editor.log` 및 프로젝트 import 결과는 생성되지 않았다. Unity 앱 UI 조회는 timeout이었다.
- 시스템 확인: `arch -x86_64 /usr/bin/true`가 `Bad CPU type in executable`로 실패. `pkgutil --pkg-info com.apple.pkg.RosettaUpdateAuto`도 설치 기록 없음. CPU는 arm64다. 이전 Unity의 Rosetta 오류가 실제 의존성 누락임을 확인했다.
- Rosetta 2 설치 진행 여부를 사용자에게 요청했다. 설치/약관 확인 전에는 의존성 설치를 진행하지 않았다.

**현재 판정: 실행 환경 차단. Unity import, NUnit, Play, 화면/입력 검사는 실행되지 않았다. 통과 결과가 아니다.**

## 준비한 후속 검사

1. GUI Editor를 정상 실행하고 import·Console·씬 직렬화 오류를 먼저 확인/수정한다.
2. CanvasHierarchyTests, BuildAreaCoverageTests, UpgradeQueryTests 및 건설·가동·회수·전력·저장 EditMode 회귀를 실행하고 실제 결과 XML을 보관한다.
3. Play 진입 전에 ETERNAL_STEAM_SAVE_TEST_ROOT를 별도 임시 경로로 지정한다. 실제 제품 저장을 테스트 데이터로 사용하지 않는다.
4. 신규 세션에서 VerifyCanvasHud, VerifyBuildAreaHologram, VerifyWorldGridVisibility를 실행한다.
5. 별도 신규 세션에서 VerifyStartLoop의 기지·정면 121셀·발전/자원/방어·편집/일시정지·야간 소환·보스/오브를 확인한다.
6. 목표 해상도별 화면, 한글, 포인터 차단, 수량 입력, 카탈로그/메뉴 스크롤, 미니맵 이동, 저장/복원과 강화 회귀를 확인한다.

`Tools/VerifyStartLoop.cs`는 이전 UI Toolkit 전용 HUD 조회가 현재 Canvas 씬에서 실패하는 문제가 있어 Canvas 경로를 추가했다. 기존 검증 씬의 UI Toolkit 경로도 유지했다. Unity 번들 Roslyn으로 관련 검증 도구를 재컴파일했으며 이는 Play 실행을 대체하지 않는다.
