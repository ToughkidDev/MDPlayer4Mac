# MDPlayer4Mac 포팅 노트

Windows용 MDPlayer의 재생 엔진과 화면 구성을 macOS로 옮기는 작업이다. 원본
Windows/WinForms 코드는 `MDPlayer/`에 보존하고, macOS 구현은 이 `macos/` 아래의
`net8.0` 프로젝트로 분리한다.

마지막 갱신: 2026-08-26

## 최근 포팅 작업 (v0.1.6)

- VGM 헤더의 K051649/K052539 구분 비트(bit 31)를 보존한다. K052539(SCC+) 파일은
  정보·대시보드·볼륨 뷰에서 `SCC+`로, K051649는 `SCC`로 각각 표시한다. 두 칩은
  공통 믹서 경로를 사용하되, SCC+의 독립 5번 웨이브 RAM 쓰기 페이지도 처리한다.
- 곡 정보 뷰는 제목·작곡가·Notes 등의 긴 GD3 텍스트를 더 이상 말줄임표로 축약하지
  않는다. 창을 넓히면 값 열도 같이 늘어나 전체 문자열을 읽을 수 있다.
- 대시보드에 파일을 드롭하여 재생목록을 교체할 때, 같은 채널 구조의 곡은 현재 창
  크기를 유지한다. 창 자동 맞춤은 뷰 모드 또는 실제 칩 패널 구조가 바뀔 때만 수행한다.

## 최근 포팅 작업 (v0.1.5)

- Windows 원본의 `frmSetting` 구성을 기준으로 Avalonia 설정 창을 추가했다. `Output`,
  `Sound`, 칩별 에뮬레이션, MIDI, 재생목록, 네트워크, 기타, About을 포함한 원본 탭을
  확인할 수 있다. 현재 macOS에 실제로 연결된 항목은 Output이며, Windows 전용 기능은
  오동작하지 않도록 비활성 상태로 표시한다.
- Output에서는 Core Audio 시스템 기본 장치, 렌더링 버퍼 지연 시간(25~500 ms), 재생 전
  대기 시간, 샘플 레이트를 설정한다. 저장한 출력 설정은 다음 재생에서 새 Audio Queue로
  적용된다.
- 설정 창은 긴 섹션 제목·옵션을 줄바꿈하고, 섹션 박스가 탭 내용 폭을 넘지 않도록
  고정했다. 세로 스크롤바는 탭 오른쪽 경계에 배치한다.
- Windows 원본의 Setting 스프라이트와 About 삽화를 가져왔다. 설정 아이콘은 대시보드
  우측 하단의 16×16 유틸리티 버튼으로 분리해 타임라인 폭을 차지하지 않는다.
- 대시보드 키보드 단축키를 추가했다. `Q W E R T Y U`는 Stop/Pause/Previous/Slow/
  Play/Fast/Next, `A S D F G H J`는 Open/Playlist/Information/Volume/Channel/Zoom/
  Loop 순서다. 비활성 버튼은 키보드로도 실행하지 않으며 Cmd/Ctrl+A, Delete,
  Backspace 같은 재생목록 편집 키는 유지한다.

## 최근 포팅 작업 (v0.1.4)

- PCM·ADPCM 계열을 포함한 채널 뷰의 레벨 미터 경로를 전수 점검했다. 재생 전과 Stop 뒤의
  미터 기본값은 0으로 초기화하고, YM2610/YM2608 등의 ADPCM-A·ADPCM-B 미터는 실제 채널
  활동에 따라 변하도록 보정했다.
- ADPCM 미터는 볼륨 뷰의 사용자가 설정한 게인과 독립적으로 원래 채널 신호를 표시한다.
  작은 ADPCM-B 변화도 확인하기 쉽도록 표시 감도를 확대했다.
- 채널 뷰 크기는 25%·50%·75%·100%를 지원하며, 기본 75%에서
  `75 → 100 → 75 → 50 → 25 → 50 → 75` 순서로 왕복한다. 대시보드 시간 표시와 진행 막대도
  이에 맞춰 축소·확대된다.
- 진행 막대 아래에는 현재 곡의 사용 칩을 항상 표시한다. 같은 칩이 복수 인스턴스로
  사용되면 `2xYM2610`, `3xDCSG`처럼 합쳐 표시한다.

## 최근 포팅 작업 (v0.1.3)

- Windows 원본의 픽셀 스프라이트를 사용해 재생·정지·일시정지·이전/다음·되감기/빨리감기,
  반복/랜덤, 채널·볼륨·재생목록·곡 정보·50% 축소 버튼을 플레이어 대시보드에 구성했다.
- 대시보드에 Windows 원본 글꼴 기반의 현재 시간/전체 길이/루프 위치 표시와 진행 막대를
  추가했다. 파일명 영역 오른쪽에는 `-60 dB`~`+20 dB` 범위의 마스터 볼륨 페이더를 둔다.
- 곡 정보 뷰는 GD3 메타데이터와 가사 이벤트를 표시한다. 채널 뷰는 VGM의 칩 패널을
  FM → SSG/PSG → PCM 순서로 정렬하고, Yamaha 복합 칩의 서브 출력도 이 순서에 맞춘다.
