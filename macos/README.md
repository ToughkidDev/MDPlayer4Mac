# MDPlayer4Mac

Windows용 MDPlayer를 macOS에서 쓸 수 있도록 옮기는 개인 포팅 프로젝트임. 원본
WinForms 코드는 저장소의 `MDPlayer/`에 그대로 두고, macOS 쪽 코드는 `macos/` 아래의
`.NET 8` 프로젝트로 따로 관리 중임.

마지막 갱신: 2026-08-26

## v0.1.6

- VGM의 K051649/K052539 플래그를 읽어 일반 SCC는 `SCC`, SCC+는 `SCC+`로 표시했음.
  SCC+의 5번 채널 독립 웨이브 RAM 쓰기도 그대로 처리함.
- 곡 정보 화면에서 제목, 작곡가, Notes 같은 긴 문자열을 `...`으로 줄이지 않게 했음. 창을
  넓히면 정보 열도 함께 넓어져 전체 내용을 읽을 수 있음.
- 대시보드에 파일을 드롭해 재생목록을 바꿀 때, 같은 칩 구성의 곡이면 창 크기를 그대로
  유지하게 했음. 실제 채널 패널 구성이 달라질 때만 다시 크기를 맞춤.

## 이전 포팅 작업

### v0.1.5

- Windows 원본의 `frmSetting` 구성을 기준으로 Avalonia 설정 창을 추가했음. `Output`,
  `Sound`, 칩별 에뮬레이션, MIDI, 재생목록, 네트워크, 기타, About을 포함한 원본 탭을
  확인할 수 있음. 현재 macOS에 실제로 연결한 항목은 Output이며, Windows 전용 기능은
  오동작하지 않게 비활성 상태로 표시함.
- Output에서는 Core Audio 시스템 기본 장치, 렌더링 버퍼 지연 시간(25~500 ms), 재생 전
  대기 시간, 샘플 레이트를 설정함. 저장한 출력 설정은 다음 재생에서 새 Audio Queue로
  적용됨.
- 설정 창은 긴 섹션 제목·옵션을 줄바꿈하고, 섹션 박스가 탭 내용 폭을 넘지 않게 고정했음.
  세로 스크롤바는 탭 오른쪽 경계에 배치함.
- Windows 원본의 Setting 스프라이트와 About 삽화를 가져왔음. 설정 아이콘은 대시보드 우측
  하단의 16×16 유틸리티 버튼으로 분리해 타임라인 폭을 차지하지 않게 했음.
- 대시보드 키보드 단축키를 추가했음. `Q W E R T Y U`는 Stop/Pause/Previous/Slow/
  Play/Fast/Next, `A S D F G H J`는 Open/Playlist/Information/Volume/Channel/Zoom/
  Loop 순서임. 비활성 버튼은 키보드로도 실행하지 않으며 Cmd/Ctrl+A, Delete,
  Backspace 같은 재생목록 편집 키는 유지함.

### v0.1.4

- PCM·ADPCM 계열을 포함한 채널 뷰의 레벨 미터 경로를 전수 점검했음. 재생 전과 Stop 뒤의
  미터 기본값은 0으로 초기화하고, YM2610/YM2608 등의 ADPCM-A·ADPCM-B 미터는 실제 채널
  활동에 따라 변하도록 보정함.
- ADPCM 미터는 볼륨 뷰의 사용자가 설정한 게인과 독립적으로 원래 채널 신호를 표시함.
  작은 ADPCM-B 변화도 확인하기 쉽게 표시 감도를 확대했음.
- 채널 뷰 크기는 25%·50%·75%·100%를 지원하며, 기본 75%에서
  `75 → 100 → 75 → 50 → 25 → 50 → 75` 순서로 왕복함. 대시보드 시간 표시와 진행 막대도
  이에 맞춰 축소·확대됨.
- 진행 막대 아래에는 현재 곡의 사용 칩을 항상 표시함. 같은 칩이 복수 인스턴스로
  사용되면 `2xYM2610`, `3xDCSG`처럼 합쳐 표시함.

### v0.1.3

- Windows 원본의 픽셀 스프라이트를 사용해 재생·정지·일시정지·이전/다음·되감기/빨리감기,
  반복/랜덤, 채널·볼륨·재생목록·곡 정보·50% 축소 버튼을 플레이어 대시보드에 구성했음.
