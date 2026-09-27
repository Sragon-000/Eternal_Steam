# HUD 시안 적용 기반 코드 준비

2026-09-27. 원본 프로젝트 `Eternal_Steam`에 [적용 준비](../BELTFED_UI_실제적용_준비.md)의 P1 선행 기능을 구현했다. **기반 코드와 기존 HUD 연결 완료, Unity 실행 검증 대기**다. 새 시안의 전체 레이아웃과 홀로그램 적용 완료를 뜻하지 않는다.

## 실제 변경

- `IUpgradeControl.CanUpgrade(out reason)`을 추가했다. 일반 강화, 성능 강화, 캠페인 공유 메인 강화가 상태 변경 없이 활성/파괴·최대 레벨·각 구현의 기존 제한을 확인한다. `TryUpgrade`도 같은 조회를 거친다.
- `ResourceBank.CanPurchase`와 실제 구매가 비용 유효성, 동일 자원 비용 합산, 잔액 판정을 공유한다. 조회는 commit 콜백이나 차감을 실행하지 않는다.
- `UpgradePurchase.CanUpgrade`는 모듈·비용 정책·잔액을 확인한다. 실제 구매는 최신 상태를 다시 확인한다. 비용 정책 없음/비용표 미설정은 무료로 처리하지 않는다. 명시적 `VerificationFreeUpgrade`만 검증용 무료로 표시한다. 무료 조회 경로는 프레임마다 비용 목록을 생성하지 않는다.
- `OpenWorldHudActions`를 추가해 강화 상태/명령과 진행 버튼의 상태/명령을 HUD에서 분리했다. 화면 차단(편집/패배/복원 오류)은 이 OpenWorld 조합 계층에서 처리한다. 기능 어셈블리에 UI나 Sandbox 의존성을 추가하지 않았다.
- 기존 HUD에 `upgrade-status` Label을 추가했다. 강화 가능하면 현재→다음 레벨과 비용 정책을 표시하고, 불가능하면 버튼을 끄고 이유를 표시한다. 성공 시 기존 자동 저장 요청을 한 번 호출한다. 저장 시스템 없는 옛 씬은 명시적인 검증용 무료 구매 경로를 사용한다.
- 시작 씬의 `run`과 `clock-pause`가 같은 진행 전환을 사용한다. Assault 없는 검증 씬의 전투 실행은 별도 분기로 보존한다. 시간 표시 어댑터는 버튼 상태를 덮어쓰지 않는다. 시간 정지/재개가 건물 선택을 해제하지 않으며 편집 중에는 작업 확정/취소를 먼저 요구한다.
- C# 파일 두 개에 고유 `.meta`를 추가했다. 씬, 프리팹, 콘텐츠 설정, 저장 포맷과 제품 저장 파일은 수정하지 않았다.

## 확인 결과와 한계

| 확인 | 결과 |
|---|---|
| 변경한 5개 어셈블리의 C# 컴파일 | PASS: Runtime → Progression/Economy → OpenWorldSandbox, EditModeTests |
| 컴파일 방식 | 설치된 Unity 6000.3.14f1의 DotNetSdkRoslyn/csc.dll과 NetCoreRuntime 사용. 기존 Bee 응답 파일을 `/tmp/eternal-hud-compile`로 복사하고 새 소스를 추가한 뒤, 변경 어셈블리 참조를 새 출력으로 교체해 컴파일. Library의 기존 출력은 덮어쓰지 않음. |
| 컴파일 경고 | 기존 OpenWorldSandbox.renderer/MinimapElement.Focus의 숨김 경고, SpawnQuantityInput.PreventDefault의 obsolete 경고. 새 코드의 컴파일 오류 없음. |
| UXML 구조·조회 | 이름 있는 요소 59개 중복 없음, 표시 어댑터 7개의 리터럴 Q 참조가 모두 존재. Unity import나 실제 렌더링 검사는 아님. |
| NUnit 테스트 코드 | `UpgradeQueryTests` 9개 케이스 작성 및 컴파일 완료. 실행 결과는 없음. |
| Unity 테스트 실행 | BLOCKED: Editor가 Rosetta 2 미설치를 알리며 테스트 시작 전에 종료. 샌드박스 밖 재실행에서도 동일. |
| CLI 반환값 교차 확인 | CLI는 success/exit 0을 반환했지만 `/tmp/eternal-hud-preparation-editmode.xml`이 생성되지 않음. PASS로 기록하지 않음. |
| Play 화면·클릭·저장/진행 회귀 | 미실행. |
| 게임 빌드 | 미실행. |

`UpgradeQueryTests`는 반복 조회의 레벨·체력·캠페인·재고 무변경, 세 강화 구현의 비활성/파괴/최대 레벨, 조회 후 메인 레벨 제한·잔액 변경, 중복 자원 비용 합산, 비용 미설정/NaN/중복 엔트리/합계 overflow, 실패 결제 무차감, 성공 한 번의 레벨 증가와 차감을 다룬다.

## Editor 실행 환경 복구 후 이어갈 검사

먼저 다음 EditMode 명령을 실행하고 결과 XML의 실제 테스트 수/실패 수를 확인한다. CLI의 성공 반환만으로 완료 처리하지 않는다.

```sh
unity test /Users/limseth/Projects/Eternal_Steam/Eternal_Steam \
  --mode EditMode \
  --filter 'EternalSteam.Tests.UpgradeQueryTests;EternalSteam.Tests.ArchitectureTests' \
  --output /tmp/eternal-hud-preparation-editmode.xml \
  --timeout 240 --format json \
  -- -logFile /tmp/eternal-hud-preparation-editor.log
```

다음은 별도 임시 저장 루트의 StartRegionSandbox에서 확인한다. [저장 검증 지침](2026-09-24-single-map-save.md)을 따른다.

1. 비선택, 최대 레벨, 메인 레벨 제한, 편집, 파괴, 복원 오류에서 강화 버튼과 이유가 일치하는지 확인.
2. 가능 상태의 클릭 한 번에 레벨과 자동 저장 요청이 한 번만 반영되는지 확인. `VerifySingleMapSave`의 유료 정책 주입/미설정 비용 회귀도 실행.
3. 두 진행 버튼에서 낮/밤 정지·재개와 편집 차단 확인. Assault 없는 옛 검증 씬은 전투 실행과 시간 정지를 별도로 확인.
4. `VerifyHudFolds`, `VerifyMinimap`, `VerifyQuantityFocus`, 독립 신규 세션의 `VerifyStartLoop`로 기존 입력·시간 동결 회귀 확인.

이 결과가 확보된 뒤 P2의 미니맵·선택 상세·플레이 메뉴 재배치와 P3의 가동 영역 홀로그램을 진행한다. 준비 기준 JSON은 변경 전 스냅샷으로 보존하며 현재 파일과 해시가 달라지는 것이 정상이다.
