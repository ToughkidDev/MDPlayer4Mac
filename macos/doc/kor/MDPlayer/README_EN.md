# MDPlayer
VGM 파일 등의 플레이어 (메가 드라이브 음원 칩 등을 에뮬레이션하여 연주하는 툴)  
  
[개요]  
  VGM 파일을 건반을 표시하면서 재생하는 툴입니다.  
  (NRD,XGM,S98,MID,RCP,RCS,NSF,GBS,HES,SID,AY,MGS,MDR,MDX,MND,ZMD,ZMS,MUC,MUB,M,M2,MZ,MPI,MVI,MZI,OPI,OVI,OZI,WAV,MP3,AIFF,OGG,M4A,AAC,WMA,FLAC 파일도 지원합니다.)  
  
[주의]  
  작성자께서 SCCI2를 패키지에 포함하는 것을 허락해 주셨습니다. 다만, Fork 등을 통해 바이너리를 배포할 때 SCCI2를 포함하고 싶으시다면 별도로 허락을 받아 주십시오.  
  
  FileAssociationTool(파일 연결 설정 툴)에 대해서는 README_AST.md/README_AST_EN.md를 참조해 주십시오.  
  
  재생 중에는 볼륨에 주의해 주십시오. 버그로 인한 노이즈가 큰 음량으로 재생될 수 있습니다.  
  (특히 한 번도 재생해 본 적 없는 파일을 시험할 때나 프로그램을 업데이트했을 때.)  
  
  사용 중에 문제를 발견하시면 아래 연락처로 연락해 주십시오.  
    Twitter(@kumakumakumaT_T)  
    Github Issues(https://github.com/kuma4649/MDPlayer/issues)  
  !!! 중요 !!!  
  VGMPlay, NRTDRV 및 기타 훌륭한 소프트웨어의 작성자분들께,  
  MDPlayer에 관한 내용으로 VGMPlay, NRTDRV 및 기타 훌륭한 소프트웨어의 작성자분들께 직접 문의하지 말아 주십시오.  
  저희도 최대한 대응하도록 노력하겠지만, 모든 경우에 대응하지 못할 수도 있습니다. 양해 부탁드립니다.  
  
[지원 형식]  
  .VGM (이른바 vgm 파일)  
  .VGZ (gzip으로 압축된 vgm 파일)  
  .NRD (NRTDRV X1에서 OPM 2개와 AY8910을 재생하는 드라이버의 연주 파일)  
  .XGM (MegaDrive용 파일)  
  .ZGM (mml2vgm으로 생성할 수 있는 VGM 확장 형식 파일)  
  .S98 (주로 일본의 레트로 PC용 파일)  
  .MID (StandardMIDI 파일. Format 0/1 지원)  
  .RCP (레코폰 파일. CM6,GSD 전송 가능)  
  .RCS (위의 RCP를 재생할 수 있고 PCM8도 발음할 수 있는 파일)  
  .NSF (NES Sound Format)  
  .GBS (Gameboy Sound Format)  
  .HES (HES 파일)  
  .SID (Commodore용 파일)  
  .AY (ZX Spectrum / Amstrad CPC용 파일)  
  .MGS (MGSDRV 파일. 재생하려면 MGSDRV.COM 필요)  
  .BGM (MuSICA 파일. 재생하려면 KINROU5.DRV 필요)  
  .MDR (MSX의 MoonDriver에서 MoonSound(OPL4)를 재생하는 드라이버 파일)  
  .MDX (MXDRV용 파일)  
  .MND (MNDRV X68000(OPM,OKIM6258) & Marquee Yunit(OPNAx2) 드라이버 연주 파일)  
  .MUC (MUCOM88Windows용 파일)  
  .MUB (MUCOM88Windows용 파일)  
  .M (PMD용 파일)  
  .M2 (PMD용 파일)  
  .MZ (PMD용 파일)  
  .MPI (FMP용 파일. 재생하려면 FMC.EXE, FMP.COM 필요)    
  .MVI (FMP용 파일. 재생하려면 FMC.EXE,FMP.COM 필요)    
  .MZI (FMP 파일. 재생하려면 FMC.EXE,FMP.COM 필요)    
  .OPI (FMP용 파일. 재생하려면 FMP.COM 필요)    
  .OVI (FMP용 파일. 재생하려면 FMP.COM 필요)    
  .OZI (FMP용 파일. 재생하려면 FMP.COM 필요)    
  .ZMS (ZMUSIC2/3용 파일. 재생하려면 ZMUSIC.X,ZMC.X,ZMSC3.X 필요)    
  .ZMD (ZMUSIC3용 파일. 재생하려면 ZMC.X,ZMSC3.X 필요)    
  .ZMD (ZMUSIC2용 파일. 재생하려면 ZMUSIC.X 필요)    
  .WAV (오디오 파일)  
  .MP3 (오디오 파일)  
  .AIF (오디오 파일)  
  .OGG (오디오 파일)  
  .M4A (오디오 파일)  
  .AAC (오디오 파일)  
  .WMA (오디오 파일)  
  .FLAC (오디오 파일)  
  .M3U (재생 목록)  
  
[기능, 특징]  
[기능]  
  현재 다음 메가 드라이브 음원 칩 등을 에뮬레이션하여 재생할 수 있습니다.  
     
      AY8910 , YM2612(YM3438) , SN76489 , RF5C164 , PWM , C140(C219) , OKIM6295 , OKIM6258(PCM8,MPCM 포함)  
      SEGAPCM , YM2151 , YM2203 , YM2413 , YM2608 , YM2609 , YM2610/B 
      HuC6280 , C352     
      K054539 , NES_APU , NES_DMC , NES_FDS , MMC5 , FME7 , N160 , VRC6 , VRC7 , MultiPCM  
      VRC7 , MultiPCM , YMF262 , YMF271 , YMF278B , YMZ280B , DMG , QSound  
      , S5B , GA20 , X1-010 , SAA1099
      YMF271 , RF5C68 , SID , Y8950 , YM3526 , YM3812 , K053260 , K051649(K052539)  
  
  현재 다음 건반 표시를 사용할 수 있습니다.  
     
      YM2612(YM3438), SN76489 , RF5C164  
      AY8910 , C140(C219) , C352 , SEGAPCM , K054539 , GA20 , OKIM6295 , OKIM6258(PCM8,MPCM 포함)  
      Y8950 , YM2151 , YM2203 , YM2413 , YM2608 , YM2609 , YM2610/B 
      YM3526 , YM3812     
      YMF262 , YMF278B , YMZ280B , MultiPCM   
      , HuC6280 , MIDI       
      NES_APU&DMC , NES_FDS , MMC5 , N106(N163) , VRC6 , VRC7 , PPZ8    
  
      채널(건반)을 왼쪽 클릭하면 해당 채널을 마스크합니다.  
      오른쪽 클릭하면 모든 채널의 마스크를 해제합니다.  
      (일부는 여러 수준에서 지원되지 않습니다.)  
    　각 건반 표시 창에서 'ch'를 클릭하면 마스크를 일괄 전환합니다.  


      재생할 파일의 정보를 바탕으로 사용할 건반을 자동으로 열 수 있습니다.  
      (같은 건반은 최대 2개까지 표시할 수 있지만, MIDI 건반은 1개만 열립니다.)  
  
  C#으로 작성되었습니다.  
  
  C#으로 작성되었습니다. - VGMPlay, MAME, DOSBOX의 소스 코드는 
  
  VGMPlay, MAME, DOSBOX의 소스 코드를 참고하여 이식하였습니다.  
  
  FMGen의 소스 코드를 참고하여 이식하였습니다.  
  
  NSFPlay의 소스 코드를 참고하여 이식하였습니다.  
  
  NEZ Plug++의 소스 코드를 참고하여 이식하였습니다.  
  
  libsidplayfp의 소스 코드를 참고하여 이식하였습니다.  
  
  sidplayfp의 소스 코드를 참고하여 이식하였습니다.  
  
  YMEmuWithFilters의 소스 코드를 참고하여 이식하였습니다.  

  NRTDRV의 소스 코드를 참고하여 이식하였습니다.  
  
  MoonDriver의 소스 코드를 참고하여 이식하였습니다.  
  
  MXP의 소스 코드를 참고하여 이식하였습니다.  
  
  MXDRV의 소스 코드를 참고하여 이식하였습니다.  
  
  MNDRV의 소스 코드를 참고하여 이식하였습니다.  
  
  X68Sound의 소스 코드를 참고하여 이식하였습니다.  
  (m_puusan 님/rururutan 님 버전 모두)  
  
  PMD의 소스 코드를 참고하여 이식하였습니다.  
  
  MGSDRV의 코드를 참고하고 있습니다.  
  
  MuSICA의 코드를 참고하고 있습니다.  
  
  FMP의 코드를 참고하고 있습니다.  
  
  run68의 코드를 참고하고 있습니다.  
  
  ZMUSICv2/v3의 코드를 참고하고 있습니다.  

  CVS.EXE의 출력을 참고하여 같은 데이터가 출력되도록 조정하고 있습니다.  
  
  CVS.EXE와 같은 데이터가 출력됩니다.  
  SPPCM에도 대응합니다.  
  SCCI2는 scci2config.exe로 설정해야 합니다.  
  
  GIMIC(C86ctl)을 사용하여 실제 YM2608,YM2151,YMF262로 재생할 수 있습니다.  
  
  Z80dotNet을 사용하고 있습니다.  
  
  버튼은 다음 순서로 배치되어 있습니다.  
     
     설정, 정지, 일시 정지, 페이드 아웃, 이전 곡, 1/4배속 재생, 재생, 4배속 재생, 다음 곡,  
     재생 모드, 파일 열기, 재생 목록,  
     정보 패널 표시, 믹서 패널 표시, 패널 목록 표시, VSTeffect 설정, MIDI 키보드 표시, 표시 배율 변경  
  
  OPN, OPM, OPL의 음색 파라미터를 왼쪽 클릭하면 음색 파라미터가 텍스트로 클립보드에 복사됩니다.  
  파라미터의 형식은 옵션 설정에서 변경할 수 있습니다.  
     
      FMP7 , MDX, MUCOM88(MUSIC LALF), NRTDRV, HuSIC, MML2VGM, .TFI, MGSC, .DMP, .OPNI  
  
  .TFI / .DMP / .OPNI를 선택하면 클립보드 대신 파일로 
  
  YM2612(YM3438)와 YM2151의 연주 데이터를, 품질은 그다지 좋지 않지만 MIDI 파일로 출력할 수 있습니다.  
  VOPMex를 사용하면 FM 음원의 음색 정보를 반영할 수 있습니다.  
  (VOPM이 아니라 VOPMex입니다. ;-P )  
  
  PCM 데이터를 덤프할 수 있지만, WAV로 출력되는 것은 SEGAPCM뿐입니다.  
  
  연주를 wav로 내보낼 수 있습니다.  
  
  MIDI 음원으로 VSTi를 지정할 수 있습니다.  
  
  키보드나 MIDI 키보드로 재생, 정지 등의 조작을 할 수 있습니다.  
  
  재생 목록에서 확장자만 다른 같은 이름의 파일(텍스트, MML, 이미지)을 열 수 있습니다.  
  
  VGM/VGZ 파일에 독자적인 기능을 추가했습니다.  
      RF5C164 2개 동시 연주  
      가사 표시  

  ∙ 명령줄에서 mdc.exe를 사용하여 MDPlayer를 조작할 수 있게 했습니다 ∙ 명령줄에서 MDPlayer를 여는 기능을 추가했습니다 ∙ 명령줄에서 MDPlayer를 여는 기능을 추가했습니다  
  mdc.exe는 주로 STREAM DECK이나 에디터에서 MDPlayer를 조작하기 위해 사용합니다.  
        mdc.exe 명령은 다음과 같습니다  
            PLAY [file].  
            STOP [file  
            NEXT  
            PREV  
            FADEOUT  
            FAST  
            SLOW  
            PAUSE  
            CLOSE  
            LOOP  
            MIXER  
            INFO  
  
  명령줄에서 시작할 때 파일을 지정하면 그 파일을 읽어 들여 재생합니다.  
    보통은 지정한 파일이 재생 목록에 추가되지만, “-PL-” 옵션을 지정하면 추가되지 않습니다.  
      예)
        MDPlayerx64.exe sample.vgm  
          시작할 때 sample.vgm을 읽어 들여 재생을 시작합니다. 재생 목록에 추가합니다.
        MDPlayerx64.exe -PL- sample.vgm
          시작할 때 sample.vgm을 읽어 들여 재생을 시작합니다. 단, 재생 목록에는 추가하지 않습니다.

[약간 알기 어려운 조작] ・각 창의 제목 표시줄이 표시됩니다.  
  각 창의 제목 표시줄을 더블 클릭하면(토글) 항상 맨 앞에 표시됩니다.  
　Shift 키를 누른 채로 애플리케이션을 실행하면 창 위치를 초기화할 수 있습니다.  
　OPN 계열 건반에서는 Shift 키를 누른 채로 FM3OP1~4를 클릭하면 SLOT별로 음소거할 수 있습니다. (단, 캐리어만 해당).  

  
[약간 알기 어려운 설정 항목].  
  Options] 창 > [other] 탭 > [Search paths on additional file].  
    이 텍스트 상자에 경로를 입력하면,  
    이 텍스트 상자에 경로를 입력하면 곡 데이터를 재생할 때 참조되는 추가 파일을 그 위치에서 검색합니다.  
    ; 구분자로 여러 경로를 나열할 수 있습니다.  
    드라이버별로 검색되는 추가 파일은 다음과 같습니다  
      Recomposer (.RCP)  
        .CM6 / .GSD  
      MoonDriver(.MDR)  
        .PCM  
      MXDRV(.MDX)  
        .PDX  
      MNDRV(.MND)  
        .PND  
    PMDDotNET의 경우에는 PMD 환경 변수로 지정된 경로를 참조합니다.  

  레코포저(.RCP) 파일을 재생할 때 .CM6 .GSD를 읽어 들이려면  
    GSD, 설정 화면의 MIDI 장치 설정 시 LA, GS 및 음원 종류를 적절히 선택해야 합니다.  

  X68000 계열 파일(.MDX .ZMS .ZMD .RCS)을 재생할 때는 PCM 재생 드라이버(PCM8, PCM8PP, MPCM, MPCMPP)를 적절히 선택해야 합니다.  
    (이들은 데이터로부터 최적의 드라이버를 자동으로 선택하게 할 수단이 없기 때문입니다.)   
  