- 대시보드에 Windows 원본 글꼴 기반의 현재 시간/전체 길이/루프 위치 표시와 진행 막대를
  추가했음. 파일명 영역 오른쪽에는 `-60 dB`~`+20 dB` 범위의 마스터 볼륨 페이더를 둠.
- 곡 정보 뷰는 GD3 메타데이터와 가사 이벤트를 표시함. 채널 뷰는 VGM의 칩 패널을
  FM → SSG/PSG → PCM 순서로 정렬하고, Yamaha 복합 칩의 서브 출력도 이 순서에 맞춤.
- 볼륨 뷰는 실제로 사용된 칩 인스턴스를 각각 표시하고, FM/SSG/ADPCM/PCM처럼 분리 가능한
  출력은 독립 슬라이더로 제어함. YM2612의 FM/ADPCM, Y8950의 FM/ADPCM도 포함됨.
- MDX는 OPM(YM2151)과 PCM8/PDX 구성을 별도로 표시하며, PDX를 찾지 못해도 OPM 재생을
  계속 시도함. MDX 드라이버 루프는 VGM과 같은 최대 2회 기준으로 종료 처리함.
- VGM 1.01 이하의 단일 FM 클록 헤더는 실제 명령 스트림을 분석해 YM2413/YM2612/YM2151 중
  올바른 칩 하나만 초기화함. 이로써 YM2413 곡에 YM2612·YM2151 패널이 함께 나타나던
  문제를 막고, 이후 버전 전용 헤더 필드도 버전 조건으로 안전하게 읽음.
- Finder에서 디렉터리를 드롭하면 지원 확장자의 음악 파일을 하위 폴더까지 안정된 이름순으로
  찾아 재생목록에 넣음. 재생목록 영역 드롭은 추가, 대시보드 드롭은 현재 목록 교체임.
- macOS 상단 앱 메뉴와 Dock 이름은 `MDPlayer4Mac`으로 통일했음. `dotnet run`처럼 `.app`
  번들 밖에서 실행하는 경우에도 AppKit 브리지가 같은 이름과 Windows 원본 메인 아이콘을
  설정하며, GitHub Release의 `.app` 번들은 `MDPlayer4Mac.icns`를 포함함.

## 현재 가능한 것

- Core Audio Audio Queue를 이용한 실시간 재생
- Windows판 스프라이트를 이용한 재생 버튼과 칩 채널 표시
- 채널 뷰, 볼륨 뷰, 재생목록 전용 뷰, 곡 정보 뷰
- 칩별/출력 블록별 볼륨 조절과 마스터 볼륨
- 다중 파일 재생목록, 파일 및 폴더 드래그 앤 드롭, 다중 선택과 삭제
- 다음 곡 자동 재생, 반복/랜덤 재생, 재생 속도 조절, 자동 재생
- 채널 뷰 크기 전환: 25% / 50% / 75% / 100% (기본 75%)
- Windows판 설정 창 구성과 About 화면. 현재 실제로 연결된 설정은 Output 중심임.
- 태그를 푸시하면 Apple Silicon용 macOS 앱을 자동으로 빌드해 GitHub Release에 올림.

## 지원 포맷

UI에서 열거나 드롭할 수 있는 확장자는 아래와 같음.

```text
.vgm  .vgz  .xgm  .xgz  .sid  .mnd  .zms  .zmd
.mdx  .mdr  .nsf  .gbs  .hes  .s98  .ay  .zgm
```

- `.vgz`, `.xgz`는 각각 gzip 압축 VGM/XGM으로 처리함.
- 오래된 VGM 1.01 이하 파일은 명령 스트림을 확인해 YM2413/YM2612/YM2151 중 실제 사용 칩만
  초기화함.
- MDX/MDR은 X68000 계열 포맷임. 같은 폴더의 PDX를 찾으면 PCM8/ADPCM 재생에 사용하며,
  PDX가 없어도 OPM(YM2151) 파트는 계속 재생을 시도함.
- 지원 목록에 있는 형식이라도 모든 드라이버와 칩 조합이 완성된 것은 아님. 로드할 수 없는
  조합은 상태줄에 오류로 표시됨.

