# MDPlayer4Mac — 포팅 작업 노트

MDPlayer(Windows, WinForms, .NET 8-windows)를 macOS로 옮기는 작업의 진행 상황과
다음 할 일을 정리한 문서입니다. 원본 Windows 소스(`MDPlayer/`)는 건드리지 않고,
이 `macos/` 폴더 아래에 크로스플랫폼(net8.0, `-windows` 접미사 없음) 프로젝트를
새로 만들어가는 방식으로 진행합니다.

## 현재 상태 (2026-08-20, 음악 파일 포맷 13개 지원 + SN76489/YM2612 칩 채널 표시계 실기 검증 완료 + 남은 모든 칩 표시계 순차 구현 진행 중 — YM2151/AY8910/S5B/YM2413/YM3526/YM3812/Y8950/YMF262/YMF278B/YM2203/YM2608/YM2609/YM2610/YMF271/NESDMC/FDS/MMC5/VRC6/VRC7/N106/DMG/HuC6280/K051649/C140/C352/GA20/K053260/K054539/MegaCD(RF5C164)/MpcmX68k/MultiPCM 완료 — NES 계열 + WF 계열 전체 완료, PCM 계열 진행 중(8/15), 실기 검증 대기)

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

### ✅ 음악 파일 포맷 확장 — VGM 이외 13개 포맷 지원 (완료, VGM 회귀 + 신규 2개 포맷 실제 오디오 검증)

"MDPlayer가 지원하는 모든 음악 파일 포맷과 모든 칩을 이번에 전부 추가해달라"는
요청에 따라, `VgmEngine.cs`(VGM 전용)를 일반화한 `MusicEngine.cs`를 새로 만들어
VGM/VGZ 포함 총 **13개 포맷**을 하나의 진입점(`MusicEngine.Load(buf, fileNameHint)`)
으로 로드할 수 있게 했습니다: VGM/VGZ, XGM/XGM2(세가 제네시스), SID(코모도어64),
MND(PC-98), ZMS/ZMD(X68000 "Zmusic"), MDX/MDR(X68000 MXDRV), **NSF**(NES/패미컴 —
이번 요청에서 콕 집은 NES APU 관련 포맷), GBS(게임보이), HES(PC엔진), S98(아케이드/
PC-98 레지스터 덤프), AY(ZX 스펙트럼), ZGM(니치 포맷).

- **`MusicEngineSession`**(구 `VgmEngineSession`)이 이제 모든 포맷의 공통 반환
  타입입니다. `Driver` 필드가 `baseDriver` 공통 인터페이스 타입으로 바뀌었고,
  새 `RenderSamples` 델리게이트 필드가 추가됐습니다 — 대부분의 포맷은 그냥
  `mds.Update(buf, off, count, driver.oneFrameProc)`을 감싸지만, SID/NSF/MDX
  세 포맷은 이 델리게이트가 각 드라이버 자신의 `Render()`를 직접 호출하도록
  다르게 배선됩니다 (아래 참고). **`EngineSmokeTest`/`LivePlayer`/`MDPlayerUI`
  세 프론트엔드 모두 `mds.Update()`를 직접 부르던 걸 `session.RenderSamples()`
  호출로 바꿨습니다** — 처음엔 `RenderSamples` 필드만 추가하고 이 호출부
  세 곳을 안 고쳐서, SID/NSF/MDX가 항상 무음으로 나가는 버그가 있었는데
  검증 도중 발견해서 고쳤습니다.
- **SID/NSF/MDX는 `MDSound.MDSound.Chip.Update()`/`ChipRegister` 레지스터
  쓰기 경로를 아예 안 씁니다.** 원본 `Audio.cs`의 재생 루프
  (`TrdVgmVirtualMainFunction`)를 다시 읽어보니 `DriverVirtual is nsf`/
  `is Driver.SID.sid`/`is Driver.MXDRV.MXDRV`일 때 `mds.Update()` 대신
  각 드라이버의 `Render()`를 직접 호출하는 특수 분기가 있었습니다 — SID는
  이미 이 패턴대로 포팅되어 있었는데, **NSF도 원래 `MDSound.nes_intf`(VGM의
  NES 배선이 쓰는 것과 다른, 완전히 별개의 칩 인스턴스)를 배선하도록 짰다가
  이 특수 분기를 발견하고 다시 짰습니다** — `nsf.cs`는 자기 전용 6502 CPU +
  APU/DMC/FDS/확장음원 에뮬레이터를 `chipRegister.nes_cpu`/`nes_apu`/... 필드에
  직접 만들어서(`nsfInit()`) 실제 6502 머신 코드를 실행하며 오디오를
  만들어내고, `cAPU`/`cDMC`/... 필드는 `Render()`가 각 서브칩 볼륨을 읽는
  용도로만 쓰입니다 (Update 델리게이트는 하나도 안 붙음). **MDX/MDR도
  마찬가지**입니다 — X68000 IOCS 사운드 드라이버 전체(`MDSound.NX68Sound.
  X68Sound`/`sound_iocs`, `ym2151_x68sound` 인스턴스 하나)가 OPM+PCM8을
  통째로 자체 렌더링하고, `MXDRV.Render(buffer, offset, 2)`를 원본과
  동일하게 2샘플(1프레임)씩 반복 호출해야 합니다 (`MXDRV.cs`의 내부
  `OneFrameProc2` 타이머 콜백이 이 호출 단위에 맞춰져 있음). 배선용
  `MDSound.MDSound.Chip` 항목 하나는 `Update`/`Start`/`Stop`/`Reset`을
  전부 `null`로 남긴 채로만 등록합니다 (원본도 동일).
- **YM2151을 쓰는 모든 새 포맷(MND/ZMS/MDX/S98)에서 `SamplingRate` 버그를
  하나 잡았습니다**: OPM의 내부 샘플링 레이트는 `Clock/64`인데(원본
  `Audio.cs`의 `MdxPlay`/`MndPlay`/`ZmdPlay`가 전부 `chip.SamplingRate =
  (UInt32)chip.Clock / 64;`로 명시), 처음엔 출력 디바이스 샘플레이트를 그대로
  넣어서 리샘플링 소스 레이트가 틀려 음정/속도가 왜곡되는 버그가 있었습니다.
  `VgmEngine.cs`의 VGM YM2151 배선(이미 검증됨)과 대조해서 발견/수정했습니다.
  같은 이유로 YM2608(OPNA)도 고정 `55467`이어야 하는데 출력 샘플레이트를
  넣고 있던 걸 MND/S98에서 같이 고쳤습니다.
- **MND/ZMS는 mpcmpp 드라이버 역참조 필드(`driver.mpcmpp`/`driver.mpcmtype`)를
  안 채우고 있어서 ADPCM 레지스터 쓰기가 갈 곳이 없는 버그**도 있었습니다 —
  원본 `MndPlay`/`ZmdPlay`가 `((Driver.MNDRV.mndrv)DriverVirtual).mpcmpp = mpcmpp;
  mpcmtype = 1;` 식으로 역참조를 반드시 세팅하는 걸 보고 맞춰 채웠습니다.
- **Shift-JIS(코드페이지 932) 디코딩이 이 포팅에서는 항상 예외를 던지는
  버그**를 NSF 테스트 픽스처를 만들다 발견했습니다 — `log.cs`가
  `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`를 부르긴
  하는데 `#if X64`로 감싸져 있고, 이 크로스플랫폼 포팅은 `X64`를 정의하지
  않아서 등록이 그냥 통째로 스킵됩니다(그리고 `log` 클래스의 정적 생성자가
  먼저 안 돌면 등록 자체가 안 일어남). NSF/S98/MXDRV/MNDRV 전부
  `Encoding.GetEncoding(932)`를 직접 호출하는 코드가 있어서, 제목/아티스트
  태그가 있는 파일은 전부 이 문제를 겪었을 것입니다. `common.cs`에
  `[ModuleInitializer]`로 등록 메서드를 추가해서 어셈블리 로드 시 무조건
  한 번 실행되게 고쳤습니다 (특정 클래스가 먼저 쓰이는 순서에 의존하지 않음).
- **포맷별 칩 배선**: XGM/XGM2는 YM2612+SN76489 고정(세가 제네시스 하드웨어
  클럭). SID는 칩 배선이 전혀 없이 libsidplayfp 기반 자체 렌더링만 씀. MND는
  YM2151+YM2608+mpcmpp(X68000 ADPCM 변형 중 기본값 하나만 채택). ZMS/ZMD는
  YM2151+mpcmpp(실제로는 `Driver/ZMS/nise68/` 안의 68000 CPU+Human68k OS
  에뮬레이터 전체가 Zmusic 드라이버 프로그램을 돌리지만, MDSound 레벨
  칩셋은 이 두 개뿐). NSF는 APU/DMC/FDS/MMC5/N106/VRC6/VRC7/FME7 8칸을
  전부 만들지만 실제 오디오는 전용 6502 CPU 실행으로 나옴. GBS/HES는 이미
  VGM 경로에서 검증된 `MDSound.gb`/`MDSound.Ootake_PSG`를 그대로 재사용.
  S98은 VGM처럼 파일이 선언하는 칩 목록(`S98.cs`의 `S98Info.DeviceInfos`,
  `DeviceType` 1/2/3/4/5/6/7/8/9/15/16)을 동적으로 배선. ZGM은 원본
  `ChipFactory.Create()` 자체가 YM2609 말고는 전부 미구현(`null` 반환)이라
  YM2609만 배선하는 게 이 포팅이 새로 좁힌 게 아니라 업스트림 자체의 한계.
  AY는 AY8910+ZXBeep 고정(스펙트럼 클럭 1789773/2).
- **검증**: 기존 VGM 5개 픽스처가 새 `MusicEngine.Load`→`VgmEngine.Load`
  경로로도 동일하게(동일 샘플 수/파형) 재생되는 걸 재확인했고, 새로
  **S98**(`testdata/s98-sn76489-tone.s98`, 60바이트 — SN76489 440Hz 톤,
  헤더+디바이스테이블+레지스터 덤프 커맨드를 직접 인코딩)과 **NSF**
  (`testdata/nsf-apu-tone.nsf`, 155바이트 — 진짜 6502 머신코드로 APU 펄스
  채널1을 설정하는 init 루틴 + no-op play 루틴)를 손으로 만들어 렌더링/분석
  했습니다: 둘 다 무음이 아니고(전체 샘플 대부분 0이 아님), 파형 전환
  주기로 역산한 주파수가 S98은 440.40Hz, NSF는 440.47Hz로 목표 440Hz와
  거의 정확히 일치했습니다. NSF는 CPU 트레이스 로그로 init 루틴의 레지스터
  쓰기(`$4015`/`$4001`/`$4000`/`$4002`/`$4003`)가 의도한 순서/값대로
  전부 실행되는 것도 직접 확인했습니다.
  나머지 포맷(XGM/XGM2/SID/MND/ZMS/ZMD/MDX/MDR/GBS/HES/AY/ZGM)은 개별
  테스트 픽스처 없이 코드 리뷰 + 위에 적은 버그 수정 수준의 확신에 의존합니다
  (특히 SID/MDX는 `Render()` 우회 경로 자체가 맞게 배선됐는지가 핵심 리스크).
