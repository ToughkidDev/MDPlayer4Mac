# MDPlayer4Web

MDPlayer4Mac의 재생 엔진(MDPlayerCore)을 .NET WebAssembly로 빌드해 브라우저에서 실행하는 웹 버전입니다.
설치 없이 브라우저에서 칩 음악 파일을 열어 재생할 수 있습니다. 파일은 서버로 올라가지 않고 브라우저 안에서만 처리됩니다.

## 구조

| 파일 | 역할 |
| --- | --- |
| `EngineBridge.cs` | MusicEngine을 JavaScript에서 부르는 `[JSExport]` 래퍼 (로드, PCM 렌더, 상태, 반복 설정) |
| `wwwroot/engine-worker.js` | .NET 런타임과 엔진을 Web Worker에서 실행. 열린 파일을 메모리 파일 시스템에 써 둠 |
| `wwwroot/pcm-worklet.js` | AudioWorklet. 워커에 PCM을 요청해 약 0.35초 분량을 버퍼링하며 출력 |
| `wwwroot/app.js` | 재생 목록, 재생 조작, 오실로스코프, 드라이버 파일 관리 |
| `wwwroot/index.html`, `app.css` | 화면 (라이트/다크 테마) |

곡과 같은 폴더에서 찾는 파일(PCM, PDX, 음색, 리듬 WAV 등)은 함께 연 파일이 같은 가상 폴더에 놓이므로 macOS 판과 같은 방식으로 찾습니다.
YM2608 리듬 음원(`2608_*.WAV`)은 앱에 포함되어 시작할 때 엔진 앱 폴더에 설치됩니다.

## 빌드

.NET 10 SDK가 필요합니다. 저장소 루트에서:

```sh
git submodule update --init --recursive
dotnet publish MDPlayer4Web/MDPlayer4Web.csproj -c Release -o MDPlayer4Web/publish
```

`MDPlayer4Web/publish/wwwroot`가 사이트 루트입니다. 정적 파일만 있으므로 아무 웹 서버로 배포할 수 있습니다.

```sh
cd MDPlayer4Web/publish/wwwroot
python3 -m http.server 8080
# 브라우저에서 http://localhost:8080 열기
```

`file://`로 직접 열면 Worker/WebAssembly가 막히므로 반드시 HTTP로 띄워야 합니다.
(`dotnet workload install wasm-tools`를 설치하면 더 작고 빠르게 빌드되지만 없어도 동작합니다.)

## 사용법

- **파일 열기 / 폴더 열기 / 끌어다 놓기**로 재생 목록에 추가합니다. 곡이 쓰는 부속 파일이 있으면 폴더째 여는 것이 가장 확실합니다.
- `.m3u` 재생 목록은 같은 번에 연 파일 중에서 항목을 찾아 추가합니다.
- 반복 버튼은 **반복 끔 → 반복 재생 → 랜덤 재생** 순으로 바뀝니다. 반복 끔이면 곡이 끝나면 다음 곡으로 넘어갑니다. macOS 판과 같이 반복 재생일 때만 파일의 루프를 따릅니다.
- Space 키로 재생/일시정지합니다.
- **드라이버 파일**: MGSDRV.COM, KINROU5.DRV, NDP.BIN, FMP.COM, ZMUSIC.X, yrw801.rom처럼 원본 드라이버·ROM이 필요한 형식은 여기에 추가합니다. 브라우저(IndexedDB)에 저장되어 다음 방문 때 자동으로 다시 설치됩니다.

## 지원 형식

- 엔진 재생: VGM/VGZ, XGM/XGZ, ZGM, S98, NSF, GBS, HES, SID, AY, MDX, MND, MGS, MUB/MUC, PMD(M/M2/MZ), MuSICA(BGM/MSD), MoonDriver(MDR), MDL, ZMS/ZMD, NRD, NDP, FMP(OPI/OVI/OZI/MPI/MVI/MZI), MML/MUS/O/OX/OY 등 MDPlayer4Mac이 지원하는 칩 음악 형식
- 오디오 파일: WAV, AIFF, MP3, M4A/AAC, FLAC, OGG (브라우저 내장 디코더로 재생하며, 지원 범위는 브라우저마다 다름)

## 제한 사항

- MIDI/RCP/RCS는 macOS의 시스템 신시사이저(AudioToolbox)를 쓰므로 웹 판에서는 재생할 수 없습니다.
- 출력은 44.1 kHz 스테레오로 고정이며, macOS 판의 채널 마스크·밸런스·각종 설정 창은 없습니다.
- 엔진은 단일 스레드 WebAssembly로 동작합니다. 무거운 곡(여러 칩을 동시에 쓰는 곡 등)은 느린 기기에서 끊길 수 있습니다.
- 처음 열 때 약 20 MB의 엔진 파일을 내려받습니다 (서버가 `.br`/`.gz` 압축 파일을 제공하면 더 작아짐).

## 라이선스

MDPlayer4Mac과 같이 GPLv3를 따릅니다. 원본 MDPlayer © kuma4649, 각 드라이버·음원 코어는 해당 저작자의 라이선스를 따릅니다.
