# RX-01/02 공유 역 슬롯·귀환 방어·예약·저장 검증

실행일: 2026-10-02 (Asia/Seoul). 이번 구현 단위는 완료했다. 최종 Unity EditMode **249/249**, 실패 0. 실제 `StartRegionSandbox`의 격리 Play에서 3역/10선로/2노선, 귀환 공격, 예약 선점 실패, 진입 대기와 2회의 실제 씬 재로드를 통과했다. 자연 생산·전체 밤·최종 UI 인수와는 구분한다.

## 변경 식별과 원본

- HEAD: `7e6dabe890603bdd183c4680420fd53986d3498e`. 기존 미커밋/미추적 파일이 많으며 철도 코드도 미추적이었다. HEAD diff만 이번 작업의 diff로 간주하면 안 된다.
- [변경 전 기준](../Measurements/2026-10-02-railway-station-slots/baseline.json): 대상 파일 SHA-256, 원본 사본 `before/`, 기존 Git 상태 `git-status-before.txt`와 선택 파일 `git-diff-before.patch`를 보관했다. `TrainDefenseController.cs`는 첫 수정 직전에 추가 캡처했다.
- [이번 변경만의 manifest](../Measurements/2026-10-02-railway-station-slots/implementation-manifest.json): 현재 해시, 변경 전/후 패치, 신규 파일, 실행 시간과 원본 로그 식별자. 커밋·푸시·배포는 하지 않았다.
- 기존 `2026-10-02-railway-shuttle-editmode.json`의 241/241은 이전 작업의 결과이며 이번 실행으로 재표기하지 않았다.

## 적용한 계약

역마다 내부 정차 슬롯은 1개다. 정지·정차·연료 대기·역 안 오류 상태의 열차가 점유한다. 다른 노선은 자기 선로의 마지막 연결구에서 기다린다. 최종 연결구→역 이동 구간에 진입할 때 슬롯을 예약하고, 실제 출발 시 놓는다. 공용 선로·신호·교행 기능을 추가하지 않았다.

동일 시각 경쟁은 저장되는 노선 목록 순서로 결정한다. 여러 노선을 동일한 사건 시계로 진행시켜 긴 Tick에서 미래의 도착이 다른 노선의 과거 도착보다 먼저 처리되지 않게 했다. 새 노선을 이미 점유된 첫 역에 만들면 자기 출발 연결구에 대기 배치하며 슬롯이 비면 진입한다. 이 상태에서는 급탄·무기 설치/교체/충전/강화를 거부한다. 화물 설정은 가능하다. 정지한 점유 열차가 출발하지 않으면 후속 열차도 계속 기다린다. 이는 자동 출발·자동 급탄을 추가하지 않는 보수적 기본값이다.

`waitingForStation`과 `stationEntryReserved`를 저장한다. 외부 컨테이너는 v3를 유지하고 철도 내부에 `stationSlotVersion=1`을 추가했다. 슬롯 버전이 없는 기존 저장은 목록 순서로 중복 정차 열차를 자기 출발 연결구에 대기시키며 화물·연료·처리 완료 여부를 보존한다. 기존 열차가 이미 진입 구간에 겹쳐 있으면 충돌 열차의 위치만 마지막 연결구로 되돌려 대기시킨다. 이 이관은 순간 위치 조정이 있을 수 있다. 현대 슬롯 저장은 소유권 중복/불가능한 대기 위치를 복원 전 거부한다. 현재 무기·배터리/노선 예약 형식은 그대로 사용한다.

정적 UI/씬/프리팹을 생성하지 않았다. 기존 uGUI 텍스트에 `역 진입 대기`를 표시하고 급탄 버튼 조건을 코어에 맞췄다. 초기 대기 열차의 표시 위치는 자기 연결구다.

## 환경과 fixture

Unity 6000.3.14f1, macOS Editor, Pipeline 0.6.0-exp.1, 기존 500×500 StartRegion 지형. 처음에 PID 12068 Editor는 편집·컴파일 완료 상태였으나 Pipeline descriptor가 없었다. 실행 중인 테스트/컴파일이 없는 것을 확인하고 기존 서버만 메뉴로 정지/시작해 연결을 복구했다. Editor를 종료하지 않았다.

실기 저장 경로는 `/tmp/eternal-station-slots-20261002`(초기 시도)와 `/tmp/eternal-station-slots-20261002-final`(최종 처음부터 재현)이다. 사용자 SingleMap 저장은 대상으로 삼지 않았다. 최종 [환경 복원 증거](../Measurements/2026-10-02-railway-station-slots/editor-final.json): Play=false, compile=false, dirty=false, 원래 씬, `runInBackground=false`, 저장 경로 환경 변수=null.

