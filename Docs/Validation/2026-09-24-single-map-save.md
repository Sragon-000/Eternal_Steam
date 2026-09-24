# 단일 맵 저장·패배 검증 — 2026-09-24

원본 프로젝트 Unity 6000.3.14f1의 StartRegionSandbox에서 CLI run_script와 실제 씬 재로딩, Play 종료/재진입으로 검증했다. 게임 빌드와 Unity 프로세스 종료/재실행, 강제 전원 차단 실험은 하지 않았다. 테스트 저장 경로는 `/tmp/eternal-save-check-0924`, 최종 구성은 `/tmp/eternal-save-final-0924`이며 제품 저장 경로와 분리했다.

## 통과 항목

`Tools/VerifySingleMapSave.cs`의 진입점별 결과:

| 진입점 | 확인한 내용 |
|---|---|
| Main | 초기 메인+발전기+자원 생산+신규 포탑 저장, 중복 ID 거절, 편집/밤 저장 거절, 정상 파일/손상 파일 백업 복구, 패배한 백업 차단, 새 슬롯 전환 |
| Expanded | 토대와 기존 포탑 4종, 강화·손상 체력·공격 쿨다운/발사 상태 저장, 자원 부족 시 부분 차감 없음 |
| Gates | 누락 콘텐츠·점유 중복·잘못된 소속/전력 참조를 장면 변경 없이 거절, 미지원 버전 보존/차단, 일시정지 중에도 적/개발자 소환 대기 거절 |
| Reloaded | 실제 씬 재로딩 후 저장 시각을 제외한 전체 스냅샷 동일, 로드 후 정지 상태. Expanded 및 보스/오브 완료 스냅샷에도 수행 |
| Defeat | 실제 메인 기지에 치명 피해, 파괴 이벤트에서 즉시 파일 기록, 저장/이어하기 차단 |
| FailedReload / Fresh | 재로딩 시 메인 자동 설치 없이 패배 차단, 새 게임에서 메인 1개·초기 레벨·토대 없음 |
| Transients | 실행 목록에 시험 투사체/연쇄 상태를 넣어 저장 거절 확인, 검사 후 제거 |
| FailuresAndCosts | 주입한 파일 쓰기 실패가 저장 성공/시각으로 표시되지 않음, 실제 UpgradePurchase에서 부족 시 변화 없음·성공 시 한 번 차감/강화·비용 누락 오류 |
| Ui | 기존 작성된 UXML 내 저장/이어하기/새 게임 조작 및 검증용 무료 표시 |
| Autosave | 변경 상태 29초에는 저장 없음, 30초에 저장, 변경 없으면 생략, 편집 중 보류·안전한 명령 시 저장 |
| OrbSave | 실제 보스/오브 흐름 완료 후 낮의 안전한 시험 상태를 만들고 진행·야간 예산·난수 포함 저장/복원 |

추가 회귀:

- `VerifyStartLoop.Main`: 고정 모델 1개, 정면 정확한 121칸, 발전·자원 생산·방어, 편집/정지/재개, 야간 적 진입, 보스 처치와 완벽 오브 제작 통과.
- `VerifyBasePowerIntegration.Main`: 실제 발전기/포탑 설치, 생산, 회수 취소/확정, 전력 부족 시 비파괴 정지, 보스 예약 영역 통과.
- `VerifyHudFolds.Main`: 실제 버튼 콜백으로 상단 3개 패널 접기/펼치기, 하단 접기 중 임시 배치 유지, 취소 후 예약 해제 통과.
- 최종 C# 컴파일 성공. 기존 데모 PlayerPrefs 저장 코드를 이번 저장 경로에서 호출하지 않는 것을 정적으로 확인했다. 기존 데모 3개 맵 전체 플레이는 이번 검증에서 다시 수행하지 않았다.

## 발견·수정

- Unity JSON의 빈 문자열/널 차이 때문에 기지가 아닌 건물의 BaseId를 중복으로 잘못 판정하던 문제 수정.
- 배치 생성의 방향 설정 사건이 복원한 회전 방향을 초기화하던 문제 수정. 생성 후 실행 상태를 적용하고 다시 왕복 비교 통과.
- 저장 건물 활성화 시 용량이 중복 증가할 가능성을 제거하고 최종 저장 자원 상태 적용.
- 부동소수점 직렬화를 R 형식으로 변경해 반복 저장 시 타이머 정밀도 손실 방지.
- 새 게임 전환 중단 시 이전 슬롯을 재사용하지 않도록 전환 표식 추가.

## 재현

Editor의 시작 씬을 열고 Play 전에 `System.Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT", "/tmp/고유한-검증-경로")`를 설정한다. 제품 저장으로 이 도구를 실행하지 않는다.

Play 진입 후 `unity command run_script --file Tools/VerifySingleMapSave.cs --entry VerifySingleMapSave.Main --project-path <프로젝트> --format json` 형태로 호출한다.

권장 순서: Main → Expanded → Gates(씬 재로드 요청 포함) → Reloaded → Transients → Ui → Defeat → FailedReload → Fresh. 새 초기 상태에서 VerifyStartLoop → OrbSave → Reloaded로 보스/오브 분기를 확인한다. 각 재로드 요청 후 다음 Editor 응답에서 후속 진입점을 호출한다. Autosave와 FailuresAndCosts는 안전한 낮·적 없는 상태에서 실행한다.

테스트 종료 시 Play를 끄고 환경 변수를 null로 복원한다. 파일 손상은 테스트 저장 폴더에서만 주입한다. 이 기록은 정확성 검증이며 CPU/GC 성능 측정 결과는 아니다.
