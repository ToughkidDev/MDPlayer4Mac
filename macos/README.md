# MDPlayer4Mac — 포팅 작업 노트

MDPlayer(Windows, WinForms, .NET 8-windows)를 macOS로 옮기는 작업의 진행 상황과
다음 할 일을 정리한 문서입니다. 원본 Windows 소스(`MDPlayer/`)는 건드리지 않고,
이 `macos/` 폴더 아래에 크로스플랫폼(net8.0, `-windows` 접미사 없음) 프로젝트를
새로 만들어가는 방식으로 진행합니다.

## 현재 상태 (2026-08-19)

### ✅ MDSound — 사운드 칩 에뮬레이션 코어 (완료, 빌드 검증됨)

- 원본: [kuma4649/MDSound](https://github.com/kuma4649/MDSound) (GPLv3). MDPlayer 저장소에는
  소스가 없고 미리 빌드된 `MDSound.dll`만 참조하는 구조였는데, 실제 소스는 이 별도
  저장소에 전부 공개되어 있었습니다.
- `macos/MDSound/`에 소스 전체(테스트용 WinForms 프로젝트 제외, 131개 파일)를 그대로
  가져와 `net8.0` 대상 라이브러리로 빌드했습니다.
- **Windows 전용 의존성 0개.** YM2612/SN76489/SID/AY8910 등 모든 칩 에뮬레이션 코드가
  순정 관리 코드(managed C#)라, 원래 `netstandard2.0`을 타겟하던 걸 `net8.0`으로
  바꾸기만 하면 됩니다. 실제로 리눅스 샌드박스에서 `dotnet build`로 0 error 확인했습니다
  (macOS도 같은 non-Windows .NET 런타임이라 그대로 통할 것으로 예상).
- 즉 MDPlayer에서 가장 위험 부담이 큰 부분(칩 에뮬레이션 정확도)은 이미 검증된 코드를
  그대로 재사용할 수 있다는 뜻입니다.

### ✅ MDPlayerCore — 파일 포맷 파싱/시퀀싱 레이어 (컴파일 성공)

`MDPlayerx64/Driver/` 이하와 그 지원 클래스들을 WinForms/NAudio 출력/레지스트리 등
Windows 전용 요소로부터 떼어내는 작업입니다. `macos/MDPlayerCore/`에 옮긴 파일:

- `Driver/baseDriver.cs`, `Driver/vgm.cs` (VGM/메가드라이브 포맷 드라이버)
- `Setting.cs`, `common.cs`, `ChipRegister.cs`, `dacControl.cs`
- `Tone.cs`, `dgvColumnInfo.cs`, `midiOutInfo.cs`, `VST/vstInfo.cs`
- `PlayList.cs`, `MIDIExport.cs`, `MIDIParam.cs`, `ChipLEDs.cs`
- `PianoRollMng.cs` + `PianoRoll/*.cs` (건반 시각화 로직 — 칩별 note on/off 계산.
  실제 그리기가 아니라 로직만이라 Windows 의존성 없음)
- `Driver/SID/**` — libsidplayfp를 통째로 이식한 SID 드라이버 (88개 파일, 3만 줄대.
  MOS 6510 CPU 에뮬레이터까지 포함. Windows 의존성 0개로 그대로 붙었습니다)
- `CompatShims.cs` — 아래 "이식하며 손댄 부분" 참고

**실제 Mac에서 `dotnet build` 0 error로 확인 완료했습니다** (MDSound, MDPlayerCore 둘 다).
마지막에 걸렸던 `MDChipParams.cs`, `Driver/MNDRV/*`(둘 다 Windows 의존성 없음)를
채우고, `PlayList.cs`가 실제 `DataGridView`(WinForms 그리드 컨트롤)에 행을 직접
만들어 넣는 구조인 걸 뒤늦게 발견해서 `CompatShims.cs`에 컴파일만 되게 하는
최소 `DataGridView`/`DataGridViewRow`/`DataGridViewColumn` 셈으로 대체했습니다
(지금은 아무것도 실제로 채워주지 않는 빈 그리드 — UI 붙일 때 `PlayList.cs`를
데이터/화면 분리하는 리팩터링을 하든, 이 셈에 진짜 저장소를 붙이든 결정 필요).

`RealChip.cs`는 그대로 가져오지 않았습니다. 열어보니 `NScci`/`Nc86ctl`/`NiseC86ctl`
(SCCI/C86Ctrl 계열 — 실제 FM 신디사이저 칩 확장 카드를 시리얼/전용 인터페이스로
제어하는 Windows 전용 드라이버 라이브러리, 저장소에 없음)에 의존하고 있어서,
`ChipRegister.cs`가 실제로 쓰는 부분만 골라 `CompatShims.cs`에 넣었습니다:
Windows 의존성이 전혀 없는 `RSoundChip` 베이스 클래스는 원본 그대로 옮기고,
`RealChip` 자체는 (생성자 / `SendData()` / `Dispose()`만 있으면 되길래) 아무 동작
안 하는 스텁으로 대체했습니다. 실물 FM 칩 확장 카드를 macOS에서 쓰는 기능은
사실상 없는 셈인데, 애초에 그런 하드웨어 자체가 Windows PC 전용 애드인 카드라
macOS에서 의미가 크지 않은 기능입니다.

### 아직 손 안 댄 것

- **오디오 출력**: `NAudioWrap.cs`, `myAsioOut.cs` 등은 전부 Windows 전용
  (WASAPI/WinMM/ASIO). macOS에서 스피커로 실제 소리를 내려면 CoreAudio 계열
  (예: `AVAudioEngine`를 P/Invoke 하거나, 크로스플랫폼 오디오 바인딩 라이브러리)로
  새로 짜야 합니다. 이 프로젝트가 "VGM → WAV 파일 렌더링"까지만 되면 사실 오디오
  출력 레이어 없이도 엔진 동작 자체는 검증 가능합니다 (`WaveWriter.cs`는 파일 쓰기라
  이식이 쉬움).
- **VST 플러그인 호스팅**: Win32 `LoadLibrary`로 `.dll`을 직접 로드하는 구조라
  Mac에서는 전혀 다른 방식(AU/VST3 번들 로딩)이 필요. 우선순위 낮음.
- **전역 키보드 훅** (`KeyboardHook3.cs`): `SetWindowsHookEx` 기반. macOS는
  `NSEvent` 전역 모니터 + 손쉬운 사용 권한 필요. UI 작업 단계에서 처리.
- **UI**: 아직 시작 안 함. Avalonia로 가기로 방향만 정함 (WinForms의
  `form/` 5만 줄, 특히 커스텀 GDI+ 드로잉인 `drawBuff.cs`가 제일 큰 작업이 될 것).
- **MIDI 출력**: `ChipRegister.cs`의 실시간 MIDI 패스스루는 `CompatShims.cs`에
  아무 동작 안 하는 스텁으로 막아뒀습니다. 실제로 쓰려면 CoreMIDI 연동이 필요.
- **LHA 압축 해제** (`UnlhaWrap/`): Windows 전용 `unlha32.dll` 래퍼. 대체 라이브러리
  또는 셸 커맨드로 교체 필요. 아직 안 건드림.

## 이식하며 손댄 부분 (`CompatShims.cs`)

원본 코드를 최대한 그대로 유지하면서, WindowsDesktop SDK가 암묵적으로 제공하던
타입 몇 개만 최소 크기로 대체했습니다:

- `Point`, `Size` — `System.Drawing`의 동명 구조체 대신 직접 정의 (단순 값 구조체라
  기능 차이 없음. `System.Drawing.Common` 패키지 의존을 피하려는 목적).
- `FormWindowState` — WinForms 열거형 대신 이름만 같은 자체 열거형.
- `Resources.cntSettingFileName` — 리소스 시스템 전체 대신 상수 하나만 (`"Setting.xml"`).
- `NAudio.Midi.MidiOut` — 실제 MIDI 출력 대신 아무 동작 안 하는 스텁 (위 참고).

그 외에 실제로 고친 부분:

- `Driver/vgm.cs`의 `pic` 필드: `System.Drawing.Image` → `byte[]` (GD3 태그의
  아트워크 필드인데 어디서도 읽거나 쓰지 않는 걸 확인 — 안전한 변경).
- `common.cs`, `Driver/xgm2.cs`, `Driver/aiff.cs`, `Driver/mp3.cs`,
  `ChipRegister.cs` 등에 남아있던 안 쓰는 `using NAudio.*;` / `using MDPlayerx64;` 등
  죽은 import 제거.
- `Setting.cs`의 `#if X64 ... #else ... #endif` 블록(VisualBasic/Properties
  참조) 제거 — 실제로 쓰는 건 문자열 상수 하나뿐이라 위 `Resources` 스텁으로 대체.

## 빌드하는 법

```
cd macos/MDPlayerCore
dotnet build -c Release
```

`Z80dotNet` NuGet 패키지 restore가 필요해서 인터넷 연결이 있어야 합니다.

## 다음 단계 후보

MDPlayerCore가 라이브러리로서는 컴파일되지만, 아직 "VGM 파일을 실제로 읽어서 뭔가
출력하는" 실행 가능한 진입점은 없습니다. 다음으로 하면 좋을 것:

1. **VGM → WAV 스모크 테스트용 콘솔 앱** (`macos/EngineSmokeTest/` 같은 이름으로)을
   새로 만들어서 `Vgm` 드라이버 + `ChipRegister` + `MDSound`를 실제로 초기화하고
   VGM 파일 하나를 끝까지 재생시켜 `WaveWriter.cs`로 WAV 파일에 떨어뜨려보는 것.
   여기서 십중팔구 "컴파일은 되는데 런타임에 null 참조" 같은 issue들이 나올 텐데,
   그게 진짜 다음 산 넘기입니다 (Setting 초기화 순서, ChipRegister 생성자 인자로
   뭘 넘겨야 하는지 등 — 원래 UI 코드(`frmMain.cs`)가 어떻게 조립하는지 참고 필요).
2. 그 다음에야 오디오 출력 레이어(CoreAudio) 붙이기, UI(Avalonia) 시작하기로 넘어가는
   게 순서상 맞을 것 같습니다.

## 라이선스 메모

MDSound는 GPLv3입니다 (`macos/MDSound/LICENSE.txt`). MDPlayer 본체도 동일 라이선스
계열로 알고 있으니 macOS 포크도 GPLv3를 유지하는 게 맞습니다 — 배포 전에 원본
LICENSE.txt 조건(특히 SCCI2 동봉 관련 별도 허가 조항)도 한 번 더 확인하세요.