실기 fixture 조건:

- 게임 시계 일시정지, 자동 저장 비활성, 철도/방어의 정상 Tick을 명시적으로 호출. 자연 생산/야간 소환은 실행하지 않았다.
- 무한 자원 OFF. 철 1,100, 석탄 300을 기지 재고에 fixture로 넣었다. 메인 레벨 2와 배터리 100을 fixture로 설정했다. 재고 용량은 현행 검증용 1,000,000이다.
- 보조 기지 1개는 배치 API로 준비했다. 일반 건설의 검증용 무료 정책은 유지했다. 철도 3역×철10 + 10선로×철1 = **철40**, 무기 장착 **철10**은 정상 결제 API에서 확인했다. 이 결과를 최종 자원 밸런스 근거로 확대하지 않는다.
- 지형 탐색으로 첫 역 셀 (-8,18), 나머지 두 역은 x+6/x+12. 선로는 물리 구간당 5칸, 노선은 A↔B와 C↔B. 경로 난수 없음. Enemy world 기본 seed=731, 공격 표적은 속도0/체력1000 지상 적 1개를 지정 위치에 주입했다.
- 실제 SceneView pose, TrainDefenseController/MobileDefenseRuntime, 재고·결제·SingleMapPersistence, 실제 씬 재로드를 사용했다. OS 물리 마우스 입력이나 최종 스프라이트 화면 평가는 아니다.

## 기대와 실측

| 시나리오 | 기대 | 이번 실측 |
|---|---|---|
| 전체 EditMode | 신규/기존 회귀 실패0 | [249/249, 실패0](../Measurements/2026-10-02-railway-station-slots/editmode-final.json). 왕복 관련 17개(기존9+신규8) |
| A/C의 B 동시 도착 | 슬롯1개, 후속 열차는 역 밖 대기 | 대기 progress=4.166666666666667, 마지막 연결구 위치 오차0 |
| 점유 열차 정지 중 100초 | 후속 화물·연료·기지 재고 불변 | 화물30, 석탄48.6 유지. 정지 요청 유지 |
| 대기 저장→실제 씬 재로드→슬롯 해제 | 모든 저장 상태 일치, 1회 하역 | savedUtc만 제외한 전체 snapshot 일치. 철30 하역, 석탄48.6→48.6 |
| 귀환 중 실제 공격(40×0.05초) | 표적 피해, 배터리 감소, 현재 기차 pose 추종 | `defense.arc` 피해150, 배터리100→89.999999850988388. 위치 오차0, 차체 방향 내적 최솟값0.99999994 |
| 귀환 예약+무기 동시 저장 | 방향/진행/예약/무기·배터리·재고 일치 | 실제 재로드 후 전체 snapshot 일치. 전투 중 저장 시도는 거부 |
| 예약 선로를 다른 노선이 확정 | 첫 역 귀환 시 교체 실패, 원래 노선 보존 | pending.active/failed=true, revision0, 기존2역/1구간 보존 |
| 큰 Tick 대 작은 Tick | 도착 순서/대기/연료/진행 동일 | 코어 80초 1회 대 0.1초×800, 허용오차1e-8 내 일치 |
| 진입 예약 중 재로드·역 상실 | 슬롯 예약 유지, 연료 재차 차감 없음 | 코어 회귀 통과. 불량 슬롯 저장은 live 상태 변경 전 거부 |
| 슬롯 없는 기존 중복 정차 저장 | 순서대로 슬롯/대기 배치, 재고 불변 | 합성 코어 저장 이관 통과. 기존 v1/v2 회귀도 전체 테스트에 포함 |

실측 JSON: [최종 measurements](../Measurements/2026-10-02-railway-station-slots/play-final-measurements.json). 단계별 저장 원본: [귀환](../Measurements/2026-10-02-railway-station-slots/return-snapshot.json), [대기](../Measurements/2026-10-02-railway-station-slots/waiting-snapshot.json), [진입 후](../Measurements/2026-10-02-railway-station-slots/admitted-snapshot.json). 이전 소스에는 슬롯/대기 상태가 없었음은 baseline으로 확인했다. 수정 전 동일 실기 시나리오를 실행해 충돌 수를 측정한 것은 아니므로 개선 전 충돌 건수/성능 수치를 만들지 않았다.

## 실패·수정·미실행

