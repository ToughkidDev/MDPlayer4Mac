# MDPlayer4Mac — 포팅 작업 노트

MDPlayer(Windows, WinForms, .NET 8-windows)를 macOS로 옮기는 작업의 진행 상황과
다음 할 일을 정리한 문서입니다. 원본 Windows 소스(`MDPlayer/`)는 건드리지 않고,
이 `macos/` 폴더 아래에 크로스플랫폼(net8.0, `-windows` 접미사 없음) 프로젝트를
새로 만들어가는 방식으로 진행합니다.

## 현재 상태 (2026-08-19, 칩 배선 13개 → 38개 확장)

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

**실제 Mac에서 `dotnet build -c Release` 0 error로 확인 완료했습니다** (MDSound,
MDPlayerCore 둘 다; `Z80dotNet` NuGet 패키지도 실제로 restore되어 정상 빌드됨 —
다만 이 패키지가 net8.0이 아니라 .NET Framework 4.x 타겟으로 restore된다는 NU1701
경고가 뜨는데, 순수 관리 코드(P/Invoke 없음) 라이브러리라 지금까지는 문제없이
동작. 총 124개 경고는 전부 사전에 있던 패턴(대소문자 타입명, 미사용 필드, nullable
주석 컨텍스트 등)이라 안전 — 아래 "빌드 경고" 참고). 이번 라운드에 추가로 채운 것:

- `MDChipParams.cs`, `Driver/MNDRV/*`, `Driver/ZMS/**`, `Driver/MXDRV/*` — 전부
  Windows 의존성 없음.
- `PlayList.cs`가 실제 `DataGridView`(WinForms 그리드 컨트롤)에 행을 직접
  만들어 넣는 구조인 걸 발견해서 `CompatShims.cs`에 컴파일만 되게 하는
  최소 `DataGridView`/`DataGridViewRow`/`DataGridViewColumn` 셈으로 대체했습니다
  (지금은 아무것도 실제로 채워주지 않는 빈 그리드 — UI 붙일 때 `PlayList.cs`를
  데이터/화면 분리하는 리팩터링을 하든, 이 셈에 진짜 저장소를 붙이든 결정 필요).
- `log.cs`, `Tables.cs`, `M3U.cs`, `UnZDF.cs`, `PodcastFeedParser.cs`,
  `Driver/xgm.cs`, `Driver/xgm2.cs` — 전부 Windows 의존성 없음, 원본 그대로 이식
  (`PodcastFeedParser.cs`/`Driver/xgm2.cs`는 원래 `MDPlayerx64`/`MDPlayerx64.Driver`
  네임스페이스였는데, 이 포팅 프로젝트는 전부 `MDPlayer` 네임스페이스 하나로 통일해서
  쓰고 있어 맞춰 고쳤습니다).
- `Resources` 셈에 `log.cs`가 쓰는 문자열 상수 4개 추가 (`cntLogFilename`,
  `cntTimeFormat`, `cntExceptionFormat`, `cntInnerExceptionFormat`).
- `Application.ExecutablePath`, `MessageBox`/`MessageBoxButtons`/`MessageBoxIcon` —
  WinForms 셈 추가. `MessageBox.Show()`는 지금은 콘솔에 로그만 남기는 무동작 스텁
  (실제 UI 붙을 때 다이얼로그 서비스로 교체 예정).
- `RC86ctlSoundChip`(`RSoundChip`의 구체 서브클래스) + `Nc86ctl.ChipType` 열거형 —
  `ChipRegister.cs`가 실물 OPNA/OPN3L/YM2149/OPL3 하드웨어 클럭 2배 로직에서 직접
  타입 체크/캐스팅하는 걸 뒤늦게 발견. `RealChip`이 이미 무동작 스텁이라 실제로
  이 타입이 생성되진 않지만, 컴파일을 위해 최소한의 형태로 추가.
- `UnlhaWrap.UnlhaCmd` — Windows 전용 `unlha32.dll`(LHA 압축 해제) 래퍼. 실제
  `PlayList.cs`가 LHA로 압축된 플레이리스트 항목을 열 때 씀. `RealChip`과 같은
  패턴으로, 호출부가 요구하는 시그니처(`GetFileList`/`GetFileByte`)만 갖춘 스텁으로
  대체 — 지금은 호출하면 `NotImplementedException`. 나중에 셸 커맨드(`lha`/`7z`)나
  관리 코드 LHA 디코더로 교체 필요.
