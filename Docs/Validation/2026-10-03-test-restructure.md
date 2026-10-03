# 전체 테스트 개편 검증

현재 씬을 바꾸지 않는 검사 체계로 전환했다. 기준은 [테스트 운영 문서](../Testing/README.md)이며 [항목 인벤토리](../Testing/inventory.json)에 이전 303개와 현재 285개의 이름·결과·증거가 있다.

## 실제 변경

11개 EditMode 씬 검사에서 StartRegion/OpenWorld 중복 매개변수 18개를 통합했다. 모든 기능 조건과 각기 다른 해상도 조건은 유지한다. Preview Scene 생성도 없애고 현재 씬 오브젝트를 검사한다. 일시적인 레이아웃 변경은 Undo로 복원하며 메뉴/이징 내부 값도 저장·복원한다. 테스트가 씬을 저장하는 일은 없다.

Unity 기본 실행기가 임시 씬으로 이동하는 것을 발견해 기본 경로에서 제외했다. 메인 스레드에서 Unity Test Framework의 검사 엔진만 실행하는 전용 실행기를 만들었다. 초기 일반 NUnit 엔진 시도는 백그라운드 생성 문제로 실패했고, Unity 컨텍스트·메인 스레드 생성·원복 검증을 보완했다. 이전 실패 XML은 `attempts`에 보관했으며 최종 성공으로 덮어 해석하지 않는다.

씬 전환 도구 25개는 실행 불가능한 `.cs.txt` 원문으로 보관했다. 원문 SHA-256 일치와 활성 테스트 소스의 씬 로드 호출 부재를 `Tools/Testing/update_inventory.py`로 확인한다. Foundation/Module의 구형 UI PlayMode 4개는 optional/Explicit로 남기고 씬 자동 로딩을 제거했다.

## 최종 결과

- 현재 씬: `Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity`.
- 전체 동기 회귀 **285/285 통과**: 현재 도메인 253, 공통 레거시 건물 10, 전투 14, 배치 8.
- 같은 씬의 UI 및 관련 계산 재실행 **35/35 통과**. 씬 핸들 유지, dirty=false, 저장된 씬 파일 바이트 동일, 오류 로그 0.
- Play 스모크 통과: 건설 그룹 전환/이징, Esc 시뮬레이션·Tab 차단, 전투 복귀, 150×150 미니맵, 무한 자원 OFF, 테스트 컨트롤 유지.
- 전투·건설·Esc 캡처 3장 저장. 건설 화면을 직접 확인했으며 하단 독과 양측 패널이 화면 안에 있다.
- 이 씬에는 캠페인 저장/시작 기지가 없으므로 실제 캠페인 생존 회차는 **NOT APPLICABLE**. 저장·철도·경제 자연 진행까지 이번 스모크가 증명한 것으로 확대하지 않는다.

근거: `Docs/Measurements/2026-10-03-test-restructure/all-summary.json`, 어셈블리 XML, `CurrentScene-summary.json`, `play/result.json`과 PNG. 종료 시 Play 해제, 현재 씬 유지, 원래 실행 설정 복원. 정상 플레이 감사/수집은 별도 후속 작업이다.