- **AY는 샌드박스에서는 빌드 검증이 안 됐지만, 실제 Mac에서는 빌드 확인
  완료**입니다 — `Driver/AY/AY.cs`가 실제 `Konamiman.Z80dotNet` NuGet
  패키지(진짜 Z80 CPU 에뮬레이터, `Z80Processor`/`IZ80Registers` 타입)를
  쓰는데, 이 클라우드 샌드박스는 nuget.org에 접근할 수 없어서 최소 스캇치
  스텁(`IMemory`만 구현)으로만 나머지 코드를 검증했고 `AY.cs`/`port.cs`
  자체는 문법 확인 수준이었습니다. 이후 사용자가 실제 Mac에서
  `MDPlayerCore`/`LivePlayer`/`MDPlayerUI`/`EngineSmokeTest` 4개 프로젝트를
  전부 `dotnet build -c Release`로 빌드해 진짜 Z80dotNet 패키지가 정상
  restore/컴파일되는 것을 확인했습니다 (경고는 NU1701 프레임워크 불일치 등
  기존 패턴뿐, 에러 0). 다만 **AY 파일을 실제로 재생해서 소리를 들어본
  적은 아직 없습니다** — 검증된 건 빌드 성공까지고, AY8910+ZXBeep 배선의
  오디오 정확성 자체는 여전히 미검증입니다.
- **실기(real Mac) 오디오 검증**: 위 빌드 확인에 이어, 새로 추가한 두
  픽스처(S98 SN76489 톤, NSF APU 톤)를 `LivePlayer` CLI로 실제 Mac
  스피커에서 재생해 사용자가 직접 듣고 확인했습니다("전부 성공이야") —
  샌드박스의 Python WAV 분석(무음 아님/파형 주파수 일치)에 더해, 실제
  오디오 출력 장치를 통한 청취 확인까지 끝난 것입니다. 이어서
  `MDPlayerUI` GUI로도 파일 열기 → 포맷/칩 정보 표시 → 재생 → 정지 버튼까지
  전체 플로우를 테스트했고, 기존 VGM 픽스처도 같은 세션에서 함께 확인해
  회귀가 없음을 재확인했습니다("전부 정상이야"). 나머지 10개 포맷
  (XGM/XGM2/SID/MND/ZMS/ZMD/MDX/MDR/GBS/HES/AY/ZGM)은 아직 실제 파일로
  재생 테스트하지 않았습니다.
- `EngineSmokeTest`/`LivePlayer`/`MDPlayerUI` 세 프론트엔드 모두
  `MusicEngine.Load(buf, fileName)`을 쓰도록(구 `VgmEngine.Load(buf)`)
  갱신했고, `MDPlayerUI`의 파일 열기 다이얼로그 필터도 13개 포맷 확장자를
  전부 받도록(`*.vgm`/`*.vgz`/`*.xgm`/`*.xgz`/`*.sid`/`*.mnd`/`*.zms`/
  `*.zmd`/`*.mdx`/`*.mdr`/`*.nsf`/`*.gbs`/`*.hes`/`*.s98`/`*.ay`/`*.zgm`)
  넓혔습니다.

### 🚧 칩 채널 표시계(visualizer) — SN76489/YM2612 실기 검증 완료, 남은 ~12개 칩 순차 구현 중 (YM2151/AY8910/S5B/YM2413/YM3526/YM3812/Y8950/YMF262/YMF278B/YM2203/YM2608/YM2609/YM2610/YMF271/NESDMC/FDS/MMC5/VRC6/VRC7/N106/DMG/HuC6280/K051649/C140/C352/GA20/K053260/K054539/MegaCD(RF5C164)/MpcmX68k/MultiPCM 완료 — NES 계열 8종 + WF 계열 2종 전체 완료, PCM 계열 8/15 진행 중, 실기 검증 대기)

MDPlayer의 정체성이라 할 수 있는 "칩 채널 표시계"(LED 볼륨미터 + 미니 건반 +
팬 인디케이터, 원본 Windows판의 `form/KB/**` 약 40개 창) 재현 작업입니다.
SN76489(PSG)와 YM2612(FM, 오퍼레이터 파라미터 표 포함)가 실기에서 정상 동작
확인됐고("정상적으로 작동하고 있어"), 이제 남은 모든 칩(~22개)에 대해서도
같은 패턴(칩별 `DrawBuffXxx.cs` + `XxxVisualizer.cs`, `ChipRegister` 필드
직접 참조, 자체 스프라이트 로딩)으로 표시계를 계속 이식하는 중입니다.
YM2151(OPM, 8채널 FM)/AY8910(PSG/SSG)/S5B(Sunsoft FME-7)/YM2413(OPLL)/
YM3526(OPL/OPL1)/YM3812(OPL2)/Y8950(MSX-AUDIO FM+ADPCM)/YMF262(OPL3)/
YMF278B(OPL4)/YM2203(OPN)/YM2608(OPNA)/YM2609(가상 "듀얼 OPNA")/
YM2610(OPNB, Neo Geo)/YMF271(OPX)/NESDMC(NES/패미컴 내장 APU)/
FDS(패미컴 디스크 시스템 확장 음원)/MMC5(NES 카트리지 매퍼 확장 음원)/
VRC6(코나미 NES 카트리지 매퍼 확장 음원)/VRC7(코나미 NES 카트리지 매퍼,
OPLL 기반 FM 음원)/N106(남코 106/163, 웨이브테이블 최대 8채널)/DMG(오리지널
게임보이 4채널 사운드)/HuC6280(PC엔진/터보그래픽스-16 내장 PSG, 6채널)/
K051649(코나미 SCC, 5채널 웨이브테이블)/C140(남코 System 2/21/NA-1용
24비트 PCM 샘플 재생 칩, 24채널)/C352(남코의 후기 32채널 쿼드 출력(전방
L/R + 후방 L/R) PCM 샘플 재생 칩)/GA20(아이렘 GA20, 4채널 PCM 샘플 재생
칩)/K053260(코나미 4채널 PCM 샘플 재생 칩, 여러 코나미 아케이드 기판에서
Z80과 짝을 이룸)/K054539(코나미 8채널 PCM 샘플 재생 칩, 채널당
피치·리버브 딜레이/깊이·스테레오 팬 지원)/MegaCD(세가 메가 CD/세가 CD
내장 RF5C164 8채널 PCM 샘플 재생 칩 — 아케이드용 독립 RF5C68과 사촌뻘로
같은 `MDSound.scd_pcm` 클래스를 공유)/MpcmX68k(X68000의 MPCM ADPCM/PCM
샘플 재생 서브시스템, ZMS/MND 시퀀스 음악 포맷용 — 데이터 접근 구조가
지금까지의 다른 모든 칩과 근본적으로 달라 생성자 시그니처 예외를 둔
첫 사례)/MultiPCM(야마하 YMW258-F, 세가 아케이드/콘솔 기판에서 널리
쓰인 28채널 웨이브테이블/PCM 샘플 재생 칩 — 채널 배지를 그리지 않는
원본의 별난 동작을 그대로 보존)가 이번 라운드에서 완료됐습니다 — 이로써
NES 계열 8종(NESDMC/FDS/MMC5/VRC6/VRC7/N106/DMG + 이미 완료된
S5B(FME-7))과 WF 계열 2종(HuC6280/K051649)이 모두 끝났고, PCM 계열
(~15종)도 C140/C352/GA20/K053260/K054539/MegaCD/MpcmX68k/MultiPCM
8종째까지 진행했습니다.