- `Audio` 클래스 — 원본 `Audio.cs`(약 1만 3600줄)는 NAudio 기반 실제 오디오 출력,
  MIDI 장치 열거, Ogg/Vorbis/FLAC 디코딩, 그리고 아직 이식 안 한 여러 드라이버
  계열(MGSDRV/MuSICA/NDP/FMP/PMDDotNET/MoonDriverDotNET/muapDotNET/NRTDRV/
  MucomDotNET)까지 아우르는 재생 오케스트레이션 엔진이라 통째로 가져오지 않았습니다.
  `ChipRegister.cs`/`PlayList.cs`/`PianoRoll/*.cs`가 "지금 재생 중인 게 뭔지"
  참조하려고 쓰는 정적 멤버 몇 개만(`ClockAY8910` 등 칩 클럭 캐시, `DriverVirtual`,
  `PlayingFileFormat`) `AudioShim.cs`에 최소 구현했고, 진짜 파일 포맷 감지/메타데이터
  추출 로직인 `GetMusic()`은 아직 이식 안 한 드라이버들에 발목 잡혀 있어 호출하면
  `NotImplementedException`을 던지는 스텁으로 남겨뒀습니다 — 다음 단계 후보의
  VGM→WAV 스모크 테스트를 만들 때 결정할 문제 (진짜 `GetMusic` + 필요한 드라이버들을
  이식하거나, 이미 이식된 드라이버들(VGM/XGM/SID/MNDRV/ZMS/MXDRV)만 다루도록
  `PlayList`의 요구 범위를 좁히거나).

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
- **LHA 압축 해제** (`UnlhaWrap/`): Windows 전용 `unlha32.dll` 래퍼. 컴파일만 되는
  스텁(`CompatShims.cs`의 `UnlhaWrap.UnlhaCmd`)으로 막아뒀고, 호출하면
  `NotImplementedException`. 대체 라이브러리 또는 셸 커맨드(`lha`/`7z`)로 교체 필요.
- **`Audio` 클래스(오디오 출력 오케스트레이션 본체)**: `AudioShim.cs`에 다른 파일들이
  참조하는 정적 멤버 몇 개만 최소 스텁으로 있고, 실제 재생/디코딩/파일 포맷 감지
  로직은 이식 안 함 (위 "MDPlayerCore" 섹션 참고).

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

### 빌드 경고 (124개, 전부 안전)

실제 Mac 빌드에서 뜨는 경고들은 전부 아래 몇 가지 패턴으로 분류되고, 전부 원본
코드에 원래 있던 것들이라 안전합니다 (이 포팅 작업이 새로 만들어낸 문제 아님):

- **NU1701** (`Z80dotNet` 패키지) — 이 패키지가 net8.0이 아니라 .NET Framework
  4.x 대상으로 빌드/게시되어 있어서 뜨는 호환성 경고. 순수 C#/관리 코드라
  (Win32 P/Invoke 없음) 지금까지는 실제로 문제없이 동작. 나중에 net8.0/netstandard
  대상 대체 패키지가 있으면 바꾸는 걸 고려.
- **CS8981** (형식 이름이 소문자로만 되어있음, 예: `xgm`, `log`, `mem`, `sid`) —
  C# 언어팀이 미래에 소문자 전용 타입명을 예약어로 쓸 수도 있다는 경고일 뿐,
  지금 컴파일/실행에는 전혀 영향 없음. 원본 MDPlayer/MDSound 코드의 명명 관례를
  그대로 유지.
- **CS0649/CS0169/CS0414** (필드가 할당은 됐지만 안 읽히거나, 아예 안 쓰임) —
  대부분 MDSound의 칩 에뮬레이션 코드(MAME 유래)에 있던 디버그/미사용 플래그.
  로직에 영향 없음.
  이러한 필드에 실제로 값을 넣어야 하는데 빠뜨린 버그일 수도 있는데, 상위 원본
  프로젝트(MDSound/MDPlayer)에도 동일하게 존재하던 경고라 이 포팅 작업에서
  새로 생긴 문제는 아닙니다.
- **CS8632** (nullable 주석은 `#nullable` 컨텍스트 안에서만 써야 함) — `ZMS.cs`
  등 일부 원본 파일에 `?`가 nullable 컨텍스트 밖에서 쓰인 것. 컴파일러가
  무시하고 넘어가는 수준.
