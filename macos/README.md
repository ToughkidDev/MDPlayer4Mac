# MDPlayer4Mac 포팅 노트

Windows용 MDPlayer의 재생 엔진과 화면 구성을 macOS로 옮기는 작업이다. 원본
Windows/WinForms 코드는 `MDPlayer/`에 보존하고, macOS 구현은 이 `macos/` 아래의
`net8.0` 프로젝트로 분리한다.

마지막 갱신: 2026-08-22

## 현재 제공 기능

- CoreAudio Audio Queue Services를 이용한 실시간 재생
- Windows 원본의 스프라이트를 이용한 전송 버튼과 칩 채널 표시계
- VGM/VGZ, XGM/XGZ, SID, MND, ZMS/ZMD, MDX/MDR, NSF, GBS, HES, S98, AY, ZGM 로드
- VGM에 기록된 칩 구성과 복수 칩 인스턴스를 바탕으로 한 채널 뷰
- 칩·서브컴포넌트별 볼륨 믹서 및 마스터 볼륨, 원곡 기본값으로 볼륨 초기화
- 채널 뷰 / 볼륨 뷰 / 재생목록 전용 뷰 전환
- 다중 파일 재생목록, 드래그 앤 드롭 추가·교체, 다중 선택, Cmd+A, Delete/Backspace 제거
- 다음/이전 곡, 곡 종료 후 다음 곡 자동 재생, 반복 재생, 재생 속도 조절
- Play 버튼 2초 롱프레스 또는 macOS Force Click으로 자동 재생 토글
- Play 클릭 시 Fast/Slow로 바꾼 재생 속도를 1x로 복귀
- 채널 뷰 50% 축소/복원
- 태그 푸시 기반 GitHub Release 자동 생성 (`osx-arm64`)

## 프로젝트 구성

| 프로젝트 | 역할 |
| --- | --- |
| `MDSound/` | 칩 에뮬레이션 라이브러리 |
| `MDPlayerCore/` | 포맷 감지, 드라이버 초기화, MDSound 배선, 믹서 상태 |
| `CoreAudioOutput/` | macOS AudioToolbox/Audio Queue 출력 래퍼 |
| `MDPlayerUI/` | Avalonia 기반 플레이어 UI와 채널·볼륨 시각화 |
| `LivePlayer/` | GUI 없이 실제 오디오 출력을 확인하는 콘솔 플레이어 |
| `EngineSmokeTest/` | VGM을 WAV로 렌더링하는 회귀 테스트 도구 |

## 지원 포맷

UI에서 선택하거나 드롭할 수 있는 확장자는 다음과 같다.

```text
.vgm  .vgz  .xgm  .xgz  .sid  .mnd  .zms  .zmd
.mdx  .mdr  .nsf  .gbs  .hes  .s98  .ay  .zgm
```

- 형식을 판별할 수 있는 포맷은 헤더를 먼저 확인하고, 그 외에는 파일 확장자를 사용한다.
- `.vgz`와 `.xgz`는 각각 gzip 압축 VGM/XGM 입력으로 처리한다.
- MDX/MDR은 X68000 MXDRV 계열이다. 같은 디렉터리의 PDX를 참조하는 곡은 PDX를 찾아
  PCM8/ADPCM 재생에 사용한다. PDX를 찾지 못해도 OPM(YM2151) 측 재생은 계속 시도한다.
- 모든 파일이 해당 포맷의 모든 칩·서브드라이버 조합을 보장하지는 않는다. 로드에 실패하면
  UI 상태줄에 지원하지 않는 포맷 또는 칩이라는 오류가 표시된다.

## UI 동작 요약

### 뷰와 재생목록

- 파일을 처음 로드하면 채널 뷰가 열린다.
- 채널/볼륨 뷰에서는 아래에 약 3.5행 높이의 재생목록을 표시한다.
- 재생목록 버튼은 20행 높이의 재생목록 전용 뷰로 전환한다. 20곡을 넘는 목록은 내부
  스크롤로 탐색하며 창 높이를 더 키우지 않는다.
- 재생목록 영역에 파일을 놓으면 현재 목록 뒤에 추가하고, 상단 플레이어 대시보드에 놓으면
  현재 목록을 교체한다.
- 재생목록의 Cmd+A, Shift/Cmd 선택, Delete/Backspace, Escape 선택 해제를 지원한다.

### 전송 버튼

- Stop은 현재 뷰와 채널 뷰를 닫지 않는다.
- 곡이 끝나면 재생목록의 다음 곡을 자동으로 재생한다. 반복 버튼이 켜져 있으면 현재 곡의
  드라이버 루프 설정을 사용한다.