- 볼륨 뷰는 실제로 사용된 칩 인스턴스를 각각 표시하고, FM/SSG/ADPCM/PCM처럼 분리 가능한
  출력은 독립 슬라이더로 제어한다. YM2612의 FM/ADPCM, Y8950의 FM/ADPCM도 포함한다.
- MDX는 OPM(YM2151)과 PCM8/PDX 구성을 별도로 표시하며, PDX를 찾지 못해도 OPM 재생을
  계속 시도한다. MDX 드라이버 루프는 VGM과 같은 최대 2회 기준으로 종료 처리한다.
- VGM 1.01 이하의 단일 FM 클록 헤더는 실제 명령 스트림을 분석해 YM2413/YM2612/YM2151 중
  올바른 칩 하나만 초기화한다. 이로써 YM2413 곡에 YM2612·YM2151 패널이 함께 나타나던
  문제를 막고, 이후 버전 전용 헤더 필드도 버전 조건으로 안전하게 읽는다.
- Finder에서 디렉터리를 드롭하면 지원 확장자의 음악 파일을 하위 폴더까지 안정된 이름순으로
  찾아 재생목록에 넣는다. 재생목록 영역 드롭은 추가, 대시보드 드롭은 현재 목록 교체다.
- macOS 상단 앱 메뉴와 Dock 이름은 `MDPlayer4Mac`으로 통일했다. `dotnet run`처럼 `.app`
  번들 밖에서 실행하는 경우에도 AppKit 브리지가 같은 이름과 Windows 원본 메인 아이콘을
  설정하며, GitHub Release의 `.app` 번들은 `MDPlayer4Mac.icns`를 포함한다.

## 현재 제공 기능

- CoreAudio Audio Queue Services를 이용한 실시간 재생
- Windows 원본의 스프라이트를 이용한 전송 버튼과 칩 채널 표시계
- VGM/VGZ, XGM/XGZ, SID, MND, ZMS/ZMD, MDX/MDR, NSF, GBS, HES, S98, AY, ZGM 로드
- VGM에 기록된 칩 구성과 복수 칩 인스턴스를 바탕으로 한 채널 뷰
- 칩·서브컴포넌트별 볼륨 믹서 및 마스터 볼륨, 원곡 기본값으로 볼륨 초기화
- 채널 뷰 / 볼륨 뷰 / 재생목록 전용 뷰 전환
- 다중 파일 재생목록, 파일·디렉터리 드래그 앤 드롭 추가·교체, 다중 선택, Cmd+A,
  Delete/Backspace 제거
- 다음/이전 곡, 곡 종료 후 다음 곡 자동 재생, 반복 재생, 재생 속도 조절
- Play 버튼 2초 롱프레스 또는 macOS Force Click으로 자동 재생 토글
- Play 클릭 시 Fast/Slow로 바꾼 재생 속도를 1x로 복귀
- 채널 뷰 25% / 50% / 75% / 100% 크기 전환(기본 75%)
- Windows 설정 창의 Output 및 호환성 탭, About 화면
- 대시보드 버튼 단축키: `Q W E R T Y U` / `A S D F G H J`
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
- VGM 1.01 이하의 공유 FM 클록은 명령 스트림으로 YM2413/YM2612/YM2151을 판별한다.
  따라서 오래된 YM2413 VGM도 실제 사용 칩만 채널 뷰에 표시한다.
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
- 키보드는 위쪽 버튼 줄을 `Q W E R T Y U`, 아래쪽 버튼 줄을 `A S D F G H J`에 각각
  왼쪽부터 대응한다. 예를 들어 `T`는 Play, `F`는 볼륨 뷰, `J`는 반복/랜덤 버튼이다.

### 설정

- 대시보드 우측 하단의 Setting 아이콘으로 연다.
- `Output`은 Core Audio 출력의 렌더링 지연 시간, 재생 전 대기 시간, 샘플 레이트를 저장한다.
  저장값은 다음 재생에서 적용된다.
- 나머지 탭은 Windows 원본 설정 구조와 항목명을 보존한 호환성 화면이다. SCCI/C86CTL,
  ASIO/WASAPI, VST, MIDI 실시간 입출력처럼 macOS 포트에 아직 구현되지 않은 기능은 비활성
  표시되어 설정값이 재생 엔진에 잘못 적용되지 않는다.

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
Apple Silicon용 dylib로 컴파일해 함께 배포한다. 이 dylib는 Force Click 처리 외에도
`.app` 밖의 개발 실행에서 AppKit의 앱 이름과 Dock 아이콘을 설정한다.

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
git tag v0.1.6
git push origin v0.1.6
```

생성물은 `MDPlayer4Mac-osx-arm64.zip`이며, 압축을 풀면 하나의
`MDPlayer4Mac.app` 번들이 나온다. 필요한 .NET·Avalonia·Force Touch dylib와
`MDPlayer4Mac.icns` 아이콘은 모두 앱 번들 안에 포함되며, CI에서 ad-hoc 서명을 적용한다. 다만 Apple Developer ID 서명과
notarization은 적용하지 않으므로, 다른 Mac에서 처음 실행할 때 Gatekeeper의 확인 절차는
여전히 필요할 수 있다.

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
