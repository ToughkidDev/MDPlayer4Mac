# 설치 프로그램 제작 키트 v1.0

## 절차

1. bin.zip의 내용을 mdplayer 디렉터리로 압축 해제
2. personal.pfx를 생성
3. 서명 기능은 환경에 따라 동작하지 않으므로 배치 파일 안의 경로를 조정
4. NSIS로 _inst_mui2.nsi를 컴파일

## 디렉터리 설명
* `mdplayer`
bin 파일의 내용과 동일합니다.

예시로 MDPlayer의 위치:
`mdplayer\MDPlayerx64.exe`

* `images`
이미지 리소스입니다.

## 파일 목록 생성

`MakeFileList.ps1`
로 다음 스크립트를 생성할 수 있습니다.

`00inst.nsh`
설치용 파일 목록

`01uninst.nsh`
제거용 파일 목록


## 서명용 파일 생성

personal.pfx를 생성합니다.
openssl 등을 이용하여 생성합니다.
pfx 파일은 개인 키를 포함하므로 취급에 주의해 주세요.

다음은 한 가지 예입니다.

```sh
# 개인 키
openssl genrsa -out private.key

# 서명 요청
openssl req -new -sha256 -key private.key -out request.csr

# 인증서
openssl x509 -req -in request.csr -signkey private.key -out cert.crt -days 3650

# pfx 파일
openssl pkcs12 -export -inkey private.key -in cert.crt -out personal.pfx
```

## 서명 기능에 관하여

서명 기능은 Windows SDK에 있는 signtool.exe를 사용합니다.
SignExe.bat 안의 경로는 Windows SDK의 버전에 맞게 조정해 주세요.

## 파일 서명 기능

`SignMDPlayer.ps1`로 mdplayer 이하의 파일에 서명할 수 있습니다.
파일 목록은 스크립트 안에 적혀 있으며, `MakeExeList.ps1`로
그 원형을 얻을 수 있습니다.

`SignMDPlayer.ps1`은 Windows SDK가 포함된 Visual Studio의 PowerShell에서 실행합니다.
예: Developer PowerShell for VS 2022

## 설치 프로그램 서명 기능

NSIS 컴파일 시 `SignExe.bat`으로 자동으로 서명합니다.
SignExe.bat 안의 `signtool.exe` 경로는 조정해 주세요.

## 컴파일

NSIS v3.08로 `_inst_mui2.nsi`를 컴파일합니다.