- **CS0162/CS0219** (도달 불가능한 코드, 안 쓰이는 지역 변수) — 원본 코드
  그대로, 사소한 데드 코드.

요약하면 지금 단계(라이브러리 컴파일 검증)에서는 전부 무시해도 되는 경고들입니다.
다만 "VGM → WAV 스모크 테스트" 단계에서 실제로 값을 읽고 쓰는 로직을 건드리게
되면, CS0649/CS0414 계열(필드가 할당 안 됨) 중 일부가 진짜 버그였는지 다시 한번
살펴볼 가치는 있습니다.

### ✅ EngineSmokeTest — VGM → WAV 렌더링 검증 (완료, 실제 소리 확인)

`macos/EngineSmokeTest/`에 콘솔 앱을 새로 만들어서, VGM 파일을 실제로 읽어 `Vgm`
드라이버 + `ChipRegister` + `MDSound` 칩 에뮬레이션을 진짜로 돌리고 결과를
`WaveWriter.cs`로 WAV 파일에 떨어뜨리는 것까지 끝까지 검증했습니다. 컴파일만
되는 게 아니라 **실제로 올바른 파형이 나오는 것**을 프로그램적으로 확인했습니다:

- SN76489(PSG) 톤 레지스터를 손으로 채운 76바이트짜리 최소 VGM을 만들어 돌려보니,
  실제로 나온 WAV의 제로크로싱 기반 주파수 추정치가 654.2Hz(계산값) 대비 653.2Hz로
  거의 정확히 일치했습니다.
- YM2612(FM) 레지스터(알고리즘/TL/AR/DR/SR/RR/주파수/키온)를 채운 158바이트짜리
  최소 VGM도 만들어 돌려봤고, 어택 엔벌로프가 실제로 상승하는 파형과 함께 정상
  진동하는 오디오가 나왔습니다.

이 과정에서 발견/처리한 것들:

- `ChipRegister`의 생성자는 `setting.YM2612Type[0]` 등 모든 칩 타입 배열을
  무조건 역참조하는데, `new Setting()`으로 새로 만든 인스턴스는 이 배열들이
  전부 `null`이라 즉시 NRE가 납니다. 원본 `Audio.Init(Setting)`(`MDPlayerx64/Audio.cs`)
  안에 "null이면 UseEmu[0]=true인 기본값으로 채워넣는" 235줄짜리 블록이 있었는데
  (Windows 의존성 없는 순수 로직), 이걸 `Setting.ApplyChipTypeDefaults()`라는
  정식 메서드로 승격시켜 `Setting.cs`에 추가했습니다 — `Setting.Load()`/`new Setting()`
  직후 한 번 호출하면 됩니다. Audio 클래스 전체를 이식하지 않고도 이 부분만
  깔끔하게 꺼내 쓸 수 있었습니다.
- `WaveWriter.cs`를 처음으로 실사용해보니, `Open(filename)`이 넘겨받은 경로에서
  파일명만 뽑아 `.wav`로 바꾼 뒤 `setting.other.WavPath`와 합쳐 실제 출력 경로를
  스스로 계산하는 방식이라는 걸 알게 됐습니다 (원곡 파일 경로를 넘기면 옆에
  `.wav`를 만드는 용도). 미리 만든 `.wav` 경로를 그대로 넘기면 "입력과 같은
  이름"으로 오인해 `_0` 접미사가 붙는 충돌 방지 로직이 걸리므로, VGM 원본 경로를
  그대로 넘기도록 스모크 테스트를 맞췄습니다.
- `Audio.VgmPlay`/`TrdVgmVirtualMainFunction`(원본, `MDPlayerx64/Audio.cs`)을 그대로
  포팅하지 않고, `MDSound.MDSound.Update(buffer, offset, sampleCount, frame)`가
  `frame` 콜백으로 `vgm.oneFrameProc`를 받는 것과 같은 핵심 패턴만 뽑아 새로 짰습니다.
  풀 버전은 SN76489/YM2612뿐 아니라 VGM이 지원하는 20여 가지 칩 전부를 담당하고
  페이드아웃/히요리미/MIDI 패스스루/실물 하드웨어까지 처리하는 라이브 플레이어용
  로직이라, 이번 스모크 테스트 범위(MDPlayer라는 프로젝트 이름에 걸맞게 세가
  메가드라이브의 핵심 칩 두 개)에는 과합니다 — 나머지 칩들은 `Audio` 클래스를
  본격적으로 이식할 때(또는 필요해질 때마다) 이 스모크 테스트에도 하나씩 추가하면
  됩니다.