## 플레이어 사용법

파일을 처음 열면 채널 뷰가 나옴. 채널 뷰와 볼륨 뷰 아래에는 약 3.5행 높이의 재생목록이
붙고, 재생목록 버튼을 누르면 20행 높이의 목록 전용 화면으로 바뀜. 20곡이 넘는 경우에는
목록 안에서 스크롤됨.

- 재생목록 영역에 파일이나 폴더를 놓으면 현재 목록 뒤에 추가됨.
- 상단 대시보드에 놓으면 현재 재생목록을 새 목록으로 교체함.
- 폴더를 드롭하면 하위 폴더까지 찾아 지원하는 파일을 이름순으로 추가함.
- Cmd+A, Shift/Cmd 선택, Delete/Backspace, Escape로 일반적인 목록 편집이 가능함.
- Stop을 눌러도 현재 채널 뷰는 닫히지 않음.
- 곡이 끝나면 다음 곡을 재생함. 반복 버튼은 현재 곡 반복이고, 한 번 더 누르면 랜덤 재생으로
  바뀜.
- Play를 짧게 누르면 재생 속도를 1x로 돌린 뒤 재생 또는 재개함. Play를 2초 이상 누르거나
  Force Click하면 자동 재생을 켜고 끔.

### 키보드 단축키

위쪽 버튼 줄은 `Q W E R T Y U`, 아래쪽 버튼 줄은 `A S D F G H J`에 왼쪽부터 대응함.

```text
Q Stop       W Pause      E Previous   R Slow
T Play       Y Fast       U Next
A Open       S Playlist   D Information  F Volume
G Channel    H Zoom       J Loop / Random
```

## 채널 뷰와 볼륨 뷰

채널 뷰는 VGM에서 실제로 사용하는 칩을 FM → SSG/PSG → PCM 순서로 보여 줌. 같은 칩이
여러 개면 모두 표시하고, 대시보드에는 `2xYM2610`처럼 묶어서 표시함. Yamaha 복합 칩은
FM, SSG, ADPCM, PCM 등 성격이 다른 출력을 나눠 보여 줌.

볼륨 뷰는 현재 곡에서 사용하는 칩 인스턴스만 표시함. FM/SSG/ADPCM/PCM처럼 독립적인
출력은 가능한 범위에서 각각 조절할 수 있음. Reset Volume은 파일 안에 저장된 기본 볼륨으로
되돌리고, 기본값이 없는 포맷은 0 dB를 사용함.

## 설정

대시보드 오른쪽 아래의 Setting 아이콘으로 열 수 있음.

- `Output`에서는 Core Audio 기본 출력, 버퍼 지연 시간(25~500 ms), 재생 전 대기 시간,
  샘플 레이트를 설정함. 변경값은 다음 재생부터 적용됨.
- Sound, 칩별 에뮬레이션, MIDI, 재생목록, 네트워크, 기타, About은 Windows판의 구성과
  항목명을 최대한 유지했음.
- SCCI/C86CTL, ASIO/WASAPI, VST, 실시간 MIDI I/O처럼 macOS에 아직 없는 기능은 비활성으로
  보임. 구현되지 않은 설정이 재생에 영향을 주지 않게 하기 위함.

## 개발 환경

- macOS
- Xcode Command Line Tools (`xcrun clang` 포함)
- .NET SDK 8 이상
- 최초 NuGet 복원 시 인터넷 연결

`MDPlayerUI`는 Avalonia를 사용함. Force Click은 Avalonia의 일반 포인터 이벤트로 들어오지
않기 때문에 `MDPlayerUI/Native/ForceTouchMonitor.m`을 작은 Apple Silicon용 dylib로 빌드해
함께 사용함. 이 라이브러리는 개발 실행 시 AppKit의 앱 이름과 Dock 아이콘을 맞추는 역할도
함.

## 빌드와 실행

저장소 루트에서 실행하면 됨.

```bash
dotnet build macos/MDPlayerUI/MDPlayerUI.csproj -c Release
dotnet run --project macos/MDPlayerUI/MDPlayerUI.csproj -c Release
```

GUI 없이 재생 엔진만 확인하려면 아래처럼 실행하면 됨.