1. 첫 전체 회귀는 247개 중 246통과/1실패였다([원문](../Measurements/2026-10-02-railway-station-slots/editmode-all-1.json)). 기존 예약 선점 테스트가 첫 역을 경쟁 열차로 점유한 채 다른 열차가 돌아오기를 기대했다. 새 슬롯 규칙에서는 대기가 맞다. 경쟁 열차를 후보 노선의 다른 역에서 생성하도록 바꿔 예약 선점 검증 목적을 유지했다. 이후 247/247, 추가 경계 회귀 후 최종249/249.
2. 초기 Play 도구는 레벨1의 2×2 무기를 기대해 두 번 실패했다(`play-build-1.json`, `play-arm-2.json`). 실제 카탈로그 확인 후 fixture 메인 레벨을2로 명시해 아크 블래스터를 장착했다. 제품 해금/무기 크기를 변경하지 않았다. 최종 도구의 새 저장 경로에서 처음부터 다시 실행해 Build→귀환 재로드→대기 재로드 모두 통과했다.
3. 비공개 Reload 직접 호출은 컴파일 실패했다(`reload-return-command.json`). 공개 `ContinueSaved()`로 실제 씬 재로드를 실행했다. 위 실패는 검증 도구 실패이며 제품 회귀 실패와 구분한다.
4. 저장소 전체 `git diff --check`는 기존 거대 씬 YAML의 trailing whitespace로 실패했다. 이번 변경 경로를 한정한 검사는 통과했다. 기존 씬을 포맷/정리하지 않았다.
5. 미실행: 자연 생산부터 전체 야간 방어까지, 9종 기차 방어 전수, 최종 UI/물리 클릭/해상도, OpenWorldSandbox 실기, 플랫폼 빌드, 많은 노선의 이벤트 시계 성능, 모든 과거 사용자 저장/구형 v1/v2의 새로운 실기 재실행. 기존 241/241·구형 Play 증거를 이번 실행으로 합산하지 않았다.

잔여 위험: 다수 노선 이벤트 처리의 규모 성능은 측정 전이다. 역 내부 모델은 열차 기준점/슬롯이며 차체 길이의 물리 충돌 전수 검증은 아니다. 오래된 겹침 저장 이관에는 연결구로 순간 위치 조정이 있다. 정지 열차가 슬롯을 계속 막는 정책에 대한 최종 화면 안내/사용성은 RX-04~07에서 확인한다. 상하좌우 인접 추적의 평행 선로 오분기와 명시적 연결 정보/새 선로 건설 보조는 RX-03 잔여다.

## 재현

실제 Unity 저장소에서 직접 `unity`를 실행한다. 먼저 다른 작업의 Editor 사용 여부와 `isPlaying/isCompiling/isDirty`를 확인한다. 씬은 StartRegionSandbox의 저장된 편집 상태여야 한다. 도구의 격리 경로가 이미 존재하면 삭제/재사용하지 말고 새 고유 경로로 바꾼 뒤 그 변경을 기록한다.

```sh
unity command recompile --format json
unity command recompile_status --format json
unity command run_tests --mode editor --filter EternalSteam --filter_type assembly --async_tests true --format json
unity command test_status --format json
```

`eval`로 `ETERNAL_STEAM_SAVE_TEST_ROOT`를 도구에 명시한 새 경로로 설정하고 원래 `Application.runInBackground` 값을 보관한 뒤 필요 시 true로 설정한다. Play에 진입한 다음:

```sh
unity command editor_play --format json
unity command run_script --file Tools/VerifyRailwayStationSlotsPlay.cs --entry VerifyRailwayStationSlotsPlay.Build --format json
unity command eval 'UnityEngine.Object.FindFirstObjectByType<EternalSteam.OpenWorld.OpenWorldSandbox>().Persistence.ContinueSaved();' --format json
unity command run_script --file Tools/VerifyRailwayStationSlotsPlay.cs --entry VerifyRailwayStationSlotsPlay.ReloadReturnAndShare --format json
unity command eval 'UnityEngine.Object.FindFirstObjectByType<EternalSteam.OpenWorld.OpenWorldSandbox>().Persistence.ContinueSaved();' --format json
unity command run_script --file Tools/VerifyRailwayStationSlotsPlay.cs --entry VerifyRailwayStationSlotsPlay.ReloadWaiting --format json
unity command editor_stop --format json
```

각 결과의 외부 `success`뿐 아니라 내부 `data.result.success`와 예외를 확인한다. 단계 사이 씬 로드 완료 후 다음 도구를 실행한다. 마지막에 저장 경로 환경 변수와 background 값을 원복하고 편집 씬 dirty=false인지 기록한다. 원본 실행 로그는 Measurements 폴더의 `play-final-*.json`에 있다.