- `log.cs`, `Tables.cs` 등과 마찬가지로 `WaveWriter.cs`도 이번에 처음 이식
  (Windows 의존성 없음, 그대로 복사).

돌리는 법:

```
cd macos/EngineSmokeTest
dotnet run -c Release -- <입력.vgm> [출력.wav]
```

지금은 SN76489/YM2612 두 칩만 배선되어 있습니다 (VGM 파일이 다른 칩만 쓴다면
"이 스모크 테스트는 SN76489/YM2612만 배선되어 있다"는 에러 메시지와 함께 종료).

이 스모크 테스트를 만드는 과정에서, VGM 로딩 + `Setting`/`ChipRegister`/`Vgm`/
`MDSound` 배선 로직을 `macos/MDPlayerCore/VgmEngine.cs`(`VgmEngine.Load(byte[] vgmBuf)`)
로 뽑아냈습니다 — WAV 렌더링(`EngineSmokeTest`)과 실시간 재생(`LivePlayer`, 아래)이
동일한 설정 코드를 공유하기 위해서입니다. 리팩터링 후 두 테스트 VGM으로 회귀
테스트를 돌려 콘솔 출력이 리팩터링 전과 완전히 동일함을 확인했습니다.

### ✅ CoreAudioOutput + LivePlayer — 실시간 오디오 출력 (완료, 실기에서 소리 확인)

`EngineSmokeTest`는 WAV 파일로만 렌더링했는데, 이번엔 실제로 스피커에서 소리가
나도록 macOS의 **Audio Queue Services**(`AudioToolbox.framework`)를 P/Invoke로
직접 붙였습니다.

