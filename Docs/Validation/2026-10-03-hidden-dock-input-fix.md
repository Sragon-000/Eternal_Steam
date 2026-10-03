# 숨은 건설 독이 전투 입력을 막는 문제 수정

콘텐츠 정상 회차 종료 중 오브 제작이 거부됐다. 실제 상태는 에너지1,000,000, AwaitingCraft, 적0, 편집false, 메뉴false, CanInteract=true였지만 숨은 건설 독의 Transitioning이 true로 남았다. [실제 UI 잠금 증거](../Measurements/Balance/20261003-content-coverage-02/craft-ui-gate-state.json).

`CompactConstructionDock`는 숨겨지면 레이아웃 갱신을 중지한다. 초기/숨은 상태의 `Apply(0)`에서 시작된 전환이 그대로 남을 수 있는데 `CanvasWorldHud`가 이 값을 전역 명령·카메라 키보드·포인터 차단에 사용했다. 정상 건설 그룹 전환 후 오브 제작이 성공한 기록도 보존했다.

## 적용

`CompactConstructionDock.BlocksInput`은 전환 중이며 건설 그룹이 실제 활성화되어 있을 때만 true다. HUD 명령/카메라 입력은 이 속성을 사용한다. 숨은 레이아웃을 매 프레임 다시 계산하지 않고, 보이는 독의 전환 중 클릭 차단을 유지한다. 씬 계층·스프라이트·앵커·테스트 UI는 변경하지 않았다.

## 검증

- 컴파일 오류0. 현재 씬 전체 **297/297** 통과(기존296개+숨은/보이는 독 잠금 회귀1개). 씬 핸들·파일 바이트·dirty 보존.
- 같은 씬의 새 Play에서14개 실제 점검 통과: 초기 숨은 독의 전환값이 true인 조건에서 카메라 키보드 차단 해제, 실제 HUD pause 왕복, 숨은 편집 취소, 보이는 전환 중 cancel 차단, 전환 완료 후 cancel 허용, 전투 그룹 복귀. [실제 결과](../Measurements/2026-10-03-hidden-dock-input-fix/play-result.json).
- 테스트 후 Play 종료·같은 씬·dirty=false·저장 재정의 없음·원래 백그라운드 설정 복구. [복구](../Measurements/2026-10-03-hidden-dock-input-fix/play-restore-command.json).
- [전체 회귀](../Measurements/2026-10-03-hidden-dock-input-fix/all-summary.json), [테스트 인벤토리](../Testing/inventory.json), [수정 전 소스/해시](../Measurements/2026-10-03-hidden-dock-input-fix/before-manifest.json).

실제 QA는 정상 생존 기록과 분리했다. 입력 경로 수정은 경제·무기·적 시뮬레이션 수치를 바꾸지 않는다.