[G.I.M.I.C. 관련 정보].  
  SSG 볼륨에 대하여  
    SSG 볼륨은 믹서 화면 오른쪽 아래의 “G.OPN”과 “G.OPNA” 페이더로 조절할 수 있습니다.  
    각각 G.OPN -> YM220.  
      G.OPN -> YM2203(Pri/Sec)으로 설정된 G.I.M.I.C. 모듈  
      G.OPNA -> YM2608(Pri/Sec)으로 설정된 G.I.M.I.C. 모듈  
    설정 정보는 YM2608(Pri/Sec)으로 설정된 G.I.M.I.C. 모듈로 전송됩니다.  
    설정은 재생을 시작할 때만 전송된다는 점에 유의해 주십시오.  
    따라서 재생 중에 페이더를 움직여도 값이 즉시 반영되지 않습니다.  
    초깃값은 다음과 같습니다,  
      .muc(mucom88) -> 63 (PC-8801-11 상당)  
      .mub(mucom88) -> 63 (PC-8801-11 상당)  
      .mnd(MNDRV) -> 31 (PC-9801-86 상당)  
      .s98 -> 31 (PC-9801-86 상당)  
      .vgm -> 31 (PC-9801-86 상당)  
    다음은 드라이버 및 파일 목록입니다.  
    필요에 따라 드라이버별 또는 파일별로 밸런스를 조정하고,  
    파일을 저장해 주십시오(믹서 화면에서 오른쪽 클릭하면 저장 메뉴가 표시됩니다).  
    
    다음 연주 파일은 파일에 기술된 태그를 식별하여 자동으로 설정할 수도 있습니다(TBD).
    
    .S98 파일  
    “system” 태그에서 “8801”이라는 문자를 찾으면 MDPlayer는 “63”을 설정합니다.  
    “9801”이라는 문자를 찾으면 MDPlayer는 “31”을 설정합니다.  
    둘 다 발견되면 “8801”이 우선합니다.  
    찾지 못하면 믹서 화면에서 설정한 값으로 설정됩니다.  
    
    .vgm 파일  
    “systemname” 또는 “systemnamej” 태그에서 “8801”이라는 문자를 찾으면 MDPlayer는 “63”을 설정합니다.  
    “9801”이라는 문자를 찾으면 MDPlayer는 값을 “31”로 설정합니다.  
    둘 다 발견되면 “8801”이 우선합니다.  
    찾지 못하면 값은 믹서 화면에서 설정한 값으로 설정됩니다.  
    
    
  주파수에 대하여  
    모듈 주파수(칩의 마스터 클록)는 파일 형식별로 설정됩니다.  
    설정값은 다음과 같습니다.  
      .vgm -> 파일에 설정된 값을 사용  
      .s98 -> 파일에 설정된 값을 사용  
      .mub(mucom88) -> OPNA:7987200Hz  
      .muc(mucom88) -> OPNA:7987200Hz  
      .nrd(NRTDRV) -> OPM:40000000000Hz  
      .mdx(MXDRV) -> OPM:40000000000Hz  
      .mnd(MNDRV) -> OPM:40000000000Hz OPNA:8000000Hz  
      .mml(PMD) -> OPNA:7987200Hz  
      .m(PMD) -> OPNA:7987200Hz  
      .m2(PMD) -> OPNA:7987200Hz  
      .mz(PMD) -> OPNA:7987200Hz  
      .opi(FMP) -> OPNA:7987200Hz  
      .ovi(FMP) -> OPNA:7987200Hz  
      .ozi(FMP) -> OPNA:7987200Hz  
  
  