- **MegaCD(세가 메가 CD/세가 CD 내장 RF5C164 8채널 PCM 샘플 재생 칩):
  이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본 `frmMegaCD.cs`를
  그대로 포팅 — 채널 8개, 채널당 8px 행 하나씩 키보드/노트 표시, 독립된
  L/R 볼륨 LED 바, 바이트로 압축된 2타일 팬 아이콘, 채널 배지가
  나열됩니다. **데이터 소스**: `chipRegister.GetRf5c164Register(chipId)`
  (새 한 줄짜리 포워딩 게터 — 이 포트에서 아홉 번째로 새로 추가한 게터.
  `mds.ReadRf5c164Register`를 감싸기만 하면 됐는데, K053260 때와 같은
  이유로 — 이 포트의 `ChipRegister`가 `mds`를 private으로 유지하기
  때문에 — 래퍼가 필요했습니다)로 `MDSound.scd_pcm.pcm_chip_`을 읽습니다.
  **클록 처리 불필요**: 지금까지 이식한 다른 모든 PCM 계열 칩(C140/C352/
  GA20/K053260/K054539)과 달리 `frmMegaCD.cs`의 `searchRf5c164Note`는
  어떤 `Audio.ClockXxx` 프로퍼티로도 나누지 않고, 원시 `Step_B` 주파수
  레지스터 값을 `pcmMulTbl` 기반 테이블과 직접 비교합니다. 또한 이
  칩은 지금까지 이식한 PCM 계열 `search*Note` 중 처음으로 진짜 전역
  최소값 탐색(`if (m > a) { m = a; n = i; }`)을 수행합니다 — C140/C352/
  GA20/K053260/K054539가 공유하는 "freq > a일 때마다 계속 덮어쓰는" 별난
  동작이 아니라, 원본이 실제로 정직한 최근접 탐색을 하길래 그대로
  정직하게 포팅했습니다. **의도적 단순화**: `tp`는 0 고정,
  `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **MpcmX68k(X68000의 MPCM ADPCM/PCM 샘플 재생 서브시스템, ZMS/MND(mndrv)
  시퀀스 음악 포맷이 사용): 이번 라운드에서 신규 구현, 아직 실기
  미검증. 지금까지 이식한 칩 중 데이터 접근 구조가 완전히 다른
  첫 사례입니다.** 지금까지의 모든 칩은 `ChipRegister.GetXxxRegister
  (chipId)`를 거쳐 상태를 읽었지만, 원본 `frmMpcmX68k.cs`는 그렇지 않고
  현재 재생 중인 시퀀서 드라이버(`Audio.DriverVirtual`을 `ZMS` 또는
  `mndrv`로 캐스팅)의 `mpcmSt`/`mpcm`/`mpcmpp` 필드를 직접 읽습니다.
  이 포트의 `AudioShim.Audio.DriverVirtual`은 실제 재생 경로에 연결되지
  않은 죽은 스텁이라서, 대신 `MusicEngineSession.Driver`(`MDPlayer.
  baseDriver`)를 생성자에 직접 넘기는 방식을 택했습니다. **아키텍처
  예외**: 그래서 `MpcmX68kVisualizer`의 생성자는 다른 모든
  `XxxVisualizer.cs`가 쓰는 표준 `(ChipRegister chipRegister, uint
  clockHz)` 모양이 아니라 `(baseDriver driver)`입니다 — 원본이 클록 값을
  전혀 읽지 않기도 하고, 애초에 `ChipRegister`를 거치지 않는 구조이기
  때문에 의도적으로 다르게 설계했습니다(사용자에게 묻지 않고 원본 코드의
  실제 접근 방식을 근거로 독자적으로 판단). 내부에 원본의
  `GetMPCMInstance`를 그대로 미러링한 `GetMpcmInstance` 헬퍼가
  `driver is ZMS.ZMS`/`driver is MNDRV.mndrv` 패턴 매칭으로 분기합니다.
  **enmInstrumentType 예외**: 원본에도, 이 포트에도 별도의 `MpcmX68k`
  enum 값은 없고 `mpcmpp` 하나뿐이라(이 포트의 `MusicEngine.cs`
  `LoadMnd`/`LoadZms`는 항상 `mpcmtype=1`(mpcmpp)을 배선하고 원시
  `mpcmX68k` 모델은 실제 재생 경로에서 절대 쓰이지 않음),
  `MainWindow.axaml.cs`는 이 표시계의 표시 여부를
  `enmInstrumentType.mpcmpp` 하나로 판단합니다 — 다만 두 브랜치
  (`mpcm`/`mpcmpp`) 모두 원본 그대로 보존해서 이식했습니다(죽은 코드지만
  원본 충실도를 위해 남겨둠). 16채널, 채널당 8px 행 하나씩 채널 배지,
  원시 단일 타일 팬 아이콘, 키보드/노트 표시(명시적 x/폰트-x 분리,
  K053260과 같은 `KeyBoardXYFX` 모양), 샘플 포인터/크기/시작/끝/카운트/
  피치(32비트) 16진 표시와 PCM 타입(바이트) 16진 표시, 샘플레이트/포맷
  문자열 표시(원본 43개 문자열 테이블 `frqStr` 그대로 포팅), 독립된 L/R
  볼륨 LED 바가 나열됩니다. **탐색 함수 별난 동작 보존**: `search
  MPCMX68kNote`/`searchMPCMppNote` 둘 다 C140/C352/GA20/K053260/K054539가
  공유하는 "freq > a일 때마다 계속 덮어쓰는" 비-전역-최소 탐색 별난
  동작과 `n - 5 + 12` 반환 오프셋을 그대로 보존했습니다. **좌표 이식
  주의**: 원본이 `(x + n) * 4` 형태로 쓴 리터럴은 그 곱셈까지 그대로
  옮겼고(예: 주소 필드들은 `(67 + 9*k) * 4`), `32`/`40`처럼 원본이 이미
  raw 픽셀 값으로 쓴 좌표는 곱하지 않고 그대로 옮겼습니다 — 원본 소스의
  좌표 표현 방식 차이를 그대로 존중.
- **MultiPCM(야마하 YMW258-F, 세가 아케이드/콘솔 기판에서 널리 쓰인
  28채널 웨이브테이블/PCM 샘플 재생 칩): 이번 라운드에서 신규 구현,
  아직 실기 미검증.** 원본 `frmMultiPCM.cs`를 그대로 포팅 — 채널 28개,
  채널당 8px 행 하나씩 바이트로 압축된 2타일 팬 아이콘, "키온" 플래그
  아이콘, 악기 번호, 12비트 주파수 표시, "TL 보간" 플래그 아이콘,
  TL/LFO 주파수/PLFO/ALFO, 24비트 샘플 시작/16비트 끝/16비트 루프 주소
  표시, LFO 비브라토/어택/디케이1/디케이2/디케이 레벨/릴리스/키 레이트
  스케일/AM 16진 표시, 독립된 L/R 볼륨 LED 바(원시 픽셀 좌표 오버로드
  `VolumeXY`), 키보드/노트 표시가 나열됩니다. **데이터 소스**:
  `chipRegister.getMultiPCMRegister(chipId)`를 직접 호출합니다 — 이미
  `ChipRegister`의 public 메서드라(Windows판 `Audio.GetMultiPCMRegister`
  도 `chipRegister.getMultiPCMRegister`를 그대로 포워딩) 새 게터가
  필요 없었습니다. **클록 처리 불필요**: 원본 `screenChangeParams`는
  클록 값을 전혀 읽지 않습니다(원본에 있던 `searchMultiPCMNote`는 전체가
  주석 처리된 죽은 코드이자 실제로 호출되지도 않아 — 노트 계산은 옥타브/
  피치 레지스터에서 직접 이뤄짐 — 이 포트에서는 아예 이식하지 않고
  생략했습니다). **채널 배지 미표시라는 원본의 별난 동작 보존**: 원본
  `screenDrawParams`는 `ChMultiPCM`/`ChMultiPCM_P` 호출이 통째로 주석
  처리돼 있어서 — 즉 이 칩은 원본에서도 채널 배지를 프레임마다 다시
  그리지 않습니다 — 이 포트도 채널 배지를 그리지 않습니다("고쳐서" 넣지
  않음). **키보드 오버로드 별난 동작 보존**: `KeyBoardToMultiPCM`은
  다른 칩들의 `KeyBoard`/`KeyBoardXYFX`와 달리 옥타브 범위 검사가
  `< 10`이 아니라 `< 8`이고(이 칩의 노트 범위가 7옥타브로 클램프되기
  때문), 음수 노트로 갈 때만 공백 텍스트를 지우는 등(다른 칩처럼
  무조건 먼저 지우고 조건부로 덮어쓰는 게 아니라) 구조 자체가 다른데,
  모두 원본 그대로 포팅했습니다.
- **K054539(코나미 8채널 PCM 샘플 재생 칩): 이번 라운드에서 신규 구현,
  아직 실기 미검증.** 원본 `frmK054539.cs`를 그대로 포팅 — 같은 배경
  이미지 안에 특이한 2단 레이아웃을 씁니다: 1~8행(위쪽)에는 키보드/노트,
  2타일 팬 아이콘, 독립된 L/R 볼륨 LED 바가, 10~17행(아래쪽, 같은 8개
  채널)에는 시작/루프 주소, 피치, 리버브 딜레이, PCM 종류, 루프/역재생
  플래그, 볼륨, 키온/키오프 플래그의 16진 표시가 나옵니다. **데이터
  소스**: `chipRegister.GetK054539State(chipId)`를 직접 호출합니다 —
  이미 `ChipRegister`의 public 메서드라(Windows판 `Audio.
  GetK054539State`도 `mds.ReadK054539Status`를 그대로 포워딩) 이 칩도 새
  게터가 필요 없었습니다. **클록 처리**: 원본은 `Audio.ClockK054539`
  (GA20의 `ClockGA20`처럼 기본값이 0)로 나누는데, 이 포트에선 `clockHz`
  생성자 매개변수를 대신 쓰되 폴백은 0 그대로 두었습니다(원본의 기본값과
  동일하게). **원본 그대로 보존한 별난 동작**: C140/C352/GA20/K053260과
  마찬가지로 `searchK054539Note`의 탐색 루프도 전역 최소 거리 대신
  `hz > a`일 때마다 `(m, n)`을 계속 덮어씁니다 — 그대로 포팅. 반환값에
  `+ 1` 오프셋이 붙는 것도(C140의 `+1`과 마찬가지로) 지금까지 이식한
  `search*Note` 계열 중 이 칩만의 특징이라 보존했습니다. 팬 레지스터
  바이트를 15개짜리 조회 테이블(`pantbl`)로 재매핑하기 전에 두 바이트
  범위(0x81-0x8f 또는 0x11-0x1f) 중 하나로 클램프하고, 둘 다 아니면
  하드코딩된 중간값 `0x18 - 0x11`로 대체하는 로직도 그 어색한 리터럴
  표현식 그대로 포팅했습니다. **의도적 단순화**: `tp`는 0 고정(원본 자체의
  `screenInit`도 `bool K054539Type = false;`로 무조건 고정), `screenInit`의
  미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **K053260(코나미 4채널 PCM 샘플 재생 칩): 이번 라운드에서 신규 구현,
  아직 실기 미검증.** 원본 `frmK053260.cs`를 그대로 포팅 — 채널 4개,
  채널당 8px 행 하나씩 주파수/뱅크/시작주소/크기/팬/볼륨 16진 표시,
  온/오프 플래그 아이콘 4개(재생/방향/루프/PPCM), 독립된 단일 타일 L/R
  팬 아이콘(C140/C352의 바이트-압축 2타일 `PanType2`와 달리 L/R을 각각
  별개의 `PanType5` 호출로 그림), 독립된 L/R 볼륨 LED 바(원시 픽셀 좌표
  오버로드 `VolumeXY1` 사용), 그리고 명시적 x/폰트-x 분리 오버로드인
  `KeyBoardXYFX`(지금까지 이식한 다른 모든 칩의 행-인덱스 또는 고정-x
  오버로드와 다름)로 그리는 키보드/노트 표시가 있습니다. **데이터 소스**:
  `ChipRegister.GetK053260Register`(새 한 줄짜리 포워딩 게터 — 이
  포트에서 여덟 번째로 새로 추가한 게터. `mds.getK053260State`를 감싸기만
  하면 됐는데, Windows판 `Audio.cs`는 `mds`를 정적 필드로 들고 있어
  직접 호출하지만 이 포트의 `ChipRegister`는 `mds`를 private으로 유지하기
  때문에 `GetHuC6280Register`/`GetDMGRegister`처럼 래퍼가 필요했습니다)로
  `MDSound.K053260.k053260_state`를 읽습니다. **클록 처리**: 원본은
  `Audio.ClockK053260`(기본값 3579545)로 나누는데, 이 포트에선 다른
  칩들과 같은 패턴으로 `clockHz` 생성자 매개변수(0이면 3579545 기본값
  폴백)를 대신 사용합니다. **원본 그대로 보존한 별난 동작**: C140/C352/
  GA20과 마찬가지로 `searchNote`의 탐색 루프도 전역 최소 거리 대신
  `freq > a`일 때마다 `(m, n)`을 계속 덮어씁니다 — 그대로 포팅. 반환값에
  `Math.Min(Math.Max(n - 2, 0), 95)` 클램프/오프셋이 붙는 것도 지금까지
  이식한 `search*Note` 계열 중 이 칩만의 특징이라 그대로 보존했습니다.
  **의도적 단순화**: `tp`는 0 고정(원본 자체도 `screenInit`/
  `screenDrawParams`에서 항상 리터럴 0을 넘기므로 애초에 UI 설정을 읽지
  않음), `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **GA20(아이렘 GA20, 4채널 PCM 샘플 재생 칩): 이번 라운드에서 신규 구현,
  아직 실기 미검증. 지금까지의 PCM 계열 칩 중 가장 단순한 구조입니다.**
  원본 `frmGA20.cs`를 그대로 포팅 — 채널 4개뿐이고 채널당 8px 행 하나씩,
  실시간 샘플 시작/종료/재생 위치 주소(20비트)와 주파수·볼륨(바이트) 16진
  표시, 키보드/노트 표시, 단일 원시 픽셀 좌표 볼륨 LED 바, 채널 배지가
  전부입니다. **데이터 소스**: `chipRegister.GetGA20State(chipId)`/
  `GetGA20KeyOn(chipId)`를 직접 호출합니다 — 둘 다 이미 `ChipRegister`의
  public 메서드라(Windows판 `Audio.GetGA20State`/`GetGA20KeyOn`도 동일한
  호출을 그대로 포워딩) 이 칩도 새 게터가 필요 없었습니다. **클록 처리**:
  원본은 `Audio.ClockGA20`(다른 모든 칩과 달리 기본값이 0 — GA20은 이
  클록을 명시적으로 세팅하는 포맷에서만 등장하기 때문)로 나누는데, 이
  포트에선 `clockHz` 생성자 매개변수를 대신 쓰되, 다른 칩들과 달리
  0이어도 0으로 그대로 두는 폴백을 채택했습니다(즉 실제로 0이면 원본과
  동일하게 0-나누기 상황이 재현됩니다 — 실전에서는 발생하지 않는 경우라
  임의의 대체값을 만들어내지 않았습니다). **원본 그대로 보존한 별난
  동작**: C140/C352와 마찬가지로 `searchGA20Note`의 탐색 루프도 전역 최소
  거리 대신 `hz > a`일 때마다 `(m, n)`을 계속 덮어씁니다(로컬 변수 `m`은
  대입만 되고 비교엔 안 쓰임) — 그대로 포팅. 또한 채널 배지는 전용 함수
  대신 C352의 `ChC352`(2자리 숫자 폰트)를 그대로 재사용하는데, 이 포트의
  `DrawBuffGA20.cs`에도 (`DrawBuffC352.cs`를 참조하지 않고 독립적으로
  복제한) 동일한 동작의 `ChGa20`으로 이식했습니다. **의도적 단순화**:
  `tp`는 0 고정, `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게
  생략.
- **C352(남코의 후기 32채널 쿼드 출력(전방 L/R + 후방 L/R) PCM 샘플 재생
  칩): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본 `frmC352.cs`를
  그대로 포팅 — C140과 비슷한 구조지만 32채널이고, 채널당 볼륨 LED 바가
  전방/후방 각각 L/R로 4개(원시 픽셀 좌표 오버로드 `VolumeXY` 사용, C140의
  행-인덱스 오버로드 `VolumeToC140`과 다름), 온/오프 플래그 아이콘도 16개
  (C140의 2개보다 훨씬 많음)입니다. **데이터 소스**: `chipRegister.
  pcmRegisterC352[chipId]`(이미 public 필드, `Audio.GetC352Register`와
  동일)와 `chipRegister.readC352(chipId)`(이미 public, `mds.
  ReadC352Flag`로 포워딩 — `Audio.GetC352KeyOn`과 동일)를 직접 읽습니다.
  C140처럼 이 칩도 새 게터가 필요 없었습니다. **클록 처리**: 원본은
  `Audio.ClockC352`(기본값 24192000)로 나누는데, 이 포트에선 다른 칩들과
  같은 패턴으로 `clockHz` 생성자 매개변수(0이면 24192000 기본값 폴백)를
  대신 사용합니다. **원본 그대로 보존한 별난 동작**: C140의
  `searchC140Note`와 마찬가지로 `searchC352Note`의 탐색 루프도 전역 최소
  거리 대신 `freq > a`일 때마다 `(m, n)`을 계속 덮어씁니다(로컬 변수 `m`은
  대입만 되고 비교엔 안 쓰임) — 그대로 포팅. 또한 노트 값은 `c352key`가
  null인지 여부와 무관하게 루프 맨 위에서 항상 먼저 계산되고, 볼륨/팬/
  마스크 처리와 "키오프 시 노트를 -1로" 로직만 `c352key != null` 블록
  안에 있는 원본의 제어 흐름을 그대로 보존했습니다(`readC352`는 실제로
  null을 반환하지 않지만, 원본의 null 가드 구조 자체를 그대로 옮겼습니다).
  **의도적 단순화**: `tp`는 0 고정(원본 자체의 `screenInit`도 `bool
  C352Type = false;`로 무조건 고정), `screenInit`의 미리 그리기 루프는
  다른 모든 칩과 동일하게 생략.
- **C140(남코 System 2/21/NA-1용 24비트 PCM 샘플 재생 칩, 24채널): 이번
  라운드에서 신규 구현, 아직 실기 미검증. PCM 계열의 첫 이식입니다.**
  원본 `frmC140.cs`를 그대로 포팅 — 채널당 8px 행 하나씩 24행에 걸쳐
  키보드/노트 표시, 독립된 L/R 볼륨 LED 바, 바이트로 압축된 L/R 팬 아이콘,
  온/오프 플래그 아이콘 2개, 그리고 샘플 주파수·ROM 뱅크·시작/종료/루프
  주소의 16진 표시가 나열됩니다. **데이터 소스**: `chipRegister.
  pcmRegisterC140[chipId]`(24*16바이트 평탄 레지스터 이미지)와
  `chipRegister.pcmKeyOnC140[chipId]`(24개짜리 라이브 키온 플래그 배열)를
  직접 읽습니다 — 둘 다 이미 `ChipRegister`의 public 필드라(Windows판
  `Audio.GetC140Register`/`GetC140KeyOn`도 동일한 필드를 그대로 반환) 이
  칩엔 새 게터가 필요 없었습니다. **원본의 라이브 참조 동작을 그대로
  보존**: `pcmKeyOnC140[ch]`는 레지스터 쓰기 쪽(`ChipRegister.writeC140`가
  키온 시 true로 세팅)과 공유하는 라이브 참조이고, `ScreenChangeParams`가
  매 프레임 이를 소비한 뒤 다시 false로 되돌립니다 — 원본 `frmC140.cs`의
  `screenChangeParams`가 `Audio.GetC140KeyOn`의 직접 참조로 하는 것과
  동일하며, 로컬 스냅샷으로 복사하지 않았습니다. **클록 처리**: 원본은
  `Audio.ClockC140`(포맷마다 동적으로 바뀌는 정적 프로퍼티, 기본값
  21390 — 다른 칩들보다 훨씬 낮은데, C140은 입력 클록 속도로 도는 대신
  고정 스텝으로 샘플 ROM을 주소 지정하기 때문)로 나누는데, 이 포트에선
  다른 칩들과 같은 패턴으로 `clockHz` 생성자 매개변수(0이면 21390 기본값
  폴백)를 대신 사용합니다. **원본 그대로 보존한 별난 동작**:
  `searchC140Note`의 탐색 루프는 전역 최소 거리를 찾는 대신 `freq > a`일
  때마다 `(m, n)`을 계속 덮어쓰는데, 로컬 변수 `m`은 대입만 되고 비교에는
  전혀 쓰이지 않습니다 — "고치지" 않고 그대로 포팅했습니다. 또한
  `screenChangeParams`에서 반환된 노트 인덱스에 `+1`을 더하는데
  (`searchC140Note(frequency) + 1`), 다른 모든 칩의 0-기반 노트 인덱스와
  달리 이 칩만 그렇습니다 — 그대로 보존. **의도적 단순화**: `tp`는 0
  고정, `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **K051649(코나미 SCC, 5채널 웨이브테이블): 이번 라운드에서 신규 구현,
  아직 실기 미검증. WF 계열의 두 번째이자 마지막 이식으로, 이로써 WF 계열도
  전부 끝났습니다.** 원본 `frmK051649.cs`를 그대로 포팅 — 5개 웨이브테이블
  채널이 3개(위 행)+2개(아래 행)로 배치되고(`x = c % 3, y = c / 3`), 채널마다
  키보드/노트 표시, 볼륨 LED 바, 주파수·볼륨 16진 표시, DDA(키온) 아이콘,
  32샘플 파형을 HuC6280/N106과 같은 막대그래프 스트립으로 보여주는 그래픽
  표시, 그리고 같은 32바이트 웨이브램을 별도의 16진 바이트 그리드로도
  보여주는 이중 판독(각각 독립된 더티-디프 추적 배열 `typ`/`inst` 사용)이
  있습니다. **데이터 소스**: `ChipRegister.GetK051649Register`(새 한 줄짜리
  포워딩 게터 — 이 포트에서 아홉 번째로 새로 추가한 게터. 기존
  `scc_k051649.GetK051649_State`를 감싸기만 하면 됐습니다)로
  `MDSound.K051649.k051649_state`를 읽습니다. **클록 처리**: 원본은
  `Audio.ClockK051649`(포맷마다 동적으로 바뀌는 정적 프로퍼티, 기본값
  1500000)로 나누는데, 이 포트에선 `Sn76489Visualizer`/`S5bVisualizer`/
  `Ay8910Visualizer`와 같은 패턴으로 생성자의 `clockHz` 매개변수(0이면
  1500000 기본값으로 폴백)를 대신 사용합니다. **원본의 `searchSSGNote`를
  재사용**: `frmK051649.cs`의 자체 `searchSSGNote`는 DMG의 것과 달리
  `Common.searchSSGNote`와 완전히 동일한 로직(12*8 테이블 전체 스캔, 동일한
  거리 계산식, 조기 종료 없음)이라 별도로 복제하지 않고 `Common.
  searchSSGNote`를 직접 호출합니다 — 이 포트에서 "항상 복제, 절대 참조하지
  않는다" 원칙의 유일한 의도적 예외입니다. **의도적 단순화**: `tp`는 0
  고정, `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **HuC6280(PC엔진/터보그래픽스-16 내장 PSG, 6채널): 이번 라운드에서
  신규 구현, 아직 실기 미검증. NES 계열이 아니라 WF 계열의 첫 이식입니다.**
  원본 `frmHuC6280.cs`를 그대로 포팅 — 6개 웨이브테이블 채널이 3개씩
  2행으로 배치되고(0-2번 채널이 위 행, 3-5번이 아래 행 — `WaveFormToHuC6280`
  등 여러 그리기 함수가 공유하는 `c > 2` 분기), 채널 4-5는 추가로 노이즈
  발생기가 붙습니다. **데이터 소스**: `ChipRegister.GetHuC6280Register`(새
  한 줄짜리 포워딩 게터 — 이 포트에서 여덟 번째로 새로 추가한 게터. 기존
  `mds.ReadHuC6280Status`를 감싸기만 하면 됐습니다)로
  `MDSound.Ootake_PSG.huc6280_state`를 읽습니다. **원본의 별칭(aliasing)
  동작을 그대로 보존**: `screenChangeParams`의 `channel.inst = psg.wave`는
  원소별 복사가 아니라 채널의 파형 배열 참조 자체를 통째로 교체합니다 —
  즉 그 순간 에뮬레이션 코어가 들고 있는 라이브 `PSG.wave` 배열을 그대로
  가리키게 되는데, "고치지" 않고 원본 그대로 옮겼습니다. **의도적
  단순화**: `tp`는 0 고정(원본은 "실제 하드웨어 사용" UI 설정에서 가져오는데
  이 포트엔 그런 설정이 없음), `screenInit`의 미리 그리기 루프는 다른
  모든 칩과 동일하게 생략.
- **DMG(오리지널 게임보이 4채널 사운드): 이번 라운드에서 신규 구현, 아직
  실기 미검증.** 원본 `frmDMG.cs`를 그대로 포팅 — 펄스 2채널(채널 0만
  주파수 스윕 있음) + 커스텀 웨이브테이블 1채널(32개의 4비트 샘플을
  실시간 막대그래프로 표시) + 노이즈 1채널, 총 4채널이며 채널마다 스테레오
  팬 표시가 붙습니다(이 포트에서 지금까지 이식한 NES 계열 칩 중 스테레오
  패닝이 있는 첫 사례). **데이터 소스**: `ChipRegister.GetDMGRegister`(새
  한 줄짜리 포워딩 게터 — 이 포트에서 일곱 번째로 새로 추가한 게터. 기존
  `mds.ReadDMG`를 감싸기만 하면 됐습니다)로 `MDSound.gb.gb_sound_t`를
  읽습니다. **원본 자체의 독립적인 노트 탐색 함수를 그대로 보존**:
  `frmDMG.cs`는 공용 `Common.searchSSGNote`를 쓰지 않고 자체 private
  `searchSSGNote`를 따로 갖고 있는데, 주파수를 4로 나눈 뒤 비교하고
  (`1 << (6 - 4)`), 테이블도 12*8이 아니라 12*9칸을 검색하며, 전역
  최솟값이 아니라 첫 번째 지역 최솟값에서 멈추는(`else break`) 다른
  탐색 알고리즘입니다 — "고치지" 않고 그대로 별도 함수로 옮겼습니다.
  **의도적 단순화**: `tp`는 0 고정, `screenInit`의 미리 그리기 루프는
  다른 모든 칩과 동일하게 생략.
- **N106(남코 106/163, 웨이브테이블 최대 8채널): 이번 라운드에서 신규
  구현, 아직 실기 미검증.** 원본 `frmN106.cs`를 그대로 포팅 — 최대 8채널
  웨이브테이블 합성 칩으로, 각 채널이 자기 자신의 8비트 웨이브테이블 RAM
  내용을 실시간으로 그래픽 파형으로 그려서 보여줍니다(FDS의 고정 32샘플
  테이블과 달리 채널마다 길이가 가변인 실시간 RAM 내용). 채널 표시 순서가
  원본에서부터 **역순**입니다(`rev = true`가 하드코딩돼 있어 이 포트에도
  그대로 반영 — 화면 0번 채널이 실제로는 레지스터 인덱스 7의 데이터를
  보여주는 식). 이 포트에서 이례적으로 이미 공개(public) 상태였던
  `getN106Register`를 새 래퍼 없이 바로 호출합니다(YM3812/Y8950의
  KeyInfo 게터처럼). **데이터 소스**: 에뮬레이션 코어가 이미 디코딩해 둔
  `MDSound.np.chip.TrackInfoN106[]`(`TrackInfoBasic`을 상속하며
  `wave[]`/`wavelen` 필드를 추가한 서브클래스) — VRC6와 마찬가지로
  `ITrackInfo[]`가 아니라 `TrackInfoN106[]`로 명시적 캐스팅해서 멤버
  숨김(`new`) 문제를 피합니다. **의도적 단순화**: `tp`는 0 고정,
  `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **VRC7(코나미 NES 카트리지 매퍼, OPLL 기반 FM 음원): 이번 라운드에서
  신규 구현, 아직 실기 미검증.** 원본 `frmVRC7.cs`를 그대로 포팅 — YM2413
  (OPLL) 코어를 그대로 내장한 칩으로, 6개 멜로디 FM 채널만 있고(YM2413의
  9멜로디+5리듬 구성과 달리 리듬 섹션 없음) 채널 0의 레지스터 뱅크에만
  공유 오퍼레이터 파라미터 테이블이 실립니다. YM2413 포트와 완전히 동일한
  `drawInstNumber`/`SUSFlag`/`ChYM2413` 그리기 로직을 그대로 다시
  옮겼습니다(파일 간 참조 없이 중복 작성 — 이 포트의 기존 방침). **데이터
  소스**: `ChipRegister.GetVRC7Register`(새 공개 래퍼 — 기존
  `getVRC7Register`가 `internal`로 선언돼 있어 UI 레이어에서 접근이
  불가능했던 것을 감쌌습니다. 이 포트에서 여섯 번째로 새로 추가한 게터)와
  이미 존재하던 `getVRC7KeyInfo`(YM3812/Y8950 포트에서 이미 쓰인 것과 같은
  `ChipKeyInfo` 원샷 키온 추적 패턴)를 함께 읽어, 이미 감쇠한 노트라도
  두 폴링 사이에 키온이 있었으면 볼륨미터가 반응하도록 합니다. **의도적
  단순화**: `tp`는 0 고정, `screenInit`의 미리 그리기 루프는 다른 모든
  칩과 동일하게 생략.
