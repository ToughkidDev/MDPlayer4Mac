# MDPlayer4Web

MDPlayer4Mac을 브라우저에서 그대로 실행하는 웹 버전입니다.
설치 없이 브라우저에서 칩 음악 파일을 열어 재생할 수 있고, 파일은 서버로 올라가지 않고 브라우저 안에서만 처리됩니다.

**바로 사용하기: https://toughkiddev.github.io/MDPlayer4Mac/MDPlayer4Web/**

## Mac 버전과 같은 화면

웹 버전은 화면을 따로 다시 만든 것이 아니라, Mac 앱의 Avalonia UI 소스(`macos/MDPlayerUI`)를 그대로 가져와 브라우저용 Avalonia(Avalonia.Browser)로 빌드합니다.
그래서 대시보드·전송 버튼·타이머, 칩별 채널 뷰, 볼륨(믹서) 뷰, 재생목록 뷰, 곡 정보 뷰, 설정 창, 단축키(Q W E R T Y U / A S D F G H J), 반복/랜덤, 자동 재생(재생 버튼 2초 누름)까지 Mac 버전과 같은 코드로 동작합니다.
Mac 앱의 화면이 바뀌면 웹 버전도 다음 배포 때 함께 바뀝니다.

브라우저에서 할 수 없는 부분만 `Web/` 폴더의 대체 코드가 맡습니다.

| 파일 | 역할 |
| --- | --- |
| `Web/Window.cs`, `Web/WindowFrame.cs`, `Web/WebDesktop.cs` | 브라우저에는 창이 없으므로, 페이지 안에 macOS 모양의 창(제목줄·신호등 버튼)을 그리고 Mac 앱의 `Window`를 대신합니다. 메인 창과 설정 창은 원본 코드 그대로 이 창 안에 들어갑니다. |
| `Web/CoreAudioQueue.cs`, `Web/WebAudio.cs`, `wwwroot/audio.js`, `wwwroot/pcm-worklet.js` | macOS Audio Queue 대신 Web Audio(AudioWorklet)로 소리를 냅니다. |
| `Web/MainWindow.Web.cs`, `Web/WebDrop.cs`, `Web/WebStorage.cs`, `wwwroot/main.js` | 파일 열기와 끌어다 놓기. 고르거나 놓은 파일(폴더 포함)을 메모리 안의 파일 시스템에 함께 넣어, PDX·PCM·음색 파일처럼 곡 옆에 있어야 하는 파일을 Mac과 같은 방식으로 찾게 합니다. |
| `Web/WebPersistence.cs`, `wwwroot/storage.js` | 설정(`Setting.xml`)과 드라이버 파일을 브라우저(IndexedDB)에 저장해 다음 방문 때 되살립니다. |
| `Web/ForceTouchMonitor.cs` | macOS 전용 Force Touch 대신 아무 동작도 하지 않는 대체 코드입니다(2초 누름은 그대로 동작). |
| `Assets/Fonts/` | 브라우저에는 시스템 글꼴이 없으므로 한글·일본어 표시용 Noto Sans CJK(한·일 공용 부분 집합)를 넣었습니다(SIL OFL 1.1, `OFL.txt`). |

## 빌드

.NET 10 SDK와 `wasm-tools` 워크로드가 필요합니다(SkiaSharp의 WebAssembly 네이티브 라이브러리를 링크하기 위함). 저장소 루트에서:

```sh
git submodule update --init --recursive
dotnet workload install wasm-tools
dotnet publish MDPlayer4Web/MDPlayer4Web.csproj -c Release -o MDPlayer4Web/publish
```

`MDPlayer4Web/publish/wwwroot`가 사이트 루트입니다. 정적 파일만 있으므로 아무 웹 서버로 배포할 수 있습니다.

```sh
cd MDPlayer4Web/publish/wwwroot
python3 -m http.server 8080
# 브라우저에서 http://localhost:8080 열기
```

`file://`로 직접 열면 WebAssembly가 막히므로 반드시 HTTP로 띄워야 합니다.

`MDPlayer4Mac` 브랜치에 `MDPlayer4Web/` 또는 `macos/` 변경이 푸시되면 `.github/workflows/web-pages.yml`이 같은 방식으로 빌드해 GitHub Pages에 자동 배포합니다
(저장소 Settings → Pages → Source를 "GitHub Actions"로 두어야 함). Actions 탭에서 수동 실행도 됩니다.

## 사용법

Mac 버전과 같습니다.

- **파일 열기 버튼(A)**: 여러 파일을 한 번에 고를 수 있습니다. 곡이 쓰는 부속 파일(PDX, PCM, 음색 등)도 함께 고르세요.
- **끌어다 놓기**: 파일이나 폴더를 창에 놓으면 재생 목록에 추가하고, 대시보드(검은 버튼 영역)에 놓으면 재생 목록을 교체합니다. 부속 파일이 있는 곡은 폴더째 놓는 것이 가장 확실합니다.
- **드라이버 파일**: MGSDRV.COM, KINROU5.DRV, NDP.BIN, FMP.COM, ZMUSIC.X, yrw801.rom 같은 원본 드라이버·ROM은 페이지에 끌어다 놓으면 드라이버 폴더에 저장되어 이후 계속 쓰입니다. 설정 창의 Other 탭 "Driver files"에서 확인할 수 있습니다.
- 브라우저 정책상 소리는 페이지를 한 번 클릭하거나 키를 누른 뒤부터 나옵니다.

## 지원 형식

- MDPlayer4Mac이 지원하는 칩 음악 형식: VGM/VGZ, XGM/XGZ, ZGM, S98, NSF, GBS, HES, SID, AY, MDX, MND, MGS, MUB/MUC, PMD(M/M2/MZ), MuSICA(BGM/MSD), MoonDriver(MDR), MDL, ZMS/ZMD, NRD, NDP, FMP(OPI/OVI/OZI/MPI/MVI/MZI), MML/MUS/O/OX/OY 등
- 오디오 파일: WAV, OGG는 Mac과 같은 디코더로, MP3·M4A/AAC·FLAC·AIFF는 브라우저 내장 디코더로 변환해 재생합니다(지원 범위는 브라우저마다 다름).

## 제한 사항

- MIDI/RCP/RCS는 macOS의 시스템 신시사이저(AudioToolbox)를 쓰므로 웹 버전에서는 재생할 수 없습니다.
- 설정 창의 "Open Drivers Folder"(Finder 열기)와 "Choose…" 버튼은 브라우저에서 동작하지 않습니다. 드라이버 파일은 페이지에 끌어다 놓으세요.
- 엔진과 화면이 브라우저의 한 스레드에서 함께 돌기 때문에, 곡을 불러올 때나 무거운 곡을 느린 기기에서 재생할 때 소리가 잠깐 끊길 수 있습니다(약 0.5초 분량을 미리 버퍼링합니다).
- 처음 열 때 엔진·화면·글꼴 파일을 압축 기준 약 15–20 MB 내려받습니다.
- Mac에서 고정폭 글꼴(Menlo)로 표시되는 설정 창의 드라이버 목록은 웹에서는 일반 글꼴로 표시됩니다.

## 라이선스

MDPlayer4Mac과 같이 GPLv3를 따릅니다. 원본 MDPlayer © kuma4649, 각 드라이버·음원 코어는 해당 저작자의 라이선스를 따릅니다.
Noto Sans CJK는 SIL Open Font License 1.1을 따릅니다(`Assets/Fonts/OFL.txt`).
