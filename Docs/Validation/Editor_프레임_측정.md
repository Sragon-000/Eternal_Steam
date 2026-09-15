# Editor 실제 프레임 측정 — 빌드 없음

2026-09-14. 사용자 요청에 따라 게임 빌드를 실행하지 않고 임시 복제 프로젝트의 Unity Editor에서만 분리 전후를 측정했다. 제품 코드·씬·설정 에셋은 수정하지 않았다. 측정 도구와 결과 문서만 추가했다.

## 조건

- Unity 6000.3.14f1, Apple M4 Pro, 그래픽이 활성화된 batchmode Editor.
- Lane 씬, 카메라 RenderTexture 1024×576, 포탑 4종 각 1개, 기존 시작 위치·방향, 사거리 22.
- 시드 731, 적 4,000마리 상시 유지. 검증 세션의 배열에서만 적 체력 1,000,000,000, 이동 속도 0으로 설정. 적 분포는 X -16~16 / Z -5.5~5.5. 공격 판정·감속·표현은 그대로 실행.
- 정상 `HordeSimulation.Update`와 HUD를 실제 프레임마다 실행. 수동 연속 함수 호출 측정이 아니다. 자동 추가 생성은 중지.
- simulation dt는 `Time.captureDeltaTime=1/60`, vSync 0, targetFrameRate -1. 벽시계 시간을 고정하는 프레임 제한이 아니며 측정 CPU 시간은 Profiler의 실제 값이다.
- 각 버전 3회, 회당 준비 120프레임을 제외하고 연속 600프레임 기록. 매 회차 적·포탑 전투 상태 초기화. 전후 합계 3,600개 측정 프레임.
- 동일 Editor 프로세스에서 분리 후 3회 → 분리 전 3회 순으로 실행. 그 사이 임시 복제본의 HordeSimulation만 이전 소스로 교체하고 Editor 스크립트를 컴파일했다. 배포용 빌드 과정은 없었다.
- 관찰 콜백은 프레임 번호를 확인해 누락 시 실패한다. 모든 회차 CPU·GC 카운터 600개와 적 4,000마리 유지 확인. 샘플 중 CLI 조회는 하지 않았다.

## 게임 프레임 CPU

카운터: `CPU Main Thread Frame Time` (Render), 단위 ns를 ms로 변환. `PlayerLoop`도 같이 기록해 유사한 범위의 값임을 확인했다.

| 회차 | 중앙값 전→후 ms | 변화 | p95 전→후 ms | 변화 |
|---|---|---|---|---|
| 1 | 1.4459 → 1.4818 | +2.48% | 2.1970 → 1.9950 | -9.20% |
| 2 | 1.4400 → 1.4429 | +0.20% | 2.2407 → 2.1487 | -4.11% |
| 3 | 1.4392 → 1.4269 | -0.85% | 2.3009 → 2.1399 | -7.00% |

세 회차에서 반복되는 10% 초과 악화는 관측하지 않았다. p95 감소는 이 Editor 세션의 관측값이며 최적화 효과로 일반화하지 않는다. 버전 실행 순서·OS 스케줄링·Editor 부하가 결과에 영향을 줄 수 있다.

## Editor 포함 메인 스레드

`Main Thread` 카운터를 별도로 기록했다. 게임 프레임 카운터와 혼용하지 않는다.

| 회차 | 중앙값 전→후 ms | p95 전→후 ms |
|---|---|---|
| 1 | 1.6008 → 1.6118 | 2.3975 → 2.1916 |
| 2 | 1.5988 → 1.5962 | 2.4723 → 2.3056 |
| 3 | 1.5963 → 1.5960 | 2.4981 → 2.3216 |

## GC와 GPU

- `GC Allocated In Frame`은 전후 모든 회차에서 중앙값·p95 **352 B/frame**으로 동일했다. 600프레임 전체 합계는 Editor의 간헐적 할당도 포함하며 원시 JSON에 남겼다.
- 이전 제어된 전투 루프의 0 B는 이동·공격·렌더 제출 호출 구간만의 결과다. 이번 352 B는 더 넓은 전체 프레임 범위다. 이번 측정으로 할당의 정확한 발생 지점을 특정하지 않았다.
- GPU Frame Time은 600개 중 18~19개만 양수였다. 나머지 0을 GPU 비용 0으로 해석할 수 없으므로 GPU 중앙값·p95 및 전후 판정은 **유효하지 않음**으로 처리한다.
- 제품 플레이어/Standalone 성능과 화면 출력 GPU 비용은 이 결과로 확정할 수 없다. 사용자 지시에 따라 빌드는 미실시·이번 범위에서 제외했다.

## 도구·기록

- `Tools/MeasureLegacyEditorFrames.cs`: 별도 에셋이나 MonoBehaviour를 생성하지 않고 Editor 콜백과 ProfilerRecorder로 관찰한다. 메모리상의 카메라 타깃·프레임 설정을 종료 시 복원하고 임시 전투는 로비로 초기화한다.
- `legacy-editor-frames-before.json`, `legacy-editor-frames-after.json`: 각 회차의 카운터 중앙값·p95·합계·양수 샘플 개수.
- 분리 전 HordeSimulation SHA-256: `4f0422c39efe9f829116aa4f22bc59079b32864b107f34ffbb057d57379276af`.
- 분리 후 HordeSimulation SHA-256: `3ff829a69aac7ccd2bcb619be000b725fb716540d72c6cb3f59fb7c1ca4b8169`.

```sh
unity command run_script --file Tools/MeasureLegacyEditorFrames.cs --entry MeasureLegacyEditorFrames.Main --project-path <임시복제프로젝트> --format json
```

Play 모드의 HordeDemo 로비에서 실행한다. 명령은 비동기로 측정을 시작하고 반환한다. `Temp/legacy-editor-frames-status.txt`가 completed가 되면 `Temp/legacy-editor-frames.json`을 보존한다. 같은 파일명을 사용하므로 버전을 바꾸기 전에 결과를 복사한다. 사용자 작업 중인 씬 대신 폐기 가능한 복제본에서 실행한다.

카운터 사용 기준: [Unity ProfilerRecorder 문서](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html), [LastValue — 완료된 이전 프레임 값](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.LastValue.html).