- **VRC6(코나미 NES 카트리지 매퍼 확장 음원): 이번 라운드에서 신규 구현,
  아직 실기 미검증.** 원본 `frmVRC6.cs`를 그대로 포팅 — 펄스(스퀘어) 2채널 +
  톱니파 1채널, 총 3채널이며 NESDMC/FDS/MMC5와 달리 균일한 16px 높이 행
  3개로 배치됩니다. **데이터 소스가 지금까지의 NES 계열 칩들과 또
  다릅니다**: NESDMC/FDS/MMC5는 전부 원시 레지스터 바이트를 읽어 Visualizer
  레이어에서 비트필드를 해석했지만, VRC6은 YMF271처럼 에뮬레이션 코어가 이미
  디코딩해 둔 `MDSound.np.chip.TrackInfoBasic[]` 트랙 정보 객체
  (`GetTone`/`GetVolume`/`GetKeyStatus`/`GetFreqp`/`GetHalt`/`GetNote`/
  `GetFreqHz`/`GetFreqShift`)를 그대로 읽습니다. 이 포트에 `ChipRegister.
  GetVRC6Register`를 한 줄짜리 포워딩 게터로 추가했습니다(이 포트에서
  다섯 번째로 새로 추가한 게터. 이미 있던 `getVRC6Register`를 감싸기만
  하면 됐습니다). **주의 깊게 옮긴 타입 캐스팅**: `TrackInfoBasic`의
  접근자 메서드들은 `override`가 아니라 C#의 멤버 숨김(`new`)으로
  구현되어 있어서, `ITrackInfo[]`로 타입 지정된 참조로 호출하면 조용히
  베이스 클래스의 항상-0 스텁이 호출됩니다 — 원본 `frmVRC6.cs`가
  `(TrackInfoBasic[])Audio.GetVRC6Register(0)`로 명시적으로 캐스팅하는
  이유이며, 이 포트도 동일하게 캐스팅합니다. **의도적 단순화**: `tp`는
  0 고정, `screenInit`의 미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **MMC5(NES 카트리지 매퍼 확장 음원): 이번 라운드에서 신규 구현, 아직 실기
  미검증.** 원본 `frmMMC5.cs`를 그대로 포팅 — NES APU에 펄스(스퀘어) 2채널 +
  PCM 샘플 재생 1채널을 추가하는 카트리지 매퍼 칩으로, NESDMC/FDS처럼
  채널마다 화면 좌표가 손으로 지정돼 있습니다. **데이터 소스가 지금까지의
  NES 계열 칩들과 다릅니다**: NESDMC/FDS의 `GetAPURegister`/`GetDMCRegister`/
  `GetFDSRegister`는 모두 NSF 직접 경로 + VGM 폴백의 이중 구조였지만, MMC5는
  원본의 VGM측 `getMMC5Register(chipID, EnmModel)`이 이 포트에서 항상 null을
  반환하는 스텁이라(`mds.ReadMMC5`가 존재하지 않음), `nes_mmc5.Read()`로
  `$5000`-`$5007` 레지스터 버스를 직접 순회해 10바이트 스냅샷을 조립하는
  `Audio.GetMMC5Register`의 방식이 유일하게 동작하는 경로입니다. 이 조립
  로직을 `ChipRegister.GetMMC5Register`로 그대로 옮겼습니다(이 포트에서 네
  번째로 새로 추가한 게터). **의도적 단순화**: `tp`는 0 고정, `screenInit`의
  미리 그리기 루프는 다른 모든 칩과 동일하게 생략.
