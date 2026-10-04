# 앱에 포함하는 드라이버

이 폴더의 파일(이 README 제외)은 빌드할 때 `MDPlayer4Mac.app/Contents/MacOS/Drivers/`로
복사됨. `MDPlayerCore/DriverFiles.cs`는 설정 창에서 지정한 경로, 사용자 Drivers 폴더
(`~/Library/Application Support/MDPlayer4Mac/Drivers`) 다음으로 이 폴더를 찾음.

## MGSDRV

`MGSDRV.COM`을 이 폴더에 그대로(수정 없이) 넣으면 `.mgs` 재생에 바로 쓰임.

- 배포처: https://www.gigamix.jp/mgsdrv/ (`MGSDR320.LZH`)
- 함께 둔 `MGSDR320.TXT`, `MGSDRV.HED`는 같은 아카이브의 문서이며 `licenses/MGSDRV/`와 같음
- 문서의 「転載、配布について」: 전재·배포는 자유, 단 아카이브 내용은 변경하지 말 것
- 원본 Windows판 MDPlayer도 `MGSDRV.COM`을 동봉해 배포함

다른 드라이버(KINROU5.DRV, NDP.BIN, FMP.COM, ZMUSIC.X 등)와 ROM 이미지는 배포 조건이
확인되지 않았으므로 여기에 넣지 말 것. 사용자가 자신의 Drivers 폴더에 넣어서 씀.
