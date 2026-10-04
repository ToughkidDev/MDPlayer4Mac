# 트랜스포트 스프라이트

원본 Windows `frmMain`의 16×16 조작 버튼 스프라이트로, `TransportSpriteButton`에서 쓰도록
런타임에 의존하지 않는 `.rgba32` 형식으로 내보낸 것입니다:

- `cc*`: 일반
- `ch*`: 호버
- `ci*`: 활성/눌림

macOS 플레이어는 이들을 2× 최근접 이웃(nearest-neighbour) 스케일링으로 그려, Windows의
픽셀 아트 트랜스포트 레이아웃을 유지합니다. `macos/tools/export_sprites.py`로 다시 생성하거나,
Pillow가 없는 Mac에서는 `macos/tools/export_sprites.swift`를 사용하세요.

`ccZoom`/`chZoom`/`ciZoom`은 Windows의 Zoom 버튼 스프라이트입니다. macOS 대시보드에서는
모든 채널 화면을 일반 2× 표시와 축소된 1× 표시 사이에서 전환합니다.

`ccSetting`/`chSetting`/`ciSetting`은 원본 Windows의 Setting 버튼 스프라이트입니다. 이 버튼은
macOS 포트 고유의 설정 창을 여는데, 현재 이 창에는 Windows와 호환되는
Output 탭과 About 탭이 들어 있습니다.