- **FDS(패미컴 디스크 시스템 확장 음원): 이번 라운드에서 신규 구현, 아직
  실기 미검증.** 원본 `frmFDS.cs`를 그대로 포팅 — NES APU에 웨이브테이블
  합성 채널 1개를 추가하는 확장 칩으로, 32샘플 캐리어 파형과 그걸 피치
  변조하는 32샘플 모듈레이션 파형을 각각 막대그래프로 그려서 보여줍니다
  (YM2609의 PSG 웨이브테이블 표시와 동일한 `rWavGraph` 스프라이트를
  재사용). 두 파형 각각 독립된 램프 엔벨로프(방향/속도/게인/정지 여부)를
  갖고, 마스터 엔벨로프 클록과 볼륨/쓰기활성화 스위치까지 숫자/아이콘으로
  표시됩니다. **데이터 소스는 NESDMC와 같은 이중 경로**:
  `ChipRegister.GetFDSRegister(chipID)`를 새로 추가했고(이 포트에서 세
  번째로 새로 추가한 게터), NSF 재생 시 `nes_fds.chip`을 직접 읽고 VGM
  재생 시 기존 `getFDSRegister(chipID, EnmModel.VirtualModel)`로 폴백하는
  구조를 `Audio.GetFDSRegister`에서 그대로 옮겼습니다. 음표 계산은
  NESDMC와 달리 `-15` 오프셋이 붙은 별도의 로그 공식을 그대로 보존했습니다.
  **의도적 단순화**: `tp`는 0 고정, `screenInit`의 미리 그리기 루프는
  다른 모든 칩과 동일하게 생략.
- **NESDMC(NES/패미컴 내장 APU): 이번 라운드에서 신규 구현, 아직 실기
  미검증. 지금까지 이식한 칩 중 레이아웃이 가장 작고 특이한 칩.** 원본
  `frmNESDMC.cs`를 그대로 포팅 — 펄스(스퀘어) 2채널 + 삼각파 1채널 + 노이즈
  1채널 + DMC(델타 변조 샘플 재생) 1채널, 총 5채널이며 다른 칩들처럼 균일한
  행 단위 그리드가 아니라 채널마다 화면 좌표가 손으로 지정돼 있습니다.
  **데이터 소스가 다시 한번 다릅니다**: `Audio.GetAPURegister`/
  `GetDMCRegister`는 NSF 재생 시 칩 내부 상태(`nes_apu.chip.reg`)를 직접
  읽고, VGM 재생 시에는 일반 레지스터 경로(`getNESRegisterAPU`/
  `getNESRegisterDMC`, `EnmModel.VirtualModel`)로 폴백하는 이중 경로
  구조입니다. 이 포트에도 동일한 폴백 로직으로 `ChipRegister.GetAPURegister`/
  `GetDMCRegister`를 새로 추가했습니다(YMF271의 `GetYMF271Register`에 이어
  이 포트에서 두 번째로 새로 추가한 게터). 음표 계산도 다른 칩들의
  `Common.searchXxxNote` 테이블 검색 대신 원본이 직접 로그 공식
  (`104 - (12*(log(freq)/log(2) - log2(440)) + 45 + 0.5)`)으로 계산하는
  방식을 그대로 옮겼습니다.
  **원본 자체의 특이한 필드 재사용도 그대로 보존**: DMC 채널의 loop 스위치
  아이콘을 그리는 호출(`frmNESDMC.cs:216`)이 바로 위 줄에서 이미 갱신된
  `dda` 필드를 "old" 추적값으로 재사용해 `noise`(loop) 값과 비교합니다 —
  "고치지" 않고 그대로 옮겼습니다(`NesdmcVisualizer.cs` 주석 참고).
  **의도적 단순화**: `tp`는 0 고정, `screenInit`의 미리 그리기 루프는
  다른 모든 칩과 동일하게 생략(첫 프레임에서 dirty-diff로 자연스럽게
  전부 그려짐).
