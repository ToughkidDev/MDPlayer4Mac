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

## 다음 단계 후보

1. **더 많은 칩 배선**: 지금 `EngineSmokeTest`는 SN76489/YM2612만 다룹니다.
   실제 게임 VGM(특히 아케이드/타사 콘솔 이식)을 재생해보려면 YM2151, YM2608,
   YM2610 등도 `Audio.VgmPlay`의 해당 칩 블록(각각 `MDSound.MDSound.Chip` 하나
   만드는 패턴)을 참고해서 추가해야 합니다.
2. **실제 VGM 파일로 검증**: 지금까지는 손으로 만든 최소 테스트 파일만
   썼습니다. vgmrips.net 등에서 실제 메가드라이브 게임 VGM을 받아 돌려보고
   (라이선스/저작권 확인 후) 원본 Windows 빌드와 파형/사운드를 비교해보는 게
   다음 신뢰도 검증 단계입니다.
3. 그 다음에야 오디오 출력 레이어(CoreAudio) 붙이기, UI(Avalonia) 시작하기로
   넘어가는 게 순서상 맞을 것 같습니다.

## 라이선스 메모

MDSound는 GPLv3입니다 (`macos/MDSound/LICENSE.txt`). MDPlayer 본체도 동일 라이선스
계열로 알고 있으니 macOS 포크도 GPLv3를 유지하는 게 맞습니다 — 배포 전에 원본
LICENSE.txt 조건(특히 SCCI2 동봉 관련 별도 허가 조항)도 한 번 더 확인하세요.
