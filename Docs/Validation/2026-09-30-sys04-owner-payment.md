# SYS-04 중간 검증: 일반 강화의 소속 기지 결제

현재 `UpgradePurchase`는 건물별 재고 선택 함수를 받는다. `OpenWorldContent.PaymentBankFor`는 메인 기지는 자신의 ID, 일반 건물은 설치 때 부여된 `OwnerBaseId`의 활성 기지 재고를 반환한다. 선택 중인 기지를 변경해도 이미 설치한 건물의 지불처는 바뀌지 않는다. 기지 접근이 끊기면 유료 강화 조회와 실행을 거부하며 재고·레벨은 유지한다. 씬의 `OpenWorldSandbox.UpgradeCosts`에 `UpgradeCostTable`을 지정해야 유료 정책이 활성화된다. 현재 두 씬에는 최종 비용표가 없어 기존 `VerificationFreeUpgrade`를 유지한다. 비용 항목이 누락된 유료 표는 자동 무료로 처리하지 않는다.

검증 범위: 캐시된 Unity 응답 파일로 Runtime/Progression/Economy/Railway/OpenWorldSandbox/EditModeTests 6개 어셈블리 컴파일 성공. 관리 코드 합성 강화 명령 13/13 통과. 새 사례는 (1) 소속 기지 15, 다른 기지 25의 재고에서 비용 10을 소속 기지에만 청구, (2) 접근 불가 기지의 강화 거부와 재고·레벨 보존이다. `UpgradeQueryTests`에도 소속 기지 분리와 조회 후 기지 상실/재고 변경 사례를 추가했다. 이후 이미 실행 중인 `StartRegionSandbox` Editor의 Pipeline 서버를 재시작해 연결했고, 강화 EditMode 13/13 및 원자성 수정 후 [전체 219/219](2026-09-30-sys04-editmode.json)이 통과했다.

`ResourceBank.TryPurchase`와 `ConstructionPurchase.TryCommit`은 비용을 먼저 예약하고 확정한다. 확정이 실패하거나 예외가 발생하면 지불 재고·용량·무한 자원 상태를 복원한다. 설치 도중 재고가 소모되어 성공 후 비용이 부분 차감되는 경로를 막았다. 새 EditMode 사례는 두 기지 예약 중 설치 실패와 중간 재고·용량 변화 후 정확한 원상 복원을 확인한다. 월드 설치 자체의 롤백은 기존 `ConfirmTogether`/`ConfirmPlacement` 책임이다.

실제 시작 씬의 격리 Play에서 `MainBase`의 ID/소속 ID가 같고 `PaymentBankFor`가 메인 기지 재고를 반환함을 확인했다. 임시 메모리 비용표로 Lv.2 철 10을 설정했을 때 재고 0에서는 견적 거부, 소속 기지에 철 10 입금 후 조회 허용·강화 성공·Lv.2·소속 철 0·공용 철 0을 확인했다. 선결제 변경 후 같은 Play 시나리오를 다시 통과했다.

저장된 `CanvasWorldHud`의 강화 표시도 임시 비용표로 확인했다. 메인 기지를 선택한 상태에서 철 5는 `철 10/보유 5 · 부족`·버튼 비활성, 철 10은 `철 10/보유 10`·버튼 활성으로 바뀌었다. 같은 구매 서비스의 실행은 Lv.2·철 0이었다. 이는 선택과 `Refresh`를 명령으로 호출한 검사이며 물리 마우스 클릭은 아니다. 비용표 자산이나 씬은 저장하지 않았다. 자동 저장을 끄고 `/tmp/eternal-sys04-ui-play.uTiy05`에 격리했으며 종료 후 환경변수를 해제했다. 최종 상태는 원래 `StartRegionSandbox` 씬의 Edit Mode, dirty=false, 저장 경로 미설정이다.

검증 한계: 실제 씬에 최종 유료 비용표를 영구 연결한 물리 클릭·저장/재로드는 미검증이다. 최종 비용 수치와 일반 건설·지역 생산·종류별 한도는 원본 문서에 확정표가 없으므로 아직 적용하지 않았다.