[필요한 동작 환경]  
  Windows7(64bit) 이후의 OS. 저는 Windows11Home(x64)을 사용하고 있습니다.  
  XP에서는 동작하지 않습니다.  
  
  NET8이 설치되어 있어야 합니다.  
  
  Visual Studio 2012 Update 4 Visual C++ 재배포 가능 패키지가 설치되어 있어야 합니다.  
  
  Microsoft Visual C++ 2015 Redistributable(x86) - 14.0.23026이 설치되어 있어야 합니다.  
  
  LZH 파일을 사용하려면 UNLHA32.DLL(Ver. 3.0 이상)을 설치해야 합니다.  
  
  오디오를 재생할 수 있는 오디오 장치가 필요합니다.  
  UMX250에 덤으로 딸려 온 UCA222로도 충분합니다. 저는 이것을 사용했습니다.  
  
  가능하다면 SPFM Light (SCCI2 버전) + YM2612 (YM3438) + YM2608 + YM2151 + SPPCM  
  
  가능하다면 GIMIC + YM2608 + YM2151  
  
  YM2608을 에뮬레이션할 때 리듬 음을 재생하려면 다음 오디오 파일이 필요합니다.  
  죄송하지만, 맡기겠습니다
      베이스 드럼 2608_BD.WAV  
      하이햇 2608_HH.WAV  
      림샷 2608_RIM.WAV  
      스네어 드럼 2608_SD.WAV  
      탐탐 2608_TOM.WAV  
      탑 심벌 2608_TOP.WAV  
      (44.1KHz 16bitPCM 모노 비압축 Microsoft WAVE 형식 파일)  
    위 파일이 곡 파일과 같은 위치에 있으면 그것을 읽어 들여 발음합니다.  
    리듬 음을 개별적으로 바꾸고 싶을 때 유용합니다.  
  
  YMF278B를 에뮬레이션할 때 MoonSound 음을 재생하려면 다음 ROM 파일이 필요합니다.  
  죄송하지만, 만드는 방법은 여러분께 맡기겠습니다.  
  	yrw801.rom  
  
  C64 에뮬레이션에는 다음 ROM 파일이 필요합니다.  
  죄송하지만, 만드는 방법은 여러분께 맡기겠습니다.  
  	Kernal , Basic , Character  

  CPU는 빠른 편이 좋습니다.  
  필요한 처리량은 사용하는 Chip 등에 따라 달라집니다.  
  저는 i7-9700K 3.6GHz를 사용하고 있습니다.  
  
  MGSDRV 파일을 재생하려면 다음 파일이 필요합니다.  
  (미리 포함해 두었지만, 필요하시면 공식 사이트에서 받아 주십시오.)
    MGSDRV.COM  

  MuSICA 파일을 재생하려면 다음 파일이 필요합니다.  
  (패키지에 미리 포함되어 있지만, 필요하시면 공식 사이트에서 입수해 주십시오.)
    KINROU5.DRV  

  FMP 파일을 재생하려면 다음 파일이 필요합니다.  
  (공식 사이트, VECTOR 등에서 입수해 주십시오.)
    FMP.COM  
    FMC.EXE  
    PPZ8.COM  

  ZMUSICv2 파일을 재생하려면 다음 파일이 필요합니다.  
  (공식 사이트, VECTOR 등에서 입수해 주십시오.)
    ZMUSIC.X  

  ZMUSICv3 파일을 재생하려면 다음 파일이 필요합니다.  
  (공식 사이트 등에서 입수해 주십시오.)
    ZMC.X  
    ZMSC3.X  

  SCCI2를 사용하여 실제 칩으로 재생하려면 다음 파일이 필요합니다.  
  (공식 사이트 또는 기타 출처에서 입수해 주십시오.)
    scci2.dll  
    scci2config.exe  
  