- **YMF271(OPX, FM+PCM 웨이브테이블 합성 칩): 이번 라운드에서 신규 구현,
  아직 실기 미검증. 지금까지 이식한 칩 중 유일하게 FM/SSG/리듬 구획이 없는
  구조.** 원본 `frmYMF271.cs`를 그대로 포팅 — OPN 계열과 완전히 다른 형태로,
  48개의 동일한 슬롯이 평평하게 나열되어 있고 각 슬롯이 독립적으로 FM
  보이스(AR/DR/SR/RR/SL/TL/KS/ML/DT/waveform/feedback/accon/algorithm)이거나
  PCM 샘플 재생 보이스(시작/끝/루프 주소, 샘플레이트/비트심도/소스노트/
  소스뱅크)일 수 있으며, LFO 파라미터까지 슬롯마다 붙습니다. 4슬롯씩 12개
  그룹으로 묶여서 그룹당 "동기화 모드" 아이콘 하나만 그려지고(`OpxOP`),
  채널 마스크 배지 자체가 아예 없는 유일한 칩입니다.
  **데이터 소스가 다른 칩들과 근본적으로 다릅니다**: OPN 계열은 원시
  레지스터 바이트를 읽어 이 포트의 Visualizer 레이어에서 직접 비트필드를
  해석했지만, YMF271은 원본이 애초에 에뮬레이션 코어 자신이 이미 디코딩해
  둔 `MDSound.ymf271.YMF271Chip` 구조체 트리(`slots[]`/`groups[]`)를 그대로
  읽습니다. 이 포트도 원본과 동일하게 처리하기 위해 `ChipRegister.cs`에
  `GetYMF271Register(chipID)`(`mds.ReadYMF271Register(chipID)`로 한 줄
  포워딩)를 새로 추가했습니다 — 지금까지는 이 포트가 미리 노출해둔 `Get*`
  게터만으로 충분했지만, 이 칩은 처음으로 새 게터가 필요했습니다.
  **의도적 단순화**: `tp`는 다른 모든 `DrawBuffXxx.cs`와 동일하게 0 고정.
  원본 `screenInit`의 "빈 건반/빈 팬 아이콘을 미리 한 번 그려두는" 루프는
  이 포트의 다른 모든 칩과 마찬가지로 재현하지 않았습니다 — `Channel`의
  기본값(-1 등)이 실제 디코딩된 값과 항상 다르므로 첫 `ScreenDrawParams`
  호출에서 통상적인 dirty-diff 경로로 전부 자연스럽게 그려집니다.
- **YM2610(OPNB, Neo Geo의 FM+SSG+ADPCM 칩): 이번 라운드에서 신규 구현,
  아직 실기 미검증.** 원본 `frmYM2610.cs`를 그대로 포팅 — YM2608(OPNA)과
  거의 동일한 19채널 구조(FM 0-5, Ch3 확장 슬롯 6-8, SSG 9-11)를 그대로
  쓰되, YM2608의 내장 샘플 기반 리듬 섹션 + ADPCM 1채널 대신 단일 ADPCM-B
  재생 채널(인덱스 12) + 6보이스 ADPCM-A 섹션(인덱스 13-18)이 붙습니다.
  이 포트의 `MDChipParams.YM2610.channels` 배열은 (YM2609와 달리) 원본과
  인덱스가 정확히 일치해서 별도 리매핑이 필요 없었습니다. `DrawBuffYm2608.cs`
  에서 거의 그대로 재사용 가능한 함수 집합(KeyBoardOPNA/InstOPNA/Volume/
  VolumeShort/Pan/TnOPNA/각종 font4* 등)을 이 파일에도 자체 포함(self-contained)
  방식으로 다시 옮겼고, 진짜 다른 부분만 새로 반영했습니다: 채널 배지 함수
  `ChYM2610_P`의 미번호 배지(ADPCM-B용)는 스프라이트 시트 오프셋 88을 쓰는데
  YM2608의 동일 위치는 64입니다; ADPCM-A 6채널 라벨은 YM2608의 B/S/C/H/T/R이
  아니라 "A1"/"2"/"3"/"4"/"5"/"6"이고; `VolumeYM2610Rhythm`/`PanYM2610Rhythm`는
  YM2608의 14행이 아니라 13행에 그립니다. **원본 자체의 특이한 계산 누락도
  그대로 보존**: FM 채널3 Ch3 확장 모드 분기에서 ADPCM 노트 인덱스를 구할 때
  (`frmYM2610.cs:349`) 다른 모든 노트 계산과 달리 `-1` 보정을 빼먹은 채
  `searchYM2608Adpcm(ff)`를 바로 씁니다 — "고치지" 않고 그대로 옮겼습니다
  (`Ym2610Visualizer.cs` 주석 참고). 또한 원본이 ADPCM-B 채널의 `volume`
  필드(8비트 헥스 표시용)를 `screenChangeParams`에서 한 번도 갱신하지
  않아서 해당 헥스 바이트 표시가 사실상 죽어있는 필드인 점도 그대로
  옮겼습니다(`volumeL`/`volumeR`만 실제로 갱신됨).
  **의도적 단순화**: YM2203/YM2608/YM2609와 동일하게 `parent.setting.other.ExAll`을
  원본 기본값 `false`로 고정. `tp`는 0 고정.
- **YM2609("듀얼 OPNA", 아케이드/PC-98 일부 립에 쓰이는 가상 칩): 이번
  라운드에서 신규 구현, 아직 실기 미검증. 지금까지 이식한 칩 중 가장 복잡한
  구조.** 원본 `frmYM2609.cs`를 그대로 포팅 — 레지스터 포트 공간을 공유하는
  YM2608급 FM+SSG 코어 2개("듀얼 OPNA")로 구성되며, FM 18채널(2×9, 각
  코어마다 자체 Ch3 확장 슬롯 그룹 포함), SSG 12채널(4개의 독립 톤 발생기 ×
  3채널, `psgPort`/`psgAdr` 테이블로 매핑), 6보이스 리듬 섹션, ADPCM-A
  6채널, 단채널 ADPCM("ADPCM012") 3채널, 스테레오 3밴드 EQ(저/중/고 각각
  on/off + 값 3개)가 있습니다. 오퍼레이터 파라미터 표가 YM2203/YM2608의
  11-파라미터(AR/DR/SR/RR/SL/TL/KS/ML/DT/AM/SG) 대신 16-파라미터(D2/오퍼레이터별
  FB/WT/ALL/PR 추가)라서 `Inst`/`InstOPNA`가 아니라 새 `InstOPNA2`를 씁니다.
  가장 눈에 띄는 신규 기능은 PSG 커스텀 웨이브테이블 그래픽 표시
  (`WaveFormYM2609User` — 64바이트 L/R 인터리브 샘플을 32개 열로 평균 내
  4단 8px 행에 그리는 파형 그래프, `rWavGraph` 스프라이트) + 프리셋 뱅크
  아이콘(`WaveFormYM2609Preset`, `rPSG2` 스프라이트) — 이 포트에서 처음
  등장하는 그래픽 파형 시각화입니다.
  **채널 배열 인덱스가 원본과 다릅니다**: 원본 `frmYM2609.cs`는 FM 0-17,
  SSG 18-29, 리듬 30-35, ADPCM-A 36-41, ADPCM012 42-44 순서로 번호를
  매기지만, 이 포트의 `MDChipParams.YM2609.channels` 배열은 (이전에 이미
  ADPCM012 36-38·ADPCM-A 39-44 순서로 만들어져 있었고, 아직 이 필드를 읽는
  다른 코드가 전혀 없어서) 그 기존 배열 순서를 그대로 따르고 인덱스 계산만
  맞췄습니다 — 레지스터 비트→채널 매핑 자체(값의 의미)는 원본과 동일하고,
  배열에서의 "자리"만 다릅니다. 자세한 내용은 `Ym2609Visualizer.cs`
  헤더 주석 참고.
  **의도적 단순화**: YM2203/YM2608과 동일하게 `parent.setting.other.ExAll`을
  원본 기본값 `false`로 고정(2곳). `readYM2609GetUserWave`가 요구하는
  `EnmModel` 인자는 항상 `EnmModel.VirtualModel`을 넘깁니다(피아노롤 모드에서만
  `null`을 반환하는 파라미터라 일반 재생에는 영향 없음). `tp`는 다른 모든
  `DrawBuffXxx.cs`와 동일하게 0 고정.
- **YM2608(OPNA): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYM2608.cs`를 그대로 포팅 — YM2203의 구조(3 FM + Ch3 확장 모드 + SSG
  3채널)를 그대로 확장해, FM 레지스터 포트가 2개로 늘어 FM 채널이 6개가
  되고(포트당 3채널), 채널마다 진짜 좌/우 스테레오 팬이 생기며(YM2203은 팬을
  아예 그리지 않았음), 6보이스 샘플 기반 리듬 섹션(베이스/스네어/심벌/
  하이햇/톰/림)과 ADPCM 재생 채널 1개가 추가됩니다 — 이 포트 채널 배열은
  FM 0-5, Ch3 확장 슬롯 6-8, SSG 9-11, ADPCM 12, 리듬 13-18로 총 19채널.
  `ChipRegister.fmRegisterYM2608`(2포트 × 0x100)와 `fmKeyOnYM2608`를 직접
  읽고, `GetYM2608Volume`/`GetYM2608Ch3SlotVolume`/`GetYM2608RhythmVolume`/
  `GetYM2608AdpcmVolume`를 매 프레임 호출합니다.
  **의도적 단순화**: YM2203과 동일하게 `parent.setting.other.ExAll` 사용자
  설정을 원본 기본값인 `false`로 고정했습니다(이 포트는 Setting UI를
  Visualizer 레이어까지 연결한 적이 없음 — YM2151 `hosei`/YMF278B
  MoonDriver 단순화와 같은 패턴).
  **원본 자체의 특이한 좌표 불일치를 YM2203과 동일하게 그대로 보존**: Ch3
  확장 모드 채널(6,7,8)의 볼륨/건반/주파수 표시는 행 (c+3)(9,10,11행)에
  그려지는데, 같은 반복문의 `ChYM2608` 배지 호출은 raw `c`(6,7,8)를 그대로
  넘겨서 배지 자체는 행 6,7,8에 그려집니다 — "고치지" 않고 원본 그대로
  옮겼습니다(자세한 내용은 `DrawBuffYm2608.cs`/`Ym2608Visualizer.cs`의 주석
  참고).
- **YM2203(OPN): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYM2203.cs`를 그대로 포팅 — OPL 계열과 달리 FM 채널 3개가 각각 완전한
  4-오퍼레이터 표(AR/DR/SR/RR/SL/TL/KS/ML/DT/AM/SG)를 갖고, 여기에 FM
  채널3을 3개의 독립 튜닝 가능한 오퍼레이터 슬롯으로 쪼개는 "Ch3 확장 모드"
  (이 포트의 채널 배열 인덱스 3-5) + AY8910/S5B와 같은 하드웨어 엔벨로프
  SSG 톤/노이즈 채널 3개(인덱스 6-8)가 더해집니다. 이 칩부터 처음 등장하는
  표시 방식: `Volume`/`VolumeShort`가 그리드 좌표가 아니라 원본 주석 그대로
  "raw pixel"(×1) 좌표를 받고, 오퍼레이터 표의 숫자 표시(`drawFont4Int`)가
  다른 칩들의 항상-0-패딩(`Font4Int2`)과 달리 앞자리 0을 공백으로 비웁니다
  (예: "07"이 아니라 " 7").
  **의도적 단순화 한 가지**: 원본은 Ch3 확장 모드의 슬롯 볼륨 표시가
  `parent.setting.other.ExAll`(사용자 설정 — 켜면 오퍼레이터 뮤트 마스크와
  무관하게 항상 "풀 가청" 볼륨으로 표시)에 따라 계산 마스크를 덮어쓰는데,
  이 포트는 Setting UI를 Visualizer 레이어까지 연결한 적이 없어서(다른
  칩들의 `hosei`/MoonDriver 단순화와 같은 패턴) 원본 기본값인 `false`로
  고정했습니다 — 이 포트에는 애초에 그 값을 바꿀 설정 화면 자체가 없으므로
  항상 정확합니다.
  **원본 자체의 특이한(버그로 보이지 않는) 좌표 불일치를 그대로 보존**: Ch3
  확장 모드 채널(3,4,5)의 볼륨/건반/슬롯/주파수 표시는 행 (c+3)(6,7,8행)에
  그려지는데, 같은 반복문의 `ChYM2203` 배지 호출은 raw `c`(3,4,5)를 그대로
  넘겨서 배지 자체는 행 3,4,5에 그려집니다 — `ChYM2203`의 내부 행 계산
  (`8+ch*8`)이 그 호출부의 다른 그리기 좌표와 실제로 어긋나는 원본
  그대로이며, "고치지" 않고 그대로 옮겼습니다(자세한 내용은
  `DrawBuffYm2203.cs`/`Ym2203Visualizer.cs`의 주석 참고).
