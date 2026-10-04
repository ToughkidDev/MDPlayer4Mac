# 비주얼라이저 스프라이트 에셋

원본 Windows MDPlayer의 칩 비주얼라이저 스프라이트 시트(`MDPlayer/MDPlayerx64/Resources/*.png`)를
원시 `.rgba32` 픽셀 덤프로 만든 것으로, PNG로 배포하는 대신
`macos/tools/export_sprites.py`로 다시 내보냈습니다. 그 이유는
`macos/MDPlayerUI/Visualizer/SpriteAtlas.cs`의 헤더 주석을 참고하세요(요약하면:
이 샌드박스에서는 한 번도 빌드 검증을 할 수 없었던 Avalonia 자체 PNG 디코더에
의존하지 않기 위함입니다).

형식: 4바이트 너비(int32 LE), 4바이트 높이(int32 LE), 그 뒤에 `width*height`개의 int32 LE
픽셀이 행 우선(row-major) 순서로 이어지며, 각 픽셀은 표준 `0xAARRGGBB` 값입니다.

## 현재 내보낸 항목 (채널 비주얼라이저 + 픽셀 믹서)

| 파일 | 원본 PNG | 사용처 |
|---|---|---|
| `planeSN76489.rgba32` | `Resources/planeSN76489.png` | SN76489 정적 배경 (라벨, 표 테두리, 기본 건반 그리드/모드 텍스트 아트) |
| `planeYM2612.rgba32` | `Resources/planeYM2612.png` | YM2612 정적 배경 (라벨, 표 테두리, 음색 표 그리드, 기본 건반/모드 텍스트 아트) |
| `rVol_01.rgba32` | `Resources/rVol_01.png` | LED 볼륨 바 미터 타일 (`Volume`, 두 칩 공통) |
| `rKBD_01.rgba32` | `Resources/rKBD_01.png` | 피아노 건반 모양 타일 (`DrawKbn`, 두 칩 공통) |
| `rFont_01.rgba32` | `Resources/rFont_01.png` | 8px 폰트, 마스크되지 않은 색 (`DrawFont8`, t=0, 두 칩 공통) |
| `rFont_02.rgba32` | `Resources/rFont_02.png` | 8px 폰트, 마스크된 색 (`DrawFont8`, t=1, 두 칩 공통) |
| `rFont_03.rgba32` | `Resources/rFont_03.png` | 4px 폰트 + 숫자 글리프 (`DrawFont4`/`DrawFont4Int`, 두 칩 공통) |
| `rType_01.rgba32` | `Resources/rType_01.png` | 채널 번호/종류 배지, 마스크되지 않음 (두 칩 공통) |
| `rType_02.rgba32` | `Resources/rType_02.png` | 채널 번호/종류 배지, 마스크됨 |
| `rPan_01.rgba32` | `Resources/rPan_01.png` | 팬 표시기 (`DrawPanP`, 두 칩 공통) |
| `rNESDMC.rgba32` | `Resources/rNESDMC.png` | 오퍼레이터 on/off "슬롯" 아이콘 (`DrawBuffYm2612.Slot`) - 이름과 달리 이 스프라이트 시트는 원본 앱에서 (이식되지 않은) NES DMC 비주얼라이저와 공유되며, YM2612 전용 아트가 아닙니다 |
| `rFader.rgba32` | `Resources/rFader.png` | Windows `frmMixer2`의 페이더 레일, 마스터/칩 노브, 2px 레벨 바 타일 (`MixerVisualizer`) |
| `planeMixer.rgba32` | `Resources/planeMixer.png` | Windows 믹서 레이아웃 및 팔레트 참고용. Mac 믹서는 이 고정된 전체 칩 배경을 쓰는 대신 활성 슬롯을 동적으로 그립니다. |

`rVol_01`/`rKBD_01`/`rFont_01`/`rFont_02`/`rFont_03`/`rType_01`/`rType_02`/`rPan_01`은
원본 앱에서 실제로 동일한 공유 스프라이트 시트로, `DrawBuffSn76489.cs`와 `DrawBuffYm2612.cs`
양쪽에서 참조됩니다. 각 파일은 서로를 교차 참조하지 않고 자신의 정적 필드에 자체 사본을
로드하므로, 어떤 VGM이 둘 중 하나의 칩만 구동하더라도 어느 칩의 비주얼라이저든 올바르게
동작합니다(그 이유는 `DrawBuffYm2612.cs`의 헤더 주석 참고).

여러 변형이 있는 스프라이트 시트는 각각 `tp=0`(소프트웨어 에뮬레이션 칩) 변형만
내보냈습니다. 이 포트의 엔진은 실제 하드웨어 출력(`tp=1`/`tp=2`)을 지원하지 않으므로
그 변형들은 선택될 일이 없습니다. `DrawBuffSn76489.cs`/`DrawBuffYm2612.cs`의
헤더 주석을 참고하세요.

## 추가하는 방법 (예: 향후 칩 비주얼라이저용)

1. 대상 `frmXxxx.cs`/그 `DrawBuff.screenInitXxxx`가 실제로 참조하는 스프라이트 시트를
   찾습니다(해당 Windows 소스에서 `ResMng.ImgDic["..."]`를 grep).
2. `python3 macos/tools/export_sprites.py <source PNGs...> macos/MDPlayerUI/Assets/Visualizer/`
3. 그리기 함수(`DrawBuffSn76489.cs`/`DrawBuffYm2612.cs`와 같은 위치에 같은 패턴으로 새
   `DrawBuffXxx.cs`)와 칩 창 로직(`Sn76489Visualizer.cs`/`Ym2612Visualizer.cs`와 같은 위치에
   새 `XxxVisualizer.cs`)을 이식합니다.