- `macos/CoreAudioOutput/`: `AudioToolbox.framework`를 P/Invoke하는 순수 래퍼
  라이브러리 (`CoreAudioQueue.cs`). PortAudio/SDL2 같은 외부 설치가 필요 없는
  방식을 택했습니다 — Audio Queue Services는 모든 macOS에 기본 내장되어 있습니다.
  AUHAL/AudioUnit 렌더 콜백 방식보다 지연시간은 조금 더 크지만, 버퍼를 "다 썼으면
  채워달라"고 콜백으로 요청하는 pull 모델이라 `MDSound.MDSound.Update(buf, offset,
  count, frame)`의 시그니처와 자연스럽게 맞아떨어집니다.
  - `AudioStreamBasicDescription`/`AudioQueueBuffer`는 Apple 헤더(`AudioQueue.h`,
    `CoreAudioTypes.h`)에서 필드 순서/타입을 그대로 옮긴 `[StructLayout(LayoutKind.Sequential)]`
    구조체이고, 버퍼 내부 필드 오프셋은 손으로 계산하지 않고 `Marshal.OffsetOf<T>()`로
    구합니다 (.NET이 네이티브와 동일한 정렬 규칙을 따르므로).
  - 생성자에서 버퍼 N개(기본 4개)를 동기적으로 채워 큐에 넣어두고(`AudioQueueStart()`
    호출 전에), 이후 재생 중에는 CoreAudio가 다 쓴 버퍼를 콜백으로 돌려줄 때마다
    다시 채워 넣는 트리플(N-tuple) 버퍼링 구조입니다.
  - `DllImport`는 프레임워크 절대 경로(`/System/Library/Frameworks/AudioToolbox.framework/AudioToolbox`)를
    사용합니다 — .NET 버전에 상관없이 안정적으로 동작하는 방식입니다.
- `macos/LivePlayer/`: `VgmEngine.Load()` + `CoreAudioQueue`를 연결하는 콘솔 앱.
  `dotnet run -c Release -- <입력.vgm>`으로 실행하면 재생이 끝나거나 Ctrl+C를
  누를 때까지 스피커로 계속 소리를 냅니다.

이 작업은 Linux 샌드박스에서 작성되어 `dotnet build` 컴파일 검증만 가능했는데
(리눅스에는 `AudioToolbox.framework`가 아예 없음), 실제 Mac에서 `LivePlayer`를
돌려본 결과 **스피커로 소리가 정상적으로 나오는 것을 확인했습니다.** P/Invoke
구조체 레이아웃(`AudioStreamBasicDescription`/`AudioQueueBuffer`), 콜백 마샬링,
`Marshal.OffsetOf` 기반 오프셋 계산이 전부 실기에서 올바르게 동작한다는 뜻입니다.

돌리는 법 (Mac에서):

```
cd macos/LivePlayer
dotnet run -c Release -- <입력.vgm>
```

### ✅ MDPlayerUI — Avalonia GUI 첫 마일스톤 (완료, 실기 빌드/재생 확인)

`macos/MDPlayerUI/`에 최소 기능의 Avalonia 데스크톱 앱을 새로 만들었습니다:
"VGM 열기..." 버튼으로 실제 파일 선택 대화상자를 띄우고, 선택한 파일을
`VgmEngine.Load()` + `CoreAudioOutput.CoreAudioQueue`로 그대로 재생/정지하는
창(Window) 하나짜리 GUI입니다. 콘솔에서 돌리던 `LivePlayer`와 엔진/오디오
로직은 완전히 동일하고, 앞단만 GUI로 바뀐 것입니다.

- 패키지: `Avalonia`/`Avalonia.Desktop`/`Avalonia.Themes.Fluent`/`Avalonia.Fonts.Inter`
  12.1.1 (2026-08 기준 최신 안정 버전, 웹 검색으로 확인), `Avalonia.Diagnostics`는
  Debug 빌드에서만.
- 파일 선택은 구버전 `OpenFileDialog`가 아니라 현재 권장되는
  `TopLevel.StorageProvider.OpenFilePickerAsync(FilePickerOpenOptions)` 방식을
  사용했습니다.
- 재생/정지 로직은 전부 `Task.Run`으로 백그라운드 스레드에서 돌리고, UI 갱신만
  `Dispatcher.UIThread.InvokeAsync`로 되돌아와 처리합니다 (Avalonia UI 스레드가
  블로킹되지 않도록).

이 프로젝트는 리눅스 샌드박스에서 작성됐는데(`nuget.org` 접근이 막혀 있어
`dotnet restore`조차 한 번도 못 해본 상태로 동기화했습니다), 실제 Mac에서
빌드해보며 두 가지 문제가 나왔고 둘 다 해결했습니다:

- **NU1102** (`Avalonia.Diagnostics` 12.1.1을 찾을 수 없음) — 이 패키지는
  12.x로 릴리즈된 적이 없고 `nuget.org`에서 deprecated 처리되어 있었습니다
  (11.3.20에서 멈춤, 후속은 `AvaloniaUI.DiagnosticsSupport`). F12 DevTools용
  Debug 전용 패키지라 이번 마일스톤엔 필수가 아니어서 그냥 제거했습니다.
- **CS0709** (`'App': 정적 클래스 'Application'에서 파생될 수 없습니다`) —
  `App.axaml.cs`가 `namespace MDPlayer.UI`(=`MDPlayer`의 하위 네임스페이스)에
  있는데, `MDPlayerCore/CompatShims.cs`에 이미 `MDPlayer.Application`이라는
  static 클래스(WinForms `Application.ExecutablePath` 흉내용 shim)가 있어서
  C#의 상위 네임스페이스 우선 조회 규칙 때문에 `using Avalonia;`보다 그쪽이
  먼저 잡혔던 것입니다. `: Avalonia.Application`으로 명시적으로 고쳤습니다.

두 문제 모두 고친 뒤 실제 Mac에서 빌드 성공 + 창이 뜨고 파일 선택 → 재생 →
정지가 정상 동작하는 것까지 확인했습니다.

돌리는 법 (Mac에서):

```
cd macos/MDPlayerUI
dotnet run -c Release
```

### ✅ 칩 배선 확장 — SN76489/YM2612 → 13개 칩 (완료, 1개 칩 실제 오디오 검증)

`macos/MDPlayerCore/VgmEngine.cs`가 기존 SN76489/YM2612 두 칩뿐이었는데,
원본 `Audio.Init`의 칩 배선 블록(`MDPlayer/MDPlayerx64/Audio.cs`, 8760줄
근방)을 참고해서 11개 칩을 더 추가했습니다: YM2151(OPM, 아케이드/X68000),
YM2203(OPN), YM2608(OPNA, PC-98), YM2610(OPNB, Neo Geo), YM3812(OPL2),
YM3526(OPL), YMF262(OPL3), AY8910(PSG), YM2413(OPLL), K051649(SCC, 코나미),
SEGAPCM. 이제 SN76489/YM2612까지 총 13개 칩입니다.

- 원본과 달리 **칩당 소프트웨어 에뮬레이터 하나만, 단일 칩 인스턴스만** 배선합니다
  (원본은 칩별로 여러 에뮬레이터 백엔드 중 선택 + 실제 하드웨어 출력 + 듀얼 칩
  VGM까지 지원하는데, 이 포팅에서는 처음부터 실제 하드웨어 출력 자체가 범위 밖
  이라 — `CompatShims.cs`의 `RealChip`/`RC86ctlSoundChip`이 항상 no-op인 것과
  같은 맥락 — 이 부분은 계속 단순화해서 갑니다).
- `EngineSmokeTest`/`LivePlayer`/`MDPlayerUI`의 "어떤 칩을 쓰는 파일인지" 콘솔/
  상태 표시 로직도 `VgmEngineSession.DescribeActiveChips()`라는 공용 메서드로
  뽑아서, "SN76489/YM2612만 지원"이라는 오래된 메시지가 세 곳에 따로 남아있지
  않도록 정리했습니다.
- 새로 추가한 11개 칩 중 **YM2151 하나만** 실제로 손으로 만든 테스트 VGM
  (`testdata/ym2151-tone.vgm`)으로 검증했습니다 — 엔벨로프 제너레이터/KC·KF
  피치 인코딩/CONNECT 기반 오퍼레이터 라우팅까지 갖춘, 이번에 추가한 칩들 중
  가장 다른 코드 경로를 타는 칩이라 대표로 골랐습니다. 렌더링된 WAV를
  Python으로 열어 무음도 클리핑도 아니고(최대 진폭 4084/32767, 18427/18432
  샘플이 0이 아님) 그럴듯한 단일 주파수 진동(제로크로싱 추정 ~139Hz)이 있는
  것까지 확인했습니다. 나머지 10개 칩은 전부 동일한 단순 단일 인스턴스 배선
  패턴을 그대로 따르고 MDSound 쪽 에뮬레이터 클래스들은 이미 빌드 검증된
  상태라, 개별 테스트 파일은 만들지 않았습니다 — 실제 게임 VGM으로 검증하는
  다음 단계에서 자연스럽게 커버될 것으로 봅니다.

### ✅ .vgz(gzip 압축 VGM) 지원 (완료, 샌드박스 검증)

vgmrips.net 등에서 받는 실제 VGM 파일 대부분이 `.vgz`(gzip으로 압축된 `.vgm`)
형태라, 실제 파일로 검증하는 다음 단계 전에 먼저 필요한 작업이었습니다.

- `VgmEngine.Load()`에 `DecompressIfGzip()`을 추가해서, 파일 확장자가 아니라
  **gzip 매직 바이트(`0x1f 0x8b`)** 로 압축 여부를 판별하고 자동으로
  압축을 풉니다 — 확장자가 잘못 붙은 파일도 그냥 동작합니다. `EngineSmokeTest`/
  `LivePlayer`/`MDPlayerUI` 세 앱 모두 `VgmEngine.Load()`를 통하므로 별도
  수정 없이 전부 `.vgz`를 지원하게 됐습니다 (`MDPlayerUI`의 파일 선택
  대화상자 필터에만 `*.vgz` 패턴을 추가했습니다).
- 원본 Windows `Audio.cs`의 `Common.unzipFile`과 동일하게
  `System.IO.Compression.GZipStream`(순정 .NET BCL, Windows 의존성 없음)을
  그대로 사용했습니다 — 이식 작업이랄 것도 없이 그대로 재사용 가능했습니다.
- **샌드박스에서 완전히 검증 가능**했던 작업입니다 (macOS 전용 API가 전혀
  없으므로). `sn76489-tone.vgm`을 gzip 압축한 `testdata/sn76489-tone.vgz`를
  돌려서, 압축 해제 후 렌더링된 WAV가 압축 안 된 원본과 **바이트 단위로
  완전히 동일함**을 `cmp`로 확인했습니다.

### ✅ 칩 배선 2차 확장 — 13개 → 38개 칩, 전 칩 완료 (완료, 2개 칩 실제 오디오 검증)

"NES APU, OKIM6258 같은 유명한 PCM 음원이 빠졌다, VGM이 지원하는 모든 칩을 이번에
전부 추가해달라"는 요청에 따라, 원본 `Audio.Init`의 칩 배선 블록(`Audio.cs`
9226~10094줄)에 남아있던 나머지 25개 칩을 전부 `VgmEngine.cs`에 배선했습니다:
RF5C68/RF5C164(세가 CD PCM), PWM(32X), C140(남코), OKIM6258/OKIM6295(PCM/ADPCM,
이번 요청에서 콕 집은 칩), Y8950(OPL with ADPCM), YMF278B(OPL4, 옵션 웨이브테이블
ROM), YMF271, YMZ280B, DMG(게임보이), **NES APU**(이번 요청에서 콕 집은 칩,
DMC/FDS 서브유닛 포함), MultiPCM, uPD7759, K054539/K053260(코나미),
HuC6280(PC엔진), POKEY(아타리), QSound(캡콤), WSwan(원더스완), SAA1099,
ES5503, X1_010(세타), C352(남코), GA20(아이렘). 이제 SN76489/YM2612까지
포함해 **VGM 스펙이 정의하는 칩 38개 전부**가 배선되어 있습니다.

- OKIM6258/OKIM6295는 재생 중 VGM 칩 전용 커맨드로 자체 샘플레이트를 바꿀 수
  있는데, 원본의 `Audio.ChangeChipSampleRate`(정적 콜백, 전역 `Setting.outputDevice`
  참조)를 `VgmEngine`의 private static 메서드로 이식하면서 전역 대신
  `deviceSampleRate`를 매개변수로 받도록만 바꿨습니다 (이 포팅은 `Setting`이
  세션마다 독립된 인스턴스라 전역 참조가 안 맞음). `okim6258_set_srchg_cb`/
  `okim6295_set_srchg_cb`에 이 메서드를 가리키는 람다를 등록합니다.
- NES는 원본과 동일하게 **APU/DMC/FDS 세 개의 `MDSound.MDSound.Chip` 항목이
  하나의 `nes_intf` 인스턴스를 공유**합니다 — `Update` 델리게이트는 APU
  항목에만 연결하고(오디오를 실제로 뽑아내는 건 이거 하나뿐), DMC/FDS 항목은
  `Update` 없이 등록만 해서 `ChipRegister`가 해당 VGM 커맨드 바이트를 같은
  인스턴스로 라우팅할 수 있게만 합니다 (원본 주석에도 `//chip.Update = nes.Update;`
  로 명시적으로 꺼져있음).