[동기화 실천]  
    
  SCCI2/GIMIC(C86ctl)와 에뮬레이션(이하 EMU로 약칭)의 소리를 동기화하려고 시도해 왔습니다.  
  환경에 따라 다르기 때문에 무엇이 정답인지는 저도 모릅니다.  
      
    먼저 “Output” 탭에서 오디오 출력에 사용할 장치를 선택합니다.  
    권장 패턴은 “WasapiOut”과 “Share” 또는 “ASIO”를 선택하는 것입니다. 2.  
      
    2. 지연 시간으로 50ms 또는 100ms를 선택합니다. “OK”를 한 번 눌러 EMU만 사용하는 곡을 재생한 다음, 다시 “OK”를 누릅니다.  
    소리가 거칠거나 노이즈가 섞이지 않는지 확인합니다.  
    (제대로 재생되지 않으면 지연 시간을 한 단계 높게 설정합니다). 3.  
      
    3. “Sound” 탭에서 YM2612 (YM3438)에 SCCI를 선택하고 사용할 모듈을 선택합니다.  
    SCCI 전용  
    “Send Wait Signal”과 “Emulate PCM only” 체크박스에 체크합니다.  
    “Emulate PCM only”에 체크하면 PCM 에뮬레이션만 수행됩니다.  
    “Emulate PCM only” 체크박스에 체크하지 않으면 PCM 데이터가 SCCI로 전송되지만, 음질과 템포가 안정되지 않습니다.  
    “Send Wait Signal”에 체크하면 SCCI 템포가 안정되는 것 같습니다.  
    단, “Double wait”에 체크하면 PCM 음질은 좋아지지만 템포가 불안정해지기 쉽습니다.  
      
    지연 그룹에서는 SCCI/GIMIC과 EMU를 0 ms로 설정하고, “Wait for Day Mode” 체크박스에 체크합니다.  
    “opportunistic mode”는 예를 들어 연주 중에 큰 부하가 걸려 SCCI/GIMIC 재생과 EMU 재생이 크게 어긋났을 때 사용됩니다.  
    이 기능은 SCCI/GIMIC의 재생 속도를 조정하여 어긋남을 줄입니다. 단, 지연 재생으로 설정한 (의도적인) 어긋남은 유지됩니다.  
      
    5. SCCI/GIMIC과 EMU가 모두 사용되는 곡을 재생하고, 어느 쪽이 먼저 소리 나는지 주의 깊게 확인합니다.  
    SCCI/GIMIC과 EMU 중 먼저 재생되는 쪽의 지연 시간을 늘리고 곡을 재생하여 확인합니다.  
      
    어긋남이 없어질 때까지 6.5단계를 반복하면 동기화 작업이 완료됩니다. 즐기세요!  
      
    7. SCCI와 GIMIC 사이의 연주 차이에 대하여.  
    SCCI가 빠르면 SCCI의 지연 설정 항목을 조정합니다.  
    GIMIC이 너무 빠르면 GIMIC의 지연 설정 항목을 조정합니다.  
  
