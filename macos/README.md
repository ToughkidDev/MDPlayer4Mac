# MDPlayer4Mac

Windows용 MDPlayer를 macOS에서 쓸 수 있도록 옮기는 개인 포팅 프로젝트임. 원본
WinForms 코드는 저장소의 `MDPlayer/`에 그대로 두고, macOS 쪽 코드는 `macos/` 아래의
`.NET 8` 프로젝트로 따로 관리 중임.

마지막 갱신: 2026-10-27

## 개발 중

- MDPlayer4Mac 전용 `MDPlayer.MucoNative` C# 코어를 별도 프로젝트로 분리했음.
  MUC 컴파일, msPb 검사, 채널 매핑과 재생 세션을 앱 내부에서 처리하며 MSX용
  COM·외부 모듈·도구체인 설치 없이 독립 빌드함. 기존 `msPb0100~0102`와 OPNA
  동시 연주를 유지함. 최신 `msPb0103~010A`의 PN1~PN6 음악 객체와 두 글자 `?` 확장
  명령도 C#으로 컴파일·연주하며, 공유 구절·음색·표 선택·효과 유지/중지/재시작·페이드와
  파형 보간을 처리함. OPNA A~K 문법은 기존 경로를 유지함.
  [코어 구성과 지원 범위](MucoNative/README.md)를 참고하면 됨.
- MUCOM88의 컴파일된 `.mub`와 소스 `.muc`를 재생 경로에 추가했음. `.muc`는 재생 전에
  메모리에서 MUB로 컴파일하며, macOS/Git의 LF 줄바꿈과 들여쓴 확장 트랙 선언도 MUCOM 컴파일러가
  올바르게 해석하도록 보정함. 일반 MUB의 YM2608(음원·SSG·리듬·ADPCM)과 확장 `muPb` MUB의
  2×YM2608, 2×YM2610, YM2151 구성을 파일에 실제로 들어 있는 파트만큼 초기화함. 볼륨 뷰와
  채널 뷰는 기존 칩 레지스터 경로를 그대로 사용함.
- 공식 `kuma4649/mucomDotNET` 소스를 Git 서브모듈로 연결했음. GitHub Release 빌드도 서브모듈을
  함께 받아 드라이버 DLL을 앱 번들에 포함함.
- Windows판이 사용하던 공식 `kuma4649` 드라이버 소스를 추가로 서브모듈로 연결했음. PMD의
  `.mml`/`.m`/`.m2`/`.mz`, MoonDriver의 `.mdl`/`.mdr`, MUAP98의 `.mus`/`.o`/`.ox`/`.oy`를
  재생 경로에 넣었음. 각각 원본과 같은 YM2608+PPZ8/PPSDRV/P86, YMF278B(또는 YMF262),
  YM2608+YM2612+CS4231 구성을 사용함. 소스 형식은 임시 파일 없이 메모리에서 컴파일하고,
  PMD의 PPC/PPS/PPZ/P86 및 MoonDriver의 같은 이름 `.pcm` 보조 데이터는 곡 파일과 같은
  폴더에서 찾음.
- MuSICA의 컴파일 데이터 `.bgm`, 소스 `.msd`, NRTDRV의 `.nrd` 재생을 추가했음. MuSICA는
  원본 MSX 드라이버를 실행하므로 `.bgm`에는 사용자가 지정한 `KINROU5.DRV`가 필요하고,
  `.msd`를 즉석 컴파일하려면 `KINROU4.COM`도 필요함. 두 프로그램은 앱에 포함하지 않음.
  `.m3u` 파일은 재생목록으로 열어 상대 경로의 지원 음악 파일을 현재 재생목록에 펼쳐 넣음.

## v0.2.9

- 음악 데이터가 64KB를 넘는 곡이 재생 도중 멈추던 문제를 수정함(반복 `[ / ]` 주소와 64KB 너머의 루프 위치).
- 60,000바이트 이상인 MUC 소스는 규격 밖으로 보고 거부하며, 상태줄에 크기와 이유를 표시함.
- [변경 사항](release-notes/v0.2.9.md)을 참고하면 됨.

## v0.2.8

- `msPb0100~0102` 컴파일 결과에서 헤더 태그가 누락되던 문제를 수정함.
- 제목 등 메타데이터와 `#CarrierCorrection` 같은 드라이버 설정을 원래 헤더의 태그 위치·크기로 보존함.
- [변경 사항](release-notes/v0.2.8.md)을 참고하면 됨.

## v0.2.7