- `DescribeActiveChips()`와 `vgm.init()`에 넘기는 `useChip` 배열도 25개
  칩 전부 포함하도록 갱신했습니다.
- 새로 추가한 25개 칩 중 **NES APU**를 YM2151에 이은 두 번째 실제 오디오
  검증 대상으로 골랐습니다 (이번 요청에서 명시적으로 이름이 나온 칩이라).
  펄스 채널 1을 듀티 10%/일정 볼륨으로 설정하고 타이머값 253(≈440.36Hz
  기대치)으로 손으로 채운 211바이트짜리 최소 VGM(`testdata/nes-apu-tone.vgm`)을
  만들어 렌더링한 뒤 Python으로 분석했습니다: 무음도 클리핑도 아니고(전체
  18432 샘플이 0이 아님, 최대 진폭 2490/32767), 두 값(655/2490) 사이를
  오가는 듀티 파형의 전환 시점을 세어 계산한 주기가 440.38Hz — 기대치
  440.36Hz와 거의 정확히 일치했습니다. 나머지 24개 칩은 YM2151 배치 때와
  같은 이유로(동일한 단순 단일 인스턴스 배선 패턴 + 이미 빌드 검증된 MDSound
  에뮬레이터 클래스) 개별 테스트 파일 없이 코드 리뷰 수준 확신에 의존합니다.
  OKIM6258/OKIM6295는 레지스터 쓰기만으로는 소리가 안 나고 ADPCM 데이터
  블록(`0x67` VGM 커맨드)까지 손으로 인코딩해야 해서 NES보다 훨씬 손이 많이
  가는 관계로 이번 라운드에서는 만들지 않았습니다.