- **YMF278B(OPL4): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYMF278B.cs`를 그대로 포팅 — YMF262와 동일한 18개 FM 채널(2 레지스터
  포트, ConnectSelect 4-op 페어링, 스테레오 팬) + 5개 고정 리듬 채널 구조에,
  완전히 새로운 24채널 PCM/웨이브테이블 섹션(레지스터 포트 2)이 추가됩니다
  — 채널마다 독자적인 ADSR 엔벨로프(AR/D1/DL/D2/RC/RR)/비브라토·LFO/리버브/
  팬(전용 `PanType2` 아이콘, 새로 내보낸 `rPan2_01` 스프라이트)/15옥타브
  건반(`KeyBoardToYMF278BPCM`, FM 섹션의 8옥타브보다 넓음)을 가집니다. TL/Wav
  필드는 값 범위가 넓어(최대 511/999) 이 칩에서 처음으로 3자리 숫자 표시
  변형(`font4Int2`의 `k=3` 분기)이 필요했습니다.
  **의도적 단순화 한 가지**: 원본은 PCM 키온 상태를
  `Audio.GetMoonDriverPCMKeyOn()`(MoonDriver 계열 드라이버가 로드됐을 때
  드라이버 자체의 PCM 키온 상태를 우선 사용)과 `getYMF278BPCMKeyON`(칩
  레지스터 캡처값) 사이에서 분기하는데, 이 포트는 MoonDriver(MGS/MuSICA/NDP/
  FMP/PMD 등)를 애초에 구현한 적이 없어서 항상 `getYMF278BPCMKeyON` 경로만
  탑니다 — 이 포트가 실제로 로드하는 모든 포맷에 대해 정확합니다.
  **채널 배지 리매핑 버그를 이 칩을 포팅하며 함께 발견**: 원본 `ChYMF262`/
  `ChYMF278B`는 FM 채널의 배지 번호를 raw 채널 인덱스가 아니라
  `YMF262Ch`/`YMF278BCh`라는 재배열 테이블(`drawBuff.cs:2308`/`2328`)을 거친
  값으로 그립니다 — 이전에 커밋했던 YMF262 구현이 이 리매핑을 빠뜨리고 있어서
  같은 라운드에서 별도 커밋으로 수정했습니다(자세한 내용은 커밋 메시지 참고).
  YMF278B는 처음부터 리매핑을 반영해 구현했습니다.
- **YMF262(OPL3): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYMF262.cs`를 그대로 포팅 — 지금까지 포팅한 것 중 가장 구조가 복잡한
  칩입니다. 18개 FM 채널이 2개 레지스터 포트(포트당 9채널, 주소 0x00-0xff)에
  나뉘어 있고, ConnectSelect(포트1 레지스터 0x04)로 인접한 두 채널을
  4-오퍼레이터 모드로 묶을 수 있으며(ch/ch+1 페어당 Kakko 괄호 UI로 시각적
  표시), YM3526/YM3812/Y8950과 달리 채널마다 진짜 좌/우 스테레오 팬이
  있습니다(L/R 볼륨미터 두 줄 + Pan 아이콘). 5개 고정 리듬 채널(BD/SD/TOM/
  CYM/HH)은 다른 OPL 계열과 동일. 채널 배지는 최대 18번까지 가야 해서 다른
  칩들의 1자리(`DrawFont8`)가 아니라 2자리(`DrawFont4`, `"d2"` 포맷)로
  그립니다. `VolumeXY`의 `c`(색상 팔레트 선택) 파라미터를 이 칩만 실제로
  사용합니다 — 원본이 L/R 두 줄 모두 `c=1`을 넘기고 y좌표(한 그리드 행 차이)로만
  구분하는 점을 "고치지" 않고 그대로 옮겼습니다. `getYMF262FMKeyON`은 다른
  OPL 계열의 `ChipKeyInfo` 게터들과 달리 원샷이 아니라 실시간 비트마스크,
  `getYMF262RyhthmKeyON`은 (다른 칩들처럼) 원샷입니다 — `ChipRegister.cs`
  본문 확인 완료. 새 브래킷 그리기 함수 `Kakko`(`rKakko_00.rgba32`, 16×24
  신규 스프라이트)를 이 칩에서 처음 도입했습니다.
- **YM3526(OPL/OPL1): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYM3526.cs`를 그대로 포팅 — 9개 FM 채널이 각자 독자적인 2-오퍼레이터
  파라미터 표(AR/DR/SL/RR/KL/TL/MT/AM/VB/EG/KR × 2 + 채널당 BL/F-Num/CN/FB)를
  갖는 구조(YM2413처럼 칩 전역 공유 표가 아님) + LED 볼륨미터/건반 + 5개 고정
  리듬 채널(BD/SD/TOM/CYM/HH) + ADPCM 관련 DA/DV 온오프 플래그.
- **YM3812(OPL2): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYM3812.cs`를 그대로 포팅 — YM3526과 채널 구조가 거의 동일하고(같은
  9+5채널, 같은 레지스터 오프셋), OPL2의 특징인 오퍼레이터별 파형 선택
  (Waveform Select, WS 0..3) 필드 하나만 추가됐습니다.
- **Y8950(MSX-AUDIO FM+ADPCM): 이번 라운드에서 신규 구현, 아직 실기
  미검증.** 원본 `frmY8950.cs`를 그대로 포팅 — YM3526/YM3812와 같은 9 FM +
  5 리듬 채널 구조에 ADPCM 샘플 재생 채널(채널 14, 볼륨미터+건반+배지만,
  오퍼레이터 표 없음)이 하나 추가됩니다. 원본 레이아웃을 그대로 따라간 특이
  사항 하나: 화면상 ADPCM 채널이 리듬 섹션 "위" 줄(9번 건반 행)을 재사용해서
  그려지고, 리듬 채널 5개는 그 아래 10번 행으로 한 칸 밀려 있습니다
  (`DrawBuffY8950.cs`에 상세 설명).

- **YM2413(OPLL): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYM2413.cs`를 그대로 포팅 — 9개 멜로디 FM 채널(LED 볼륨미터/건반/
  악기번호/서스테인·퍼커시브 플래그) + 5개 고정 역할 리듬 채널(BD/SD/TOM/
  CYM/HH, 볼륨미터만) + 칩 전역 공유 "사용자 악기" 오퍼레이터 표(레지스터
  0x00-0x07이 채널별이 아니라 칩 전역이라, 원본과 동일하게 채널 0의
  `inst[4..27]`에만 담김). YM2612/YM2151과 달리 리듬 채널 라벨(BD/SD 등)의
  글자색이 `mask` 값에 따라 실제로 갈리는 원본 로직이 있어서, 이 칩만은
  `DrawFont4`의 마스크 색상 변형(t 파라미터)을 단순화하지 않고 그대로
  유지했습니다(`rFont_04` 새로 내보냄).

- **YM2151: 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmYM2151.cs`를 그대로 포팅 — 8개 FM 채널(LED 볼륨미터/건반/팬/Key
  Code·Key Fraction 16진 표시) + 채널당 4개 오퍼레이터의
  AR/DR/SR/RR/SL/TL/KS/ML/DT/DT2/AM 음색표(InstOPM) + 칩 전역 Noise
  Enable/Frequency, 하드웨어 LFO(Frequency/Waveform/AMD/PMD/Sync),
  타이머A/B 표시. YM2612와 달리 PCM 채널이나 Ch3류 확장 슬롯 모드가 없어서
  채널마다 동일한 그리기 경로 하나만 탑니다.
  의도적 단순화 한 가지: 원본의 노트(건반 하이라이트) 계산은
  `Audio.DriverVirtual.YM2151Hosei[chipID]`라는 드라이버 인스턴스 상태(칩
  클럭 보정값)를 더하는데, 이 값은 `ChipRegister`에 저장되지 않고
  `setYM2151Register`를 거쳐 MIDI 내보내기용으로만 쓰입니다
  (`ChipRegister.cs`의 해당 메서드 본문 확인 완료) — 이 포트의 "ChipRegister를
  직접 읽는다" 패턴상 가져올 방법이 없어서 `hosei=0`으로 고정했습니다. 이는
  원본이 `Audio.DriverVirtual`이 null일 때 쓰는 기본값과 동일하며, 최악의 경우
  건반 하이라이트가 한두 음 어긋나는 정도이지 레지스터/음량/음색표 값 자체는
  전혀 영향받지 않습니다.