- OPNA/MSX 혼합곡에서 `#CarrierCorrection`과 `#OPNA1RhythmMute` 설정이 누락되던 문제를 수정함.
- SCC 또는 OPLL과 함께 재생해도 FM 캐리어의 음량 차이와 지정한 OPNA 리듬 뮤트를 보존함.
- [변경 사항](release-notes/v0.2.7.md)을 참고하면 됨.

## v0.2.6

- [PR #1](https://github.com/ToughkidDev/MDPlayer4Mac/pull/1)의 압축 행 문법과 OPLL 재생 수정을 적용함.
- OPLL `?FB0 … ?FBn` 반복, 드럼·효과 트랙 쉼표, `?ID`의 음색·음량·베이스 드럼 설정 반영을 수정함.
- 압축 행에서 `|`, 두 겹 `[ ]n`, `L`을 지원하고 OPLL `?IR`/`?IU`를 허용함. PSG 전용 압축곡의 불필요한 파형 정의를 없앰.
- 파형 모핑의 범위 끝 처리를 순환하도록 바꾸고, 명령 오류 메시지에 두 글자 권장명을 표시함.
- [변경 사항과 알려진 제한](release-notes/v0.2.6.md)을 참고하면 됨.

## v0.2.5

- 릴리즈 실행기를 macos-15로 변경함. v0.2.4의 macos-14 작업은 실행기를 받지 못해 앱 파일이 게시되지 않았음.
- 기능과 검증된 코어는 v0.2.4와 동일하며 아래 변경 사항을 포함함.

## v0.2.4

- macOS 전용 독립 `MDPlayer.MucoNative` 코어와 `msPb0103~010A`/PN1~PN6를 추가함.
- 두 글자 `?` MSX 확장 명령과 기존 별칭, 공유 구절·음색·효과·페이드·파형 제어를 지원함.
- compact 스윕 재시작, 공유 PSG 노이즈, 효과 취소 후 쉼표, 혼합 FM 음색과 채널 진입점 처리를 수정함.
- 원본 MUC 743개를 실제 오디오 엔진으로 검사했고 MUC/MUB 재생 PCM이 모두 일치함.
  497개 정상 종료, 239개 2루프, 7개는 20분 정상 재생으로 구분함.
- [변경 사항과 알려진 제한](release-notes/v0.2.4.md)을 참고하면 됨.

## v0.2.3

- `msPb0102`의 전용 SCC/SCC+ 5채널 음표 MML을 지원함. `SCCA`~`SCCE`, 인라인 `@W`와 SCWD 파형 뱅크를 읽고, 다섯 번째 독립 파형과 선형 음량을 보존함.
- SCC 연주가 OPNA FM·리듬·ADPCM 또는 MSX PSG 채널을 차지하지 않도록 분리함. 기존 msPb0100/0101 재생도 유지함.
- 반복문 뒤 같은 음 타이를 합치면서 점프 주소가 손상되던 MUC 컴파일러 오류와 빈 페이지로 인한 잘못된 용량 초과를 수정함.
- SCC5·동시 OPNA/PSG·파형 뱅크·반복/루프·잘못된 입력 회귀 검사를 추가함.
  [변경 사항](release-notes/v0.2.3.md)과 [MSX playback](docs/MSX-PLAYBACK.md)을 참고하면 됨.

## v0.2.2

- msPb의 템포 설정 전용 OPNA 파트를 오디오 믹서에서 제외해, 같은 PSG/SCC 연주의
  MUC와 MUB 재생 음량이 달라지는 문제를 수정함. 실제 OPNA 연주와 SCC-SSG 폴백은 유지함.
- 루프 진입점과 미완성 명령의 칩 판정을 보완하고, 템포·쉼표만 있는 무음 곡도 재생되도록 함.
- OPNA 리듬 초기화와 여러 드럼의 자동 팬 대기 명령을 수정함.
- 최신 MUCOMSX의 기본 템포, SCC 파형 전송·캐시, raw 명령 판독과 MUC BOM/EOF 처리를 반영함.
- MUB/MUC 각각 20개 재생 및 종료 검사와 칩 판정·재로드 회귀 검사를 통과함.
  [변경 사항](release-notes/v0.2.2.md)과 [MSX playback](docs/MSX-PLAYBACK.md)을 참고하면 됨.

## v0.2.1

- MUCOMSX raw v2의 PSG, SCC/SCC+ 및 소스 OPNA SSG 레지스터 전송을 지원함.
  선언된 칩에 맞춰 에뮬레이터를 초기화하고 SCC+ 5번 채널 독립 파형을 처리함.
- MSX PSG의 에뮬레이터 클록과 SSG 클록 변환을 보정했음.
- raw 스트림은 MSX 플레이어와 같은 정수 샘플 타이머를 사용해 템포 변경과
  타이머 응답 때의 누적 재생 시간 오차를 줄였음. 일반 MUCOM 타이머는 유지함.
- 합성 MUB/MUC 각각 15개와 Z80 레지스터·타이머 비교를 포함한 회귀 검사를 추가함.
  상세 내용은 [MSX playback](docs/MSX-PLAYBACK.md)을 참고하면 됨.

## v0.2.0

- MUCOMSX의 `msPb0100`/`msPb0101` MUB와 네이티브 `MSX` 파트가 있는 MUC를 지원함.
  MSX-MUSIC, 내부 PSG, SCC+ SSG 재생과 확장 파형·게인 명령을 처리함.
- MUCOM 컴파일러·드라이버 수정본을 별도 프로젝트로 관리하며, OPNA FM3 모드 보존과
  K 파트 레지스터 라우팅 수정, ADPCM에만 적용되는 `#PCMInvert` 수정을 포함함.
- OPNA 리듬 WAV를 앱에 포함하고, MSX 재생 회귀 테스트와 Z80 드라이버의 레지스터
  비교 데이터를 추가함.
- 이전 릴리즈 이후 추가된 FMP, NDP 및 WAV/AIFF/MP3/M4A/AAC/FLAC/OGG 재생과
  외부 드라이버 경로 설정, Xcode 빌드 지원을 포함함.

## v0.1.7

- MSX-AUDIO(Y8950) ADPCM 채널 뷰가 레지스터의 고정 Total Level만 표시하던 문제를 수정했음.
  이제 Delta-T ADPCM의 실제 디코딩 신호를 사용하므로 재생 중 게이지가 움직이며, 볼륨 뷰의
  사용자 게인과 관계없이 원신호 기준으로 표시됨.

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
.mdx  .mdr  .mdl  .mub  .muc  .mml  .m  .m2  .mz
.mus  .o  .ox  .oy  .mgs  .msd  .bgm  .nrd  .ndp  .mid  .rcp  .rcs
.opi  .ovi  .ozi  .mpi  .mvi  .mzi
.nsf  .gbs  .hes  .s98  .ay  .zgm
.wav  .aif  .aiff  .mp3  .m4a  .aac  .flac  .ogg
.m3u (재생목록)
```

- `.vgz`, `.xgz`는 각각 gzip 압축 VGM/XGM으로 처리함.
- 오래된 VGM 1.01 이하 파일은 명령 스트림을 확인해 YM2413/YM2612/YM2151 중 실제 사용 칩만
  초기화함.
- MDX는 X68000 계열 포맷임. 같은 폴더의 PDX를 찾으면 PCM8/ADPCM 재생에 사용하며,
  PDX가 없어도 OPM(YM2151) 파트는 계속 재생을 시도함.
- `.mdr`/`.mdl`은 MoonDriver OPL4/OPL3 포맷임. 같은 이름의 `.pcm` 파일이 있으면 함께
  사용함. `.mml`은 PMD 소스이며 `.m`/`.m2`/`.mz`는 PMD 컴파일 데이터임. `.mus`는 MUAP98
  소스이고 `.o`/`.ox`/`.oy`는 MUAP98 컴파일 데이터임.
- `.mgs`는 MGSDRV 형식임. MGSDRV는 수정하지 않은 재배포를 허용하므로(`licenses/MGSDRV/`)
  `macos/MDPlayerUI/Assets/Drivers/`에 `MGSDRV.COM`을 넣으면 앱 번들에 함께 포함됨. 번들에
  없으면 사용자 Drivers 폴더나 Setting → Other → MGSDRV에서 지정한 파일을 씀.
- 원본 드라이버나 ROM이 필요한 포맷(MuSICA, NDP, FMP, ZMUSIC, OPL4의 `yrw801.rom`)은
  `~/Library/Application Support/MDPlayer4Mac/Drivers/`에 파일을 넣으면 됨. Windows판은 exe
  옆에서 찾지만 macOS에서는 그 위치가 서명된 앱 번들 안이라 따로 폴더를 둠. 설정 창 Other
  탭의 Driver files에서 폴더를 열고 각 파일을 찾았는지 확인할 수 있음.
- `.bgm`은 MuSICA 컴파일 데이터이며 Setting → Other에서 `KINROU5.DRV`를 지정해야 함.
  `.msd`는 MuSICA 소스라서 같은 이름의 `.vcd`가 있으면 함께 읽고, 즉석 컴파일을 위해
  `KINROU4.COM`도 지정해야 함. 두 파일 모두 라이선스상 앱에 포함하거나 재배포하지 않음.
- `.ndp`는 MSX PSG 드라이버 NDP 형식임. 원본 `NDP.BIN`을 사용자 Drivers 폴더에 넣어야 하며,
  MGSDRV와 같은 Z80/MSX 호스트에서 실행해 AY8910으로 출력함.
- `.opi`/`.ovi`/`.ozi`는 PC-98 FMP 컴파일 데이터이고 `.mpi`/`.mvi`/`.mzi`는 FMP 소스임.
  원본 `FMP.COM`을 PC-98(80286) 에뮬레이터 Nise98에서 실행해 YM2608+PPZ8로 출력함. 소스
  형식은 재생 전에 `FMC.EXE`로 메모리에서 컴파일함. 두 파일 모두 사용자 Drivers 폴더에 넣어야
  하며, PPZ8은 Nise98이 직접 에뮬레이션하므로 `PPZ8.COM`은 필요 없음. PVI/PZI PCM 파일은
  곡 파일과 같은 폴더에서 찾음.
- `.nrd`는 NRTDRV 형식이며 파일이 사용하는 YM2151(1/2개)·AY8910 조합을 자동으로 구성함.
- `.mid`는 Standard MIDI File, `.rcp`는 RCP 시퀀스, `.rcs`는 RCP+PCM8 시퀀스 형식임.
  macOS 내장 DLS General MIDI 신시사이저로 출력함. RCP/RCS가 참조하는 `.cm6`/`.gsd` 및
  companion `.rcp`는 같은 폴더에서 자동으로 찾음. 외부 MIDI 장치와 VST 라우팅은 아직 없음.
- `.wav`/`.aif`/`.aiff`/`.mp3`/`.m4a`/`.aac`/`.flac`는 macOS AudioToolbox(ExtAudioFile)로
  디코딩하며 출력 샘플레이트 변환도 시스템이 처리함. `.ogg`는 macOS가 디코딩하지 않으므로
  NVorbis로 읽음. Windows판의 `.wma`는 macOS에서 지원하지 않음. macOS 외 환경에서 돌리는
  EngineSmokeTest는 WAV와 OGG만 읽음. `macos/tools/audio-file-smoke.sh`로 생성한 테스트 톤을
  각 형식으로 디코딩해 길이와 음높이를 검사할 수 있음(CI의 audio-files 작업).
- `.m3u`는 오디오 파일이 아니라 재생목록 파일임. `#EXTINF` 제목과 상대/절대 로컬 경로를
  읽으며, 네트워크 URL은 현재 지원하지 않아 건너뜀.
- `.mub`는 MUCOM88의 컴파일 완료 데이터 포맷이며, `.muc`는 재생 시 메모리에서 같은 형식으로
  컴파일함. `#voice`, `#pcm`, `#@pcm` 등 외부 데이터를 지정한 MUC는 해당 파일을 MUC와 같은
  폴더에 두어야 함.
- MUC 소스 파일은 MUCOM88 규격에 맞춰 60,000바이트 미만만 받음. 60,000바이트 이상인 MUC는 컴파일하지
  않고 크기와 함께 오류를 상태줄에 표시함. 이미 만들어진 `.mub` 파일에는 이 제한을 적용하지 않음.
- MUCOM88의 포르타멘토는 길이를 첫 음 뒤에 쓰는 `{c4>c}` 형식임. MUCEdit 매뉴얼처럼 `}` 뒤에 쓴
  `{c>c}4`도 컴파일 직전에 메모리에서 `{c4>c}`로 옮겨 같은 결과로 재생함. MUC 파일 자체는 바꾸지
  않으며, 첫 음에 이미 길이가 있으면 그대로 둠.
- 지원 목록에 있는 형식이라도 모든 드라이버와 칩 조합이 완성된 것은 아님. 로드할 수 없는
  조합은 상태줄에 오류로 표시됨.

## 플레이어 사용법

파일을 처음 열면 채널 뷰가 나옴. 채널 뷰와 볼륨 뷰 아래에는 약 3.5행 높이의 재생목록이
붙고, 재생목록 버튼을 누르면 20행 높이의 목록 전용 화면으로 바뀜. 20곡이 넘는 경우에는
목록 안에서 스크롤됨.

- 재생목록 영역에 파일이나 폴더를 놓으면 현재 목록 뒤에 추가됨.
- 상단 대시보드에 놓으면 현재 재생목록을 새 목록으로 교체함.
- 폴더를 드롭하면 하위 폴더까지 찾아 지원하는 파일을 이름순으로 추가함.
- `.m3u`를 열거나 드롭하면 목록 안의 지원 음악 파일을 펼쳐 추가함.
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
- .NET SDK 9.0.300 이상(10 권장). Avalonia 12의 XAML 소스 생성기가 Roslyn 4.14를 요구하므로
  SDK 8로는 빌드되지 않음(타깃 프레임워크는 그대로 `net8.0`)
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

### Xcode에서 빌드

`macos/MDPlayer4Mac.xcodeproj`를 열고 `MDPlayer4Mac` 스킴을 실행(⌘R)하면 됨. 앱 자체는
.NET/Avalonia 코드라 Xcode가 직접 컴파일하지 않음. 앱 타깃의 `Build .NET App` 빌드 단계가
`macos/Xcode/build-app.sh`를 호출해 `dotnet publish`(자체 포함, 단일 파일, `osx-arm64`)를
실행하고, 결과물을 `MDPlayer4Mac.app/Contents/MacOS`에 넣음. `Info.plist`와 아이콘, 서명은
Xcode가 처리함. 번들 구성은 릴리즈 워크플로와 같음.

- 사전 준비: .NET SDK 9.0.300 이상(10 권장), 서브모듈(`git submodule update --init --recursive`)
- Xcode의 Debug/Release 구성이 그대로 `dotnet publish -c Debug/Release`로 전달됨
- `dotnet`은 `PATH`, `/usr/local/share/dotnet`, `/opt/homebrew/bin`, `~/.dotnet` 순서로
  찾음. 다른 위치라면 스킴의 환경 변수에 `DOTNET=/path/to/dotnet`을 지정하면 됨
- 서명은 기본적으로 "Sign to Run Locally"(ad-hoc)임. Developer ID로 서명하려면 타깃의
  Signing 설정만 바꾸면 되고, 번들 안의 dylib도 같은 인증서로 서명됨
- LLDB는 .NET 관리 코드를 디버깅할 수 없으므로 스킴은 디버거 없이 앱을 실행함. 디버깅이
  필요하면 VS Code나 Rider의 .NET 디버거를 실행 중인 프로세스에 연결하면 됨
- 앱 버전은 타깃의 General 탭(`MARKETING_VERSION`)에서 바꿈
- `macos/` 아래를 바꿔 브랜치에 푸시하면 `.github/workflows/xcode-build.yml`이 macOS 러너에서
  Debug/Release를 `xcodebuild`로 빌드하고, 번들 구성·서명 검증과 15초 실행 확인까지 함

## 릴리즈

`v*` 태그를 푸시하면 `.github/workflows/release-macos.yml`이 Apple Silicon용 자체 포함 앱을
빌드해 Release에 첨부함.

```bash
git tag v0.2.3
git push origin v0.2.3
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

## MUCOMSX MSX 확장 재생

`msPb0100`과 `msPb0101`을 별도로 판별하여 MSX-MUSIC, 내부 PSG, SCC+ SSG를 재생함.
네이티브 `MSX` 파트가 포함된 MUC의 메모리 컴파일도 지원함. `@I`, `@W`, `pe`,
`#PCMInvert`의 동작과 근사 한계, 회귀 테스트는 [MSX 재생 안내](docs/MSX-PLAYBACK.md)에 정리함.

## 라이선스

원본 MDPlayer와 `MDSound/` 안에 포함된 외부 코드의 라이선스를 따름. 새 코드를 추가하거나
배포할 때는 원본 파일의 저작권과 GPL 계열 조건을 함께 확인해야 함.

MUB 재생에는 [mucomDotNET](https://github.com/kuma4649/mucomDotNET)을, PMD 재생에는
[PMDDotNET](https://github.com/kuma4649/PMDDotNET)을 Git 서브모듈로 포함하며, 두 프로젝트의
GPL-3.0 라이선스와 저작권 고지를 함께 따름. MoonDriver와 MUAP 재생에는 각각
[MoonDriverDotNET](https://github.com/kuma4649/MoonDriverDotNET),
[muapDotNET](https://github.com/kuma4649/muapDotNET)의 소스를 Git 서브모듈로 포함함. 이 두
저장소는 GitHub에 별도 라이선스 파일이 표시되지 않으므로, 배포 전 원 저작자와 저장소의
저작권·배포 조건을 반드시 다시 확인해야 함.