- **테스트 픽스처를 만들며 발견한 버그성 동작 하나**: 이 코드베이스의
  `Vgm`(`Driver/vgm.cs`)은 VGM 헤더 0x04 필드("EOF 오프셋")를 **VGM 스펙과
  달리 절대 오프셋으로 직접 사용**합니다 (스펙: 필드값+4가 절대 끝 위치;
  이 코드: 필드값 자체가 절대 끝 위치, `+4`를 더하지 않음). 스펙대로
  `파일길이-4`를 넣으면 이 엔진의 EOF 체크가 실제 끝보다 4바이트 일찍
  발동해서 마지막 커맨드가 그 4바이트 창 안에 걸리면 통째로 씹힙니다
  (`nes-apu-tone.vgm`을 스펙대로 인코딩했더니 wait+end 커맨드가 통째로
  실행되지 않고 조용히 끊기는 걸 디버그 빌드로 확인). 기존 픽스처들
  (`sn76489-tone.vgm` 등)은 wait 커맨드가 이 경계보다 앞에서 이미 시작돼서
  우연히 문제가 안 됐을 뿐입니다. `nes-apu-tone.vgm`은 이 필드에 파일
  전체 길이를 그대로 넣어(스펙과 다르지만 이 엔진의 실제 동작에 맞춤)
  우회했습니다 — 진짜 vgmrips.net 등에서 받은 실제 VGM 파일도 이 4바이트
  차이 때문에 마지막 커맨드가 씹힐 수 있다는 뜻이라, 다음 "실제 VGM 파일로
  검증" 단계에서 염두에 둘 만한 잠재 버그입니다 (엔진 코드 자체를 고칠지는
  별도 판단 필요 — 원본 Windows `Audio.cs`도 동일한 방식일 가능성이 있어
  포팅 과정에서 새로 생긴 문제가 아닐 수 있습니다).