```bash
dotnet run --project macos/LivePlayer/LivePlayer.csproj -c Release -- /path/to/song.vgm
```

VGM을 WAV로 렌더링하는 스모크 테스트는 아래와 같음.

```bash
dotnet run --project macos/EngineSmokeTest/EngineSmokeTest.csproj -c Release -- /path/to/song.vgm /path/to/output.wav
```

## 릴리즈

`v*` 태그를 푸시하면 `.github/workflows/release-macos.yml`이 Apple Silicon용 자체 포함 앱을
빌드해 Release에 첨부함.

```bash
git tag v0.1.6
git push origin v0.1.6
```

생성물은 `MDPlayer4Mac-osx-arm64.zip`임. 압축을 풀면 하나의 `MDPlayer4Mac.app` 번들이
나옴. .NET 런타임, Avalonia, Force Touch dylib, 앱 아이콘은 번들 안에 포함됨.

## Gatekeeper 안내

현재 배포본은 CI에서 ad-hoc 서명만 적용함. Apple Developer ID 서명과 notarization은 아직
하지 않았기 때문에, 다른 Mac에서 처음 실행할 때 Gatekeeper가 개발자를 확인할 수 없다는
경고를 표시할 수 있음. 이는 현재 배포 방식에서는 정상적인 동작임.

앱은 반드시 이 저장소의 [GitHub Release](https://github.com/ToughkidDev/MDPlayer4Mac/releases)
에서 받은 `MDPlayer4Mac-osx-arm64.zip`만 사용해야 함. 출처가 확실하지 않은 앱에는 아래
방법을 적용하면 안 됨.

가장 권장하는 실행 방법은 아래와 같음.

1. ZIP을 풀어 `MDPlayer4Mac.app`을 `응용 프로그램` 폴더로 옮김.
2. 앱을 한 번 열어 Gatekeeper 경고를 표시함.
3. **시스템 설정 → 개인정보 보호 및 보안**으로 들어가서 화면 아래쪽의 **그래도 열기**를
   누름.
4. 다시 나타난 확인 창에서 **열기**를 누르고 macOS 로그인 암호를 입력함.

이 승인은 해당 앱에만 저장되므로, 이후에는 일반 앱처럼 더블 클릭해서 실행할 수 있음.
`그래도 열기` 버튼은 처음 실행을 막은 뒤 약 한 시간 동안 표시됨.

시스템 설정에 버튼이 나오지 않는 경우에만, GitHub Release에서 받은 파일이 맞는지 확인한 뒤
터미널에서 격리 속성을 제거할 수 있음.

```bash
xattr -dr com.apple.quarantine "/Applications/MDPlayer4Mac.app"
```

이 명령은 앱 하나에만 적용되며 Gatekeeper 자체를 끄지는 않음. 다만 다운로드 파일의 보안
표시를 직접 없애는 방식이므로, 출처를 확인할 수 없는 앱이나 `손상되었음`·악성 코드 경고가
나오는 앱에는 사용하면 안 됨. 그런 경우에는 앱을 삭제하고 Release ZIP을 다시 받아야 함.

Apple의 최신 안내도 처음 시도한 뒤 **개인정보 보호 및 보안 → 그래도 열기**로 해당 앱만
허용하는 방식을 권장함. [Apple Gatekeeper 안내](https://support.apple.com/guide/mac-help/open-a-mac-app-from-an-unknown-developer-mh40616/mac)

## 알려진 범위와 다음 작업

- Apple Developer ID 서명과 notarization
- SCCI/C86CTL 실제 칩 출력, Windows 전용 ASIO/WASAPI 출력
- VST 호스팅과 실시간 MIDI 패스스루
- LHA 압축 파일 처리
- 포맷과 칩 조합별 실제 곡 회귀 테스트 확대

`Z80dotNet 1.0.6`은 .NET Framework 대상 패키지라 `NU1701` 경고가 남음. 현재 빌드와
재생은 가능하지만, 나중에는 net8.0/netstandard 호환 패키지로 바꾸는 편이 좋음.

## 라이선스

원본 MDPlayer와 `MDSound/` 안에 포함된 외부 코드의 라이선스를 따름. 새 코드를 추가하거나
배포할 때는 원본 파일의 저작권과 GPL 계열 조건을 함께 확인해야 함.