[MIDI 키보드 실천]  
  MIDI 키보드가 있으면 그것을 사용하여 YM2612(YM3438)(EMU)로 연주할 수 있습니다.  
  이 기능은 주로 MML 입력을 지원하기 위해 제공됩니다.  
  (일부 기능은 아직 구현 중이어서 현재는 사용할 수 없습니다.)  
  
  현재로서의 사용 방법  
      
    1. 설정 화면에서 사용할 MIDI 키보드를 선택합니다. 2.  
       
    YM2612 (YM3438) 데이터를 재생하는 중에 (CC:97)을 전송합니다.  
        YM2612(YM3438)의 1Ch 음색이 모든 채널에 설정됩니다. 3.  
       
    이제 연주하기만 하면 됩니다.  
    
  주요 기능  
      
    1. 음색 데이터 가져오기  
      각 OPN 또는 OPM 음원이나 건반 표시의 음색 데이터 부분을 클릭하면  
      선택한 채널에 음색 데이터가 복사됩니다. 2.  
      
    2. 연주 모드 전환  
      MONO 모드(단일 채널로 연주)와 POLY 모드(여러 채널로 연주)  
      POLY 모드(여러 채널(최대 6채널)을 사용하여 연주).  
      MONO 모드  
      MONO 모드: 녹음 중에 짧은 프레이즈를 연주하여 MML로 출력합니다.  
      POLY 모드 녹음 중에 화음을 확인하는 용도로 사용하는 것을 상정하고 있습니다. 3.  
      
    3. 채널 노트 로그  
      채널당 최대 100개의 노트를 기록할 수 있습니다. 4.  
      
    4. 채널 노트 로그 MML 변환 기능  
      로그 영역을 클릭하면 발음 기록이 MML로 클립보드에 복사됩니다.  
      음 길이는 출력되지 않습니다. 대응하는 명령은 c d e f g a b o < > 입니다.  
      옥타브 정보는 첫 번째 음에 대해서만 o 명령으로 절대 지정하고,  
      그 이후에는 < 명령 > 명령을 사용한 상대 지정으로 옥타브 정보가 전개됩니다.  
  
    5. 음색 저장 및 불러오기  
      메모리에 256종류의 음색을 저장하거나 불러올 수 있습니다.  
      지정한 형식으로 파일에 출력하거나 파일에서 불러올 수 있습니다.  
      저장 및 불러오기에는 다음 소프트웨어 형식을 사용할 수 있습니다.  
        FMP7  
        MUCOM88(MUSIC LALF/mucomMD2vgm)  
        NRTDRV  
        MXDRV  
        mml2vgm  
      
    6. 간이 음색 편집 (TBD)  
      입력할 파라미터를 선택한 후 수치를 입력하여 편집할 수 있습니다.  
      
  건반 (TBD)  
      
    건반 (TBD)  
      연주 중인 음을 표시합니다. 1.  
      
    1. MONO  
      클릭하면 MONO 모드로 전환됩니다. 전환되면 아이콘이 â(으)로 바뀝니다  
      
    POLY  
      클릭하면 POLY 모드로 전환됩니다. 모드가 전환되면 아이콘이 ♪ 아이콘으로 바뀝니다. 3.  
      
    3. PANIC  
      모든 채널에 키 오프를 전송합니다. (소리가 계속 울릴 때 사용합니다.)  
      
    4．L.CLS  
      모든 채널의 노트 로그를 지웁니다.  
      
    5．TP.PUT  
      선택한 채널의 음색을 TonePallet(메모리 내 음색 저장 영역)에 저장합니다.  
      
    6. TP.GET  
      TonePallet에서 선택한 채널로 음색을 불러옵니다.  
      
    7.T.SAVE  
      TonePallet을 파일로 저장합니다.  
      
    T.LOAD  
      파일에서 TonePallet을 불러옵니다.  
      
    9. 음색 데이터 (6채널분)  
      “-” 또는 “♪”를 클릭하여 채널을 선택하거나 선택 해제합니다.  
      파라미터를 오른쪽 클릭하면 값을 변경합니다. (TBD)  
      파라미터를 왼쪽 클릭하면 컨텍스트 메뉴가 표시됩니다.  
        Copy : 클릭한 음색을 클립보드에 복사합니다.  
        Paste : 클립보드의 음색을 클릭한 음색에 붙여 넣습니다.  
        위 기능에서 사용하는 텍스트 형식은 FORMAT 열의 소프트웨어 이름을 클릭하여 변경할 수 있습니다.  
        복사와 붙여 넣기는 키 입력으로도 할 수 있습니다. 이 경우에는 선택한 채널이 대상이 됩니다.  
        붙여 넣을 때 형식이 자동으로 감지되지 않는다는 점에 유의해 주십시오.  
      “LOG” 옆의 “â”를 클릭하면 해당 채널의 노트 로그가 지워집니다.  
      “LOG”를 클릭하면 MML 데이터가 클립보드에 설정됩니다.  
      
  MIDI 키보드에서의 조작  
    다음은 기본 설정입니다. (설정에서 사용자 지정할 수 있습니다. 설정값을 공란으로 하여 사용하지 않도록 할 수도 있습니다.)  
      
    CC:97(DATA DEC)  
      YM2612 (YM3438)의 1Ch 음색을 모든 채널에 복사합니다. (선택 상태는 무시)  
      
    CC:96(DATA INC)  
      가장 최근의 로그를 하나 삭제합니다. (연주 실수 취소 등을 위한 기능)  
      
    CC:66(SOSTENUTO)  
      MONO 모드에서만, 선택된 줄의 로그를 MML로 변환하여 클립보드에 설정합니다.  
      화면을 클릭했을 때의 처리와 다른 점  
        Ctrl+V(붙여 넣기) 키 입력을 전송합니다.  
        선택한 채널의 노트 로그를 지웁니다.  
        첫 번째 옥타브 명령은 출력되지 않습니다.  
      
      