- Slow/Fast는 현재 재생 세션의 속도를 0.25x~4x 범위에서 바꾼다. Play를 짧게 누르면
  재생·재개와 함께 1x로 돌아가며, 복귀한 경우 상태줄에 `재생 속도 1x`가 보인다.
- Play를 2초 이상 누르거나 Force Click하면 자동 재생을 토글한다. 자동 재생 상태에서는
  Play 아이콘이 빨간색으로 표시된다. 재생목록이 비어 있어 Play가 흐리게 표시되는 경우에도
  이 롱프레스/Force Click 동작은 사용할 수 있다.

### 볼륨 뷰

- 실제 사용 중인 칩 인스턴스만 표시한다. 동일 칩이 두 개면 각각 별도 슬라이더를 표시한다.
- FM/SSG/ADPCM/PCM처럼 독립된 출력 경로를 가진 칩은 가능한 범위에서 서브컴포넌트별로
  분리한다. 마스터 볼륨도 함께 제공한다.
- Reset Volume은 파일에 보존된 기본 볼륨을 복구하며, 정보가 없는 포맷은 0 dB를 기본값으로
  사용한다.

## 개발 환경

- macOS, Xcode Command Line Tools (`xcrun clang` 포함)
- .NET SDK 8 이상
- 인터넷 연결: NuGet 패키지 복원 최초 실행 시 필요

`MDPlayerUI`는 Avalonia 12.1.1을 사용한다. macOS Force Click은 Avalonia 일반 포인터
이벤트에 전달되지 않으므로, 빌드할 때 `MDPlayerUI/Native/ForceTouchMonitor.m`을 작은
Apple Silicon용 dylib로 컴파일해 함께 배포한다.

## 빌드와 실행

저장소 루트에서 실행한다.

```bash
dotnet build macos/MDPlayerUI/MDPlayerUI.csproj -c Release
dotnet run --project macos/MDPlayerUI/MDPlayerUI.csproj -c Release
```

GUI 없이 오디오 엔진을 확인하려면:

```bash
dotnet run --project macos/LivePlayer/LivePlayer.csproj -c Release -- /path/to/song.vgm
```

VGM을 WAV로 렌더링하는 스모크 테스트:

```bash
dotnet run --project macos/EngineSmokeTest/EngineSmokeTest.csproj -c Release -- /path/to/song.vgm /path/to/output.wav
```

## 검증 기준

변경 범위에 맞춰 아래 중 필요한 항목을 실행한다.

```bash
dotnet build macos/MDPlayerCore/MDPlayerCore.csproj -c Release
dotnet build macos/MDPlayerUI/MDPlayerUI.csproj -c Release
```

UI 변경은 빌드 뒤 실제 macOS에서 다음을 확인한다.

1. 파일 로드 후 채널 뷰와 재생목록의 창 크기·모드 전환
2. 재생/정지/일시정지/곡 종료/다음 곡 전환
3. 재생목록 키보드 조작과 드래그 앤 드롭
4. 채널·볼륨·재생목록 전용 뷰의 최소 창 크기
5. Play 롱프레스와 Force Click 자동 재생 토글

현재 `Z80dotNet 1.0.6`은 .NET Framework 대상 패키지라 `NU1701` 경고가 남는다. 현재
빌드와 재생에는 사용 가능하지만, 장기적으로 net8.0/netstandard 호환 패키지로 교체하는
것이 바람직하다.

## GitHub Release

`.github/workflows/release-macos.yml`은 `v*` 태그 푸시를 감지해 Apple Silicon용
자체 포함 배포본을 만들고 GitHub Release에 ZIP을 첨부한다.

```bash
git tag v0.1.1
git push origin v0.1.1
```

생성물은 `MDPlayer4Mac-osx-arm64.zip`이다. 현재 배포본은 Apple Developer ID 서명과
notarization을 적용하지 않으므로, 다른 Mac에서 처음 실행할 때 Gatekeeper의 확인 절차가
필요할 수 있다.

## 남은 과제와 범위 밖 기능

- Apple Developer ID 서명·notarization을 포함한 정식 배포 파이프라인
- 원본의 실제 하드웨어 칩 출력(SCCI/C86CTL 계열)과 Windows 전용 ASIO/WASAPI 출력
- VST 호스팅과 실시간 MIDI 패스스루
- LHA 압축 파일 처리
- 지원 포맷과 칩 조합별 실기 회귀 테스트 확장

Windows 원본의 코드를 무조건 그대로 복제하기보다, macOS에서 의미 없는 Windows 전용
기능은 분리하고 재생·시각화·재생목록 경험을 우선적으로 유지한다.

## 라이선스

원본 MDPlayer 및 `MDSound/`에 포함된 외부 코드의 라이선스를 따른다. 새 코드를 추가하거나
배포할 때에는 해당 원본 파일의 저작권 및 GPL 계열 조건을 함께 확인한다.