- **AY8910: 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmAY8910.cs`를 그대로 포팅 — 3개 톤/노이즈 채널(LED 볼륨미터/건반/톤·노이즈
  모드 아이콘/톤 주기 16진 표시/하드웨어 엔벨로프 모드 플래그) + 칩 전역
  하드웨어 엔벨로프 제너레이터의 Frequency/Type 표시. 스테레오 팬도 오퍼레이터
  표도 없는, 지금까지 포팅한 것 중 가장 단순한 칩 창입니다.
- **S5B(Sunsoft FME-7): 이번 라운드에서 신규 구현, 아직 실기 미검증.** 원본
  `frmS5B.cs`를 그대로 포팅 — AY8910과 같은 3채널 톤/노이즈+하드웨어 엔벨로프
  구조지만 창 자체는 AY8910보다도 단순합니다(채널당 볼륨 10진 텍스트/톤 주기
  16진/엔벨로프 모드 플래그가 없음). **데이터 소스가 이 포트의 다른 모든 칩과
  근본적으로 다릅니다** — 원본 `Audio.GetS5BRegister`는 레지스터 쓰기 이력을
  담은 배열이 아니라, FME-7 에뮬레이션 코어(`nes_fme7.Read`)의 내부 상태를 매
  프레임 직접 폴링합니다. 이 포트도 `ChipRegister.nes_fme7`(NSF 파일이 FME-7
  확장 칩을 쓸 때만 non-null)를 상대로 같은 폴링 루프를 그대로 이식했습니다.
- **SAA1099는 이번 라운드 범위에서 제외했습니다** — 원본 `frmSAA1099.cs`
  자체가 `InitializeComponent()`만 있는 빈 스텁이라(`screenChangeParams`/
  `screenDrawParams` 없음), Windows판에도 애초에 이 칩의 채널 표시계가
  구현된 적이 없습니다. 포팅할 원본 로직이 존재하지 않으므로 스킵합니다.

- **SN76489: 실기 검증 완료.** 처음 실기 테스트에서 "배경만 그려지고 재생 중
  전혀 갱신되지 않는다"는 버그가 보고됐는데, 원인 규명을 위해 디버그
  빌드(틱 카운터 + 예외 노출 + 매 틱 원시 `sn76489Register`/`GetPSGVolume`
  값 덤프)를 추가해 재검증한 결과 — 실제로는 정상 동작하고 있었고, 그 세션의
  VGM 파일이 SN76489를 안 쓰거나(혹은 눈에 덜 띄게 써서) 반응이 없어
  보였을 뿐이었습니다. YM2612+SN76489를 동시에 쓰는 파일로 재확인하니
  LED 볼륨미터/건반/팬/노이즈 모드 텍스트가 모두 정상적으로 실시간
  갱신됐습니다. 디버그 계측 코드는 검증 완료 후 제거했습니다.
- **YM2612: 이번 라운드에서 신규 구현, 아직 실기 미검증.** 범위는 6개 FM
  채널(LED 볼륨미터/건반/팬) + Ch3 특수모드 확장 슬롯 3개 + 채널6 PCM
  표시(XGM/XGM2 전용 샘플 플레이어 UI는 제외 — 이 포트가 실제로 여는
  VGM/VGZ 파일은 항상 일반 PCM 경로만 타므로) + 채널당 4개 오퍼레이터의
  AR/DR/SR/RR/SL/TL/KS/ML/DT/AM/SG 값을 표로 보여주는 음색표(InstOPN2) +
  LFO/타이머 표시까지 원본 `frmYM2612.cs`를 (XGM/XGM2 분기 제외하고) 그대로
  포팅했습니다.

- **원본 구조 조사 결과**: 원본은 GDI+ 커스텀 픽셀 버퍼 blit 엔진
  (`drawBuff.cs` 5167줄 + `FrameBuffer.cs`)으로 `Resources/plane*.png`
  스프라이트시트(209장)를 직접 blit합니다. 배경 이미지(`planeYM2612.png` 등)를
  화면에 한 번 그린 뒤, 매 프레임 바뀐 값만 작은 스프라이트로 그 위에 opaque
  blit하는 방식(더티-diff)입니다. 이 데이터가 읽는 소스(`ChipRegister.cs`,
  `MDChipParams.cs`, `Tables.cs`)는 이미 macOS에 거의 1:1로 포팅되어 있어서,
  부족한 건 100% 화면(프레젠테이션) 레이어뿐이었습니다.
- **Avalonia 재현 아키텍처**: `FrameBuffer.cs`/`DoubleBuffer.cs`를 그대로
  포팅하는 대신, Avalonia의 `WriteableBitmap` 기반 커스텀 컨트롤
  (`MDPlayerUI/Visualizer/PixelScreen.cs`)로 대체했습니다 — Avalonia는 이미
  자체 컴포지터로 더블버퍼링을 하므로 원본의 그 부분은 불필요합니다.
  `drawIntArray`(무조건 opaque 복사) 프리미티브만 포팅했는데, 이번에 이식한
  SN76489 표시계 함수들이 전부 이 경로만 쓰기 때문입니다(`drawByteArrayTransp`의
  컬러키 투명 처리는 이번 범위에서 미사용).
- **스프라이트 에셋**: PNG를 런타임에 디코딩하지 않고, 커스텀 `.rgba32` 바이너리
  포맷(폭/높이 + ARGB int32 픽셀 나열)으로 미리 변환해 임베드했습니다
  (`macos/tools/export_sprites.py`로 재현 가능). 이유: `MDPlayerUI` 프로젝트는
  이 샌드박스에서 `dotnet build`조차 한 번도 못 해봤을 만큼(nuget.org 접근 불가)
  검증이 안 된 코드가 많이 쌓여 있어서, Avalonia의 PNG 디코드 API
  (`Bitmap.CopyPixels` 등)에 대한 불확실성까지 추가로 얹고 싶지 않았습니다.
  `BinaryReader.ReadInt32()`만으로 읽는 방식은 플랫폼/버전에 상관없이 항상
  동작이 보장됩니다. SN76489용으로 9개 파일만 내보냈습니다 (`planeSN76489`,
  `rVol_01`, `rKBD_01`, `rFont_01/02/03`, `rType_01/02`, `rPan_01`) — 원본
  스프라이트는 `tp`(에뮬레이션/실칩) 파라미터로 2~3가지 색상 변형을 고르는데,
  이 포트의 엔진은 실제 하드웨어 출력을 지원하지 않으므로(`VgmEngine.cs` 참고)
  `tp=0`(에뮬레이션) 변형만 필요합니다.
- **데이터 소스**: 원본 `frmSN76489.cs`는 `Audio.GetPSGRegister`/`GetPSGVolume`/
  `GetPSGRegisterGGPanning`/`ClockSN76489`를 거치는데, 이들은 전부
  `ChipRegister`의 필드/메서드로 바로 연결되는 얇은 래퍼였습니다. 이 포트는
  `MusicEngineSession.ChipRegister`로 그 필드에 직접 접근합니다. 칩 클럭값만
  기존에 노출되지 않아서, `MusicEngineSession`에 `ChipClocks`
  (`Dictionary<enmInstrumentType, uint>`) 필드를 새로 추가하고
  `VgmEngine.Load`/`MusicEngine.Finish` 양쪽에서 채우도록 했습니다 — 향후 다른
  칩 표시계를 추가할 때도 같은 방식으로 재사용됩니다.
- **새 파일**: `MDPlayerUI/Visualizer/SpriteAtlas.cs`(로더),
  `PixelScreen.cs`(렌더링 엔진), `DrawBuffSn76489.cs`/`DrawBuffYm2612.cs`/
  `DrawBuffYm2151.cs` (drawBuff.cs 서브셋 포팅, 칩별로 독립적인 파일 — 한쪽
  칩만 쓰는 VGM이어도 다른 칩 표시계 로딩에 의존하지 않도록),
  `Sn76489Visualizer.cs`/`Ym2612Visualizer.cs`/`Ym2151Visualizer.cs`(각
  frmXxxx.cs의 ScreenChangeParams/ScreenDrawParams 포팅). `MainWindow.axaml`에
  표시계를 붙일 `VisualizerHost` 패널을 추가하고, `MainWindow.axaml.cs`는
  재생 시작 시 `MusicEngineSession`을 필드로 유지하며 ~30fps
  `DispatcherTimer`로 화면에 실린 표시계를 모두 갱신하도록 했습니다(원본은
  기본 60fps 스레드 루프 — 일단 가볍게 시작해서 실기 확인 후 조정 예정).
  창 크기는 어느 칩이 표시되는지에 따라 세로 길이가 크게 달라지므로
  (`SN76489`만 ~80px vs `YM2612`만 ~368px) 고정 크기 대신
  `SizeToContent="WidthAndHeight"`로 바꿨습니다. 칩을 추가할 때마다
  `MainWindow.axaml.cs`에 필드 하나 + `ChipClocks` 조회 한 줄 + Tick 핸들러
  두 줄을 추가하는 패턴이 확립되어 있습니다.
- **검증 상태 (중요)**: `MDPlayerCore`(엔진) 쪽 변경(`ChipClocks` 배선)은
  이 세션에서 스텁 빌드로 컴파일 확인했지만(`0 error`), **`MDPlayerUI`
  (Avalonia) 쪽 새 코드는 이 샌드박스에서 전혀 빌드 검증할 수 없었습니다**
  — `MDPlayerUI` 프로젝트 자체가 nuget.org 접근 불가로 한 번도 로컬 빌드된
  적이 없기 때문입니다(기존과 동일한 제약). SN76489/YM2612 쪽은 실기에서
  정상 동작이 이미 확인됐고(위 항목 참고), 그 렌더링 파이프라인
  (`PixelScreen.cs`/`WriteableBitmap`/`DrawingContext.PushRenderOptions`/
  `AssetLoader`)을 이후 모든 칩이 그대로 재사용하므로 그 부분은 검증된
  셈이지만, 칩별 고유 레지스터 파싱/좌표 로직(`YM2151Visualizer.cs`/
  `DrawBuffYm2151.cs` 등, 그리고 앞으로 추가될 나머지 칩들)은 각각 실기
  확인 전입니다.

## 다음 단계 후보

1. **남은 ~12개 칩 채널 표시계 계속 구현**: SN76489/YM2612/YM2151/AY8910/S5B/
   YM2413/YM3526/YM3812/Y8950/YMF262/YMF278B/YM2203/YM2608/YM2609/YM2610/
   YMF271/NESDMC/FDS/MMC5/VRC6/VRC7/N106/DMG/HuC6280/K051649/C140/C352/
   GA20/K053260/K054539/MegaCD/MpcmX68k/MultiPCM는 완료했고(NES 계열 8종 +
   WF 계열 2종 전체 완료, PCM 계열 15종 중 C140/C352/GA20/K053260/
   K054539/MegaCD/MpcmX68k/MultiPCM 완료), 이어서 나머지 칩들(PCM 계열
   나머지 7종: OKIM6258/OKIM6295/PCM8/PWM/QSound/Rf5c68/SegaPCM, 그리고
   YMZ280B — SAA1099는 범위 제외)을 순서대로 이식 중입니다. 각 칩마다
   `DrawBuffXxx.cs`+
   `XxxVisualizer.cs` 작성 → 필요한 스프라이트 `export_sprites.py`로 추출 →
   `MainWindow.axaml.cs` 배선 → 커밋 → 기기 동기화 순서를 반복합니다.
2. **YM2612/YM2151 표시계 실기 검증**: 실제 Mac에서 빌드하고, 각 칩을 쓰는
   VGM 파일을 `MDPlayerUI`로 열어 LED 볼륨미터/건반/팬/음색표/LFO·타이머
   표시가 실제로 올바르게 그려지는지 확인이 필요합니다(YM2612는 Ch3 특수모드
   확장 슬롯 3개 포함). 문제가 있다면 해당 `XxxVisualizer.cs`(레지스터
   파싱)나 `DrawBuffXxx.cs`(좌표/스프라이트 인덱싱)를 의심해보세요.
3. **실제 파일로 검증**: 이제 13개 포맷 + VGM 스펙 칩 38개가 전부 배선되어
   있고, 실제 Mac에서 전체 빌드 성공 + S98/NSF 픽스처 실기 오디오 확인까지
   끝났으니, 각 포맷의 실제 파일(vgmrips.net의 VGM, HVSC의 SID/AY, 각종 NSF/GBS/
   HES 아카이브 등)을 받아(라이선스/저작권 확인 후) 원본 Windows 빌드와
   파형/사운드를 비교해보는 게 다음 신뢰도 검증 단계입니다. 지금까지
   실제 오디오로 검증된 건 VGM의 YM2151/NES APU, 그리고 신규 S98/NSF
   픽스처뿐이고 나머지(XGM/XGM2/SID/MND/ZMS/ZMD/MDX/MDR/GBS/HES/AY/ZGM)는
   코드 리뷰 수준입니다. 위에 적은 VGM EOF 오프셋 이슈도 실제 파일에서
   재현되는지 확인이 필요합니다.
4. **AY 실제 오디오 검증**: `Driver/AY/AY.cs`가 실제 Z80dotNet 패키지로
   컴파일되는 것은 실제 Mac 빌드로 이미 확인됐습니다 (에러 0). 남은 건
   실제 ZX Spectrum AY 파일을 `LivePlayer`나 `MDPlayerUI`로 재생해서
   AY8910+ZXBeep 배선이 실제로 올바른 소리를 내는지 청취 확인하는 것뿐입니다.
5. **MDPlayerUI 기능 확장**: 지금은 파일 하나 열기/재생/정지뿐입니다.
   재생 목록, 재생 시간 표시/탐색바, 볼륨 조절, 최근 파일 목록 같은 실사용에
   필요한 기본 기능을 추가할 수 있습니다.

## 라이선스 메모

MDSound는 GPLv3입니다 (`macos/MDSound/LICENSE.txt`). MDPlayer 본체도 동일 라이선스
계열로 알고 있으니 macOS 포크도 GPLv3를 유지하는 게 맞습니다 — 배포 전에 원본
LICENSE.txt 조건(특히 SCCI2 동봉 관련 별도 허가 조항)도 한 번 더 확인하세요.