[감사의 말]  
  이 툴을 만드는 데 다음 분들께 신세를 졌습니다. 또한 다음 소프트웨어와 웹 페이지를 참고하고 이용하고 있습니다.  
  대단히 감사합니다. 
    ・ラエル 님  
    ・とぼけがお 님  
    ・HI-RO 님  
    ・餓死3 님  
    ・おやぢぴぴ 님  
    ・osoumen 님  
    ・なると 님  
    ・hex125 님  
    ・Kitao Nakamura 님  
    ・くろま 님  
    ・かきうち 님  
    ・ぼう☆きち 님  
    ・dj.tuBIG/MaliceX 님  
    ・じごふりん 님  
    ・WING 님  
    ・そんそん 님  
    ・欧場豪 님  
    ・sgq1205 님  
    ・千霧＠ぶっちぎりP(but80) 님  
    ・ひぽぽ 님  
    ・Ichiro Ota 님  

    ・Visual Studio Community 2015/2017  
    ・MinGW/msys  
    ・gcc  
    ・SGDK  
    ・VGM Player  
    ・Git  
    ・SourceTree  
    ・さくらエディター  
    ・VOPMex  
    ・NRTDRV  
    ・MoonDriver  
    ・MXP  
    ・MXDRV  
    ・MNDRV  
    ・MPCM  
    ・X68Sound  
    ・hoot  
    ・XM6 TypeG  
    ・ASLPLAY  
    ・NAUDIO  
    ・VST.NET  
    ・NSFPlay  
    ・CVS.EXE  
    ・KeyboardHook3.cs  
    ・MUCOM88  
    ・MUCOM88windows  
    ・C86ctl 소스  
    ・MGSDRV  
    ・Z80dotNET  
    ・blueMSX  
    ・FMP  
    ・PPZ8  
    ・ZMUSICv2/v3    
    ・RCSMP  
     
    ・SMS Power!  
    ・DOBON.NET  
    ・Wikipedia  
    ・GitHub  
    ・ぬるり。  
    ・Gigamix Online  
    ・MSX Datapack wiki화 계획  
    ・MSX Resource Center  
    ・msxnet  
    ・Xyz 님의 트윗 링크 대상(https://twitter.com/XyzGonGivItToYa/status/1216942514902634496?s=20)  
    ・がんず Work's Diary  
    ・pastraider.com(https://www.pastraiser.com/cpu/gameboy/gameboy_opcodes.html)  
    ・Pan Docs(http://bgb.bircd.org/pandocs.htm#memorymap)  
    ・プラスウイングTV(https://youtu.be/p13EdWrQFjY?si=2L93LDE6SyvINzXX)  


[자주 묻는 질문]  
  
  실행되지 않습니다.  
  
    사례 1  
      파일에 영역 식별자(Zone Identifier)가 추가되어 있어 시작할 때 오류가 발생합니다.  
    영역 식별자는 OS의 보호 기능 중 하나로, 의도하지 않은 파일 실행을 막기 위해 인터넷에서 다운로드한 파일에 자동으로 추가됩니다.  
    그러나 실행할 생각으로 파일을 다운로드한 경우에는 이번처럼 방해가 됩니다.  
    →removeZoneIdent.bat 파일을 더블 클릭하여 실행해 주십시오.  
    이 배치 파일은 영역 식별자를 일괄 제거합니다.  
    다음과 같은 메시지가 표시됩니다.  
        Unknown error occurred.  
        Exception Message: Could not load file or assembly.  
        Could not load file or assembly  
        'file://.... .dll' or one of its dependencies,Operation is not supported.  
        (Exception from HRESULT:xxxx) 

    사례2  
      이 문제는 주로 실제 칩을 사용할 때, SCCI2가 c86ctl을 사용하는 상태로 되어 있기 때문에 발생합니다.  
    MDPlayer도 c86ctl을 사용하므로 서로 충돌하여 시작에 실패합니다.  
    →scci2config.exe를 사용하여 c86ctl 설정 항목의 “enable” 체크를 해제해 주십시오.  
    
    사례3  
      NETframework 버전.  
    →NETframework.  
    NETframework.  
        Unknown error occurred.  
        Exception Message: Could not load file or assembly.  
        Could not load file or assembly  
        'netstandard, Version=... , Culture=... , PublicKeyToken=...' or one of its dependencies. The specified file cannot be found.    
  
    사례X  
      TBD  
  
  
  템포가 불안정하거나, 연주를 시작할 때 곡의 첫 부분이 재생되지 않거나, 곡이 빨리 감기듯 재생됩니다.  
  
    사례 1  
      주로 실제 칩을 사용할 때 발생합니다. 실제 칩은 연주를 시작할 때 처리에 시간이 걸립니다.  
    반면 에뮬레이션 칩은 처리를 즉시 완료합니다.  
    이는 실제 칩이 시간 차를 메우기 위해 에뮬레이션을 따라잡으려 하기 때문입니다.  
    →“Options” 화면: “Sound” 탭: 왼쪽 아래에 있는 “Hiyori” 체크박스의 체크를 해제합니다.  
    사례X  
      TBD  
  
  
  소리가 끊깁니다. 화면 표시가 매우 무겁습니다.  
  
    사례 1  
    이 경우는 필요한 모든 처리가 제한된 시간 안에 완료되지 않을 때 발생합니다.  
    “Options” 화면에서 “Output” 탭을 열고 장치를 전환합니다.  
    환경에 따라 다르므로 여러 장치를 시험해 보시기를 권장합니다.  
    Wasapi와 ASIO가 가장 좋은 응답성을 보이는 경우가 많습니다.  
    장치에 따라서는 “Latency (render buffer)” 값을 조정하면 응답성이 개선될 수 있습니다.  
  
    사례X  
      TBD  
  
  실제 칩으로 재생하면 일부 PCM 소리가 제대로 나지 않습니다.  

    정확한 인터럽트 처리를 할 수 없기 때문에, YM2612 (YM3438)이나 SSG에 의한 PCM 재생 등 실제 칩으로 재생할 때 PCM 소리가 이상하게 들립니다.  
  
[저작권 및 면책]  
  MDPlayer는 GPLv3 라이선스를 따릅니다. LICENSE.txt를 참조해 주십시오.  
  저작권은 작성자에게 있습니다.  
  이 소프트웨어는 어떠한 보증도 없이 “있는 그대로” 제공되며, 작성자는 이 소프트웨어의 사용으로 인해 발생한 어떠한 손해에 대해서도 책임을 지지 않습니다.  
  작성자는 이 소프트웨어의 사용으로 인해 발생한 어떠한 손해에 대해서도 책임을 지지 않습니다.  
  또한 이 소프트웨어에는 저작권 표시 및 이 허가 표시가 필요하지 않습니다.  
  다음 소프트웨어의 소스 코드를 C#용으로 이식·수정하거나 그대로 사용하고 있습니다.  
  이러한 소스와 소프트웨어의 저작권은 각 작성자에게 있습니다. 라이선스는 각 문서를 참조해 주십시오.  
  
  ・VGMPlay  
  ・MAME  
  ・DOSBOX  
  ・FMGen  
  ・NSFPlay  
  ・NEZ Plug++  
  ・libsidplayfp  
  ・sidplayfp  
  ・NRTDRV  
  ・MoonDriver  
  ・MXP  
  ・MXDRV  
  ・MNDRV  
  ・X68Sound  
  (m_puusan 님/rururutan 님 버전 모두)  
  ・MUCOM88  
  ・MUCOM88windows(mucomDotNET)  
  ・M86(M86DotNET)  
  ・VST.NET  
  ・NAudio  
  ・SCCI  
  ・c86ctl  
  ・PMD(PMDDotNET)  
  ・MGSDRV  
  ・勤労5号  
  ・Z80dotNet  
  ・mucom88torym2612  
  ・FMP  
  ・PPZ8  
  ・ZMUSICv2  
  ・ZMUSICv3  
  
  
  