## 다음 단계 후보

1. **실제 VGM 파일로 검증**: 이제 `.vgz`까지 지원하고 VGM 스펙의 칩 38개가
   전부 배선되어 있으니, vgmrips.net 등에서 실제 게임 VGM을 받아
   돌려보고(라이선스/저작권 확인 후) 원본 Windows 빌드와 파형/사운드를
   비교해보는 게 다음 신뢰도 검증 단계입니다. YM2151/NES APU 두 칩을
   제외한 나머지는 아직 실제 오디오로 검증되지 않았고, 위에 적은 EOF
   오프셋 이슈도 실제 파일에서 재현되는지 확인이 필요합니다.
2. **VGM 이외의 음악 파일 포맷 지원**: MDPlayer가 지원하는 포맷은 VGM/VGZ뿐만이
   아닙니다 — `MDPlayerCore/Driver/`에 이미 이식되어 있는 것만 봐도 SID
   (`Driver/SID/**`), MXDRV(`Driver/MXDRV/*`), MNDRV(`Driver/MNDRV/*`),
   ZMS(`Driver/ZMS/**`), XGM/XGM2(`Driver/xgm.cs`/`xgm2.cs`)가 있고, 이
   드라이버들을 실제로 로드해서 재생하는 파일 포맷 감지/디스패치 로직
   (`AudioShim.cs`의 `GetMusic()`)은 아직 `NotImplementedException` 스텁
   상태입니다. `VgmEngine.cs`처럼 각 드라이버를 실제로 로드→재생하는
   경로를 만들고(포맷 감지, `baseDriver` 공통 인터페이스 활용), 최소
   하나씩 스모크 테스트로 검증하는 게 다음 큰 작업입니다 — VGM 칩 확장과는
   별도로 스코프를 잡아야 할 정도로 큰 작업이라 착수 전 별도 조사가
   필요합니다.
3. **MDPlayerUI 기능 확장**: 지금은 파일 하나 열기/재생/정지뿐입니다.
   재생 목록, 재생 시간 표시/탐색바, 볼륨 조절, 최근 파일 목록 같은 실사용에
   필요한 기본 기능을 추가할 수 있습니다.

## 라이선스 메모

MDSound는 GPLv3입니다 (`macos/MDSound/LICENSE.txt`). MDPlayer 본체도 동일 라이선스
계열로 알고 있으니 macOS 포크도 GPLv3를 유지하는 게 맞습니다 — 배포 전에 원본
LICENSE.txt 조건(특히 SCCI2 동봉 관련 별도 허가 조항)도 한 번 더 확인하세요.
