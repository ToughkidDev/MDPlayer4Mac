ToughkidDev에 의해 fork되어 MDPlayer의 Mac OS 앱 버전 개발.

# MDPlayer
VGM 파일 등의 Player(메가드라이브 음원 칩 등의 에뮬레이션에 의한 연주 툴)  
  
[개요]  
  이 툴은 건반 표시를 하면서 VGM 파일을 재생합니다.  
  (NRD,NDP,XGM,S98,MID,RCP,RCS,NSF,GBS,HES,SID,AY,MGS,MDR,MDX,MND,ZMD,ZMS,MUC,MUB,M,M2,MZ,MPI,MVI,MZI,OPI,OVI,OZI,MUS,O,OX,OY,WAV,MP3,AIFF,OGG,M4A,AAC,WMA,FLAC 파일에도 대응.)  
  
[주의]  
  ・작자님으로부터 SCCI2 동봉 허가를 받았습니다. 단, Fork 등을 하여 바이너리를 배포할 때 SCCI2를 동봉하고 싶은 경우에는 별도로 허가를 받으시기 바랍니다.  
  
  ・FileAssociationTool(파일 연결 설정 툴)에 대해서는 README_AST.md/README_AST_EN.md를 참조해 주세요.  
  
  ・재생 시의 음량에 주의해 주세요. 버그로 인한 잡음이 큰 음량으로 재생되는 경우도 있습니다.  
  (특히 재생한 적 없는 파일을 시험하는 경우나, 프로그램을 업데이트한 경우.)  
  
  ・사용 중에 문제를 발견한 경우에는 번거로우시겠지만 아래로 연락 주세요.  
    Twitter(@kumakumakumaT_T)  
    Github Issues(https://github.com/kuma4649/MDPlayer/issues)  
  !!중요!!  
  VGMPlay나 NRTDRV, 그 외 훌륭한 소프트웨어의 작자분들께,  
  MDPlayer에 관한 연락이 직접 가는 일이 없도록 부탁드립니다.  
  단, 가능한 한 대응할 생각이지만, 희망에 부응하지 못하는 경우도 많이 있습니다. 양해 부탁드립니다.  
  
[대응 포맷]  
  .VGM (이른바 vgm 파일)  
  .VGZ (vgm 파일을 gzip한 것)  
  .NRD (NRTDRV X1에서 OPM 2개와 AY8910을 울리는 드라이버의 연주 파일)  
  .NDP (NDP 파일 연주하려면 NRD.BIN이 필요합니다)  
  .XGM (MegaDrive용 파일)  
  .ZGM (mml2vgm으로 생성 가능한 VGM 확장 포맷 파일)  
  .S98 (주로 일본제 레트로 PC용 파일)  
  .MID (StandardMIDI 파일. 포맷 0/1 대응)  
  .RCP (레코폰 파일 CM6,GSD 송신 가능)  
  .RCS (위의 RCP를 연주하면서 PCM8도 발음할 수 있는 파일)  
  .NSF (NES Sound Format)  
  .GBS (Gameboy Sound Format)  
  .HES (HES 파일)  
  .SID (코모도어용 파일)  
  .AY  (ZX Spectrum / Amstrad CPC용 파일)  
  .MGS (MGSDRV 파일 연주하려면 MGSDRV.COM이 필요합니다)  
  .BGM (MuSICA 파일 연주하려면 KINROU5.DRV가 필요합니다)  
  .MDR (MoonDriver MSX에서 MoonSound(OPL4)를 울리는 드라이버의 연주 파일)  
  .MDX (MXDRV용 파일)  
  .MND (MNDRV X68000(OPM,OKIM6258) & まーきゅりーゆにっと(OPNAx2)를 사용하는 드라이버의 연주 파일)  
  .MUC (MUCOM88Windows용 파일)  
  .MUB (MUCOM88Windows용 파일)  
  .M   (PMD용 파일)  
  .M2  (PMD용 파일)  
  .MZ  (PMD용 파일)  
  .MPI (FMP용 파일 연주하려면 FMC.EXE,FMP.COM이 필요합니다)    
  .MVI (FMP용 파일 연주하려면 FMC.EXE,FMP.COM이 필요합니다)    
  .MZI (FMP용 파일 연주하려면 FMC.EXE,FMP.COM이 필요합니다)    
  .OPI (FMP용 파일 연주하려면 FMP.COM이 필요합니다)    
  .OVI (FMP용 파일 연주하려면 FMP.COM이 필요합니다)    
  .OZI (FMP용 파일 연주하려면 FMP.COM이 필요합니다)    
  .ZMS (ZMUSIC2/3용 파일 연주하려면 ZMUSIC.X,ZMC.X,ZMSC3.X가 필요합니다)    
  .ZMD (ZMUSIC3용 파일 연주하려면 ZMC.X,ZMSC3.X가 필요합니다)    
  .ZMD (ZMUSIC2용 파일 연주하려면 ZMUSIC.X가 필요합니다)    
  .MUS (みゅあっぷ용 파일)    
  .O   (みゅあっぷ용 파일)    
  .OX  (みゅあっぷ용 파일)    
  .OY  (みゅあっぷ용 파일)    
  .WAV (음성 파일)  
  .MP3 (음성 파일)  
  .AIF (음성 파일)  
  .OGG (음성 파일)  
  .M4A (음성 파일)  
  .AAC (음성 파일)  
  .WMA (음성 파일)  
  .FLAC (음성 파일)  
  .M3U (플레이리스트)  
  
[기능, 특징]  
  ・현재, 아래와 같은 주로 메가드라이브 계열 음원 칩의 에뮬레이션에 의한 재생이 가능합니다.  
     
      AY8910    , YM2612(YM3438) , SN76489 , RF5C164 , PWM     , C140(C219) , OKIM6295 , OKIM6258(PCM8,MPCM 포함)  
      , SEGAPCM , YM2151         , YM2203  , YM2413  , YM2608  , YM2609     , YM2610/B 
      , HuC6280 , C352     
      , K054539 , NES_APU        , NES_DMC , NES_FDS , MMC5    , FME7       , N160     , VRC6  
      , VRC7    , MultiPCM       , YMF262  , YMF271  , YMF278B , YMZ280B    , DMG      , QSound  
      , S5B     , GA20           , X1-010  , SAA1099
      , RF5C68  , SID            , Y8950   , YM3526  , YM3812  , K053260    , K051649(K052539)  
  
  ・현재, 아래와 같은 건반 표시가 가능합니다.  
     
      YM2612(YM3438), SN76489    , RF5C164  
      , AY8910      , C140(C219) , C352    , SEGAPCM    , K054539 , GA20 , OKIM6295 , OKIM6258(PCM8,MPCM 포함)  
      , Y8950       , YM2151     , YM2203  , YM2413     , YM2608 , YM2609 , YM2610/B 
      , YM3526      , YM3812     
      , YMF262      , YMF278B    , YMZ280B , MultiPCM   
      , HuC6280     , MIDI       
      , NES_APU&DMC , NES_FDS    , MMC5    , N106(N163) , VRC6   , VRC7   , PPZ8    
  
      채널(건반)을 왼쪽 클릭하면 마스크시킬 수 있습니다.  
      오른쪽 클릭하면 모든 채널의 마스크를 해제합니다.  
      (여러 수준에서 대응하지 않는 것도 있음)  
    　각 건반 표시 창에서 'ch'를 클릭하면 일괄로 마스크를 전환합니다.  


      재생할 파일의 정보로부터 사용할 건반을 자동으로 열 수 있습니다.  
      (같은 건반을 2개까지 표시할 수 있지만, MIDI 건반은 하나만 엽니다.)  
  
  ・C#으로 작성되어 있습니다.  
  
  ・VGMPlay,MAME,DOSBOX의 소스를 참고, 이식하고 있습니다.  
  
  ・FMGen의 소스를 참고, 이식하고 있습니다.  
  
  ・NSFPlay의 소스를 참고, 이식하고 있습니다.  
  
  ・NEZ Plug++의 소스를 참고, 이식하고 있습니다.  
  
  ・libsidplayfp의 소스를 참고, 이식하고 있습니다.  
  
  ・sidplayfp의 소스를 참고, 이식하고 있습니다.  
  
  ・YMEmuWithFilters의 소스를 참고하고 있습니다.  

  ・NRTDRV의 소스를 참고, 이식하고 있습니다.  
  
  ・NDP의 소스를 참고, 이식하고 있습니다.  
  
  ・MoonDriver의 소스를 참고, 이식하고 있습니다.  
  
  ・MXP의 소스를 참고, 이식하고 있습니다.  
  
  ・MXDRV의 소스를 참고, 이식하고 있습니다.  
  
  ・MNDRV의 소스를 참고, 이식하고 있습니다.  
  
  ・X68Sound의 소스를 참고, 이식하고 있습니다.  
  (m_puusan님/rururutan님 버전 모두)  
  
  ・PMD의 소스를 참고, 이식하고 있습니다.  
  
  ・MGSDRV의 코드를 참고하고 있습니다.  
  
  ・MuSICA의 코드를 참고하고 있습니다.  
  
  ・FMP의 코드를 참고하고 있습니다.  
  
  ・run68의 코드를 참고하고 있습니다.  
  
  ・ZMUSICv2/v3의 코드를 참고하고 있습니다.  

  ・みゅあっぷ의 소스를 참고, 이식하고 있습니다.  
  
  ・CVS.EXE의 출력을 참고하여 같은 데이터가 출력되도록 조정하고 있습니다.  
  
  ・SCCI2를 이용하여 실제 YM2612(YM3438),SN76489,YM2608,YM2151,YMF262 칩으로 재생이 가능합니다.  
  또한 SPPCM에도 대응하고 있습니다.  
  SCCI2는 scci2config.exe로 미리 설정을 해 두는 것이 필수입니다.  
  
  ・GIMIC(C86ctl)을 이용하여 실제 YM2608,YM2151,YMF262 칩으로 재생이 가능합니다.  
  
  ・Z80dotNet을 이용하고 있습니다.  
  
  ・버튼은 아래 순서로 나열되어 있습니다.  
     
     설정, 정지, 일시정지, 페이드아웃, 이전 곡, 1/4배속 재생, 재생, 4배속 재생, 다음 곡,  
     플레이 모드, 파일 열기, 플레이리스트,  
     정보 패널 표시, 믹서 패널 표시, 패널 리스트 표시, VSTeffect 설정, MIDI 건반 표시, 표시 배율 변경  
  
  ・OPN,OPM,OPL 계열의 음색 파라미터를 왼쪽 클릭하면 클립보드에 음색 파라미터를 텍스트로 복사합니다.  
  파라미터의 형식은 옵션 설정에서 변경 가능합니다.  
     
      FMP7 , MDX , MUCOM88(MUSIC LALF) , NRTDRV , HuSIC , MML2VGM , .TFI , MGSC , .DMP , .OPNI  
  
  에 대응하고 있으며, .TFI / .DMP / .OPNI를 선택한 경우에는 클립보드 대신 파일로 출력합니다.  
  
  ・완성도는 아직 부족하지만, YM2612(YM3438) , YM2151 의 연주 데이터를 MIDI 파일로 출력할 수 있습니다.  
  VOPMex를 사용하면, FM 음원의 음색 정보도 반영시킬 수 있습니다.  
  (VOPM이 아니라, VOPMex입니다. ;-P )  
  
  ・PCM 데이터를 덤프할 수 있습니다. SEGAPCM의 경우만 WAV로 출력합니다.  
  
  ・연주를 wav로 내보낼 수 있습니다.  
  
  ・MIDI 음원으로 VSTi를 지정할 수 있습니다.  
  
  ・키보드, MIDI 키보드로 재생, 정지 등의 조작이 가능합니다.  
  
  ・플레이리스트에서, 재생 중인 파일과 이름이 같고 확장자가 다른 파일(Text,MML,Image)을 열 수 있습니다.  
  
  ・VGM/VGZ 파일에 독자 기능을 추가하고 있습니다.  
      RF5C164의 Dual 연주  
      가사 표시  

  ・커맨드 라인에서 mdc.exe를 사용하여 MDPlayer를 조작할 수 있게 했습니다♪  
  mdc.exe는 주로 STREAM DECK이나 에디터 등에서 MDPlayer를 조작하는 데 사용합니다.  
        mdc.exe의 커맨드는 아래와 같습니다.  
            PLAY [file]  
            STOP  
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
  
  ・커맨드 라인에서 기동 시에 파일을 지정하면 그것을 읽어 들여 재생합니다.  
    보통, 지정한 파일은 플레이리스트에 추가되지만 "-PL-" 옵션을 지정하면 추가하지 않습니다.  
      예)
        MDPlayerx64.exe sample.vgm  
          기동 시에 sample.vgm을 읽어 들여 재생을 시작한다. 플레이리스트에 추가한다.
        MDPlayerx64.exe -PL- sample.vgm
          기동 시에 sample.vgm을 읽어 들여 재생을 시작한다. 단, 플레이리스트에 추가하지 않는다.

[조금 알기 어려운 조작]  
  ・각 창의 타이틀 바를 더블 클릭(토글)하면 항상 맨 앞에 표시되게 됩니다.  
　・Shift 키를 누른 채로 앱을 기동하면, 창의 위치를 초기화할 수 있도록 기능을 추가했습니다.  
　・OPN 계열 건반에서는 Shift 키를 누른 채로 FM3OP1～4를 클릭하면, SLOT별 뮤트가 가능합니다. (단, 캐리어만)  

  
  
[조금 알기 어려운 설정 항목]  
  ・[Options] 창 > [other] 탭 > [Search paths on additional file]  
    이 텍스트 박스에 경로를 입력해 두면,  
    곡 데이터를 재생할 때 추가로 참조되는 파일을 그 위치에서 검색하게 됩니다.  
    경로는 ;로 구분하여 여러 개 나열할 수 있습니다.  
    드라이버별로 추가로 찾는 파일은 아래와 같습니다.  
      ・RECOMPOSER(.RCP)  
        .CM6 / .GSD  
      ・MoonDriver(.MDR)  
        .PCM  
      ・MXDRV(.MDX)  
        .PDX  
      ・MNDRV(.MND)  
        .PND  
    또한, PMDDotNET의 경우에는 환경 변수 PMD로 지정된 경로를 참조합니다.  

  ・RECOMPOSER(.RCP) 파일 연주 시에 .CM6 .GSD를 읽어 들이게 하려면  
    설정 화면의 MIDI 디바이스 설정 시에 각각 LA, GS로 음원 종류를 적절히 선택해 둘 필요가 있습니다.  

  ・X68000 계열 파일(.MDX .ZMS .ZMD .RCS) 연주 시에는 PCM 재생 드라이버(PCM8,PCM8PP,MPCM,MPCMPP)를 적절히 선택할 필요가 있습니다.  
    (이 파일들은 데이터로부터 최적의 드라이버를 자동 선택하게 할 수단을 가지고 있지 않기 때문)   

  ・muap의 PCM을 재생시키려면 TONES.DTA,PCM.DTA,PCM.TBL이 필요하므로 원래 배포처에서 입수하여 MDPlayer를 설치한 폴더에 넣어 주세요.  
  또한, 사용자 정의 PCM을 사용하고 싶은 경우에는 MDPlayer를 설치한 폴더에 USRDEF 폴더를 만들고 필요한 PCM 파일을 넣어 주세요.  
  
  
[G.I.M.I.C. 관련 정보]  
  ・SSG volume에 대해  
    SSG volume은 믹서 화면 오른쪽 아래의 「G.OPN」「G.OPNA」 페이더로 조절해 주세요.  
    각각  
      G.OPN    ->  YM2203(Pri/Sec)에 설정한 G.I.M.I.C.의 모듈  
      G.OPNA   ->  YM2608(Pri/Sec)에 설정한 G.I.M.I.C.의 모듈  
    으로 설정 정보가 송신됩니다.  
    또한, 설정은 재생 시작 시에만 송신됩니다.  
    따라서 연주 중에 페이더를 움직여도 그 값이 즉시 반영되지는 않습니다.  
    초기값으로는,  
      .muc(mucom88)  ->  63 (PC-8801-11 상당)  
      .mub(mucom88)  ->  63 (PC-8801-11 상당)  
      .mnd(MNDRV)    ->  31 (PC-9801-86 상당)  
      .s98           ->  31 (PC-9801-86 상당)  
      .vgm           ->  31 (PC-9801-86 상당)  
    을 설정하고 있습니다.  
    필요에 따라 드라이버별 또는 파일별로 밸런스를 조절하고,  
    저장(믹서 화면에서 오른쪽 클릭하면 저장 메뉴가 표시됨)해 주세요.  
    
    또한, 아래의 연주 파일은 파일 안에 기술되어 있는 태그를 판별하여 자동으로 설정하는 것도 가능합니다(TBD).
    
    .S98 파일  
    「system」 태그 안에서 「8801」이라는 문자열을 찾으면 MDPlayer는 「63」을 설정합니다.  
    「9801」이라는 문자열을 찾으면 MDPlayer는 「31」을 설정합니다.  
    둘 다 찾은 경우에는 「8801」을 우선합니다.  
    찾지 못한 경우에는 믹서 화면에서 설정한 값이 됩니다.  
    
    .vgm 파일  
    「systemname」「systemnamej」 태그 안에서 「8801」이라는 문자열을 찾으면 MDPlayer는 「63」을 설정합니다.  
    「9801」이라는 문자열을 찾으면 MDPlayer는 「31」을 설정합니다.  
    둘 다 찾은 경우에는 「8801」을 우선합니다.  
    찾지 못한 경우에는 믹서 화면에서 설정한 값이 됩니다.  
    
    
  ・주파수에 대해  
    파일 형식별로 모듈의 주파수(칩의 마스터 클럭)를 설정합니다.  
    설정값은 아래와 같습니다.  
      .vgm           ->  파일 안에 설정되어 있는 값을 사용  
      .s98           ->  파일 안에 설정되어 있는 값을 사용  
      .mub(mucom88)  ->  OPNA:7987200Hz  
      .muc(mucom88)  ->  OPNA:7987200Hz  
      .nrd(NRTDRV)   ->  OPM:4000000Hz  
      .mdx(MXDRV)    ->  OPM:4000000Hz  
      .mnd(MNDRV)    ->  OPM:4000000Hz  OPNA:8000000Hz  
      .mml(PMD)      ->  OPNA:7987200Hz  
      .m(PMD)        ->  OPNA:7987200Hz  
      .m2(PMD)       ->  OPNA:7987200Hz  
      .mz(PMD)       ->  OPNA:7987200Hz  
      .opi(FMP)      ->  OPNA:7987200Hz  
      .ovi(FMP)      ->  OPNA:7987200Hz  
      .ozi(FMP)      ->  OPNA:7987200Hz  
  
  
[필요한 동작 환경]  
  ・Windows7(64bit) 이후의 OS. 저는 Windows11Home(x64)을 사용하고 있습니다.  
  XP에서는 동작하지 않습니다.  
  
  ・.NET8이 설치되어 있어야 합니다.  
  
  ・Visual Studio 2012 업데이트 4의 Visual C++ 재배포 가능 패키지가 설치되어 있어야 합니다.  
  
  ・Microsoft Visual C++ 2015 Redistributable(x86) - 14.0.23026이 설치되어 있어야 합니다.  
  
  ・LZH 파일을 사용하는 경우에는 UNLHA32.DLL(Ver3.0 이후)이 설치되어 있어야 합니다.  
  
  ・ZDF 파일을 사용하는 경우에는 lzz.r(Ver0.57 이후?)을 MDPlayer와 같은 위치에 놓아 두어야 합니다.  
  
  ・음성을 재생할 수 있는 오디오 디바이스가 필수입니다.  
  어느 정도 성능이 있는 것이 필요합니다. UMX250에 덤으로 딸려 있던 UCA222로도 충분합니다. 저는 이것을 사용했었습니다.  
  
  ・가능하다면, SPFM Light(SCCI2 대응판)＋YM2612(YM3438)＋YM2608＋YM2151＋SPPCM  
  
  ・가능하다면, GIMIC＋YM2608＋YM2151  
  
  ・YM2608 에뮬레이션 시, 리듬음을 울리기 위해 아래의 음성 파일이 필요합니다.  
  작성 방법은 죄송하지만 각자에게 맡기겠습니다.  
      
      베이스 드럼     2608_BD.WAV  
      하이햇          2608_HH.WAV  
      림샷            2608_RIM.WAV  
      스네어 드럼     2608_SD.WAV  
      탐탐            2608_TOM.WAV  
      톱 심벌         2608_TOP.WAV  
      (44.1KHz 16bitPCM 모노럴 무압축 Microsoft WAVE 형식 파일)  
    곡 파일과 같은 위치에 위의 파일이 존재하는 경우에는 그쪽을 읽어 들여 발음합니다.  
    리듬음을 독자적으로 바꾸고 싶은 경우에 편리합니다.  
  
  ・YMF278B 에뮬레이션 시, MoonSound의 음색을 울리기 위해 아래의 ROM 파일이 필요합니다.  
  작성 방법은 죄송하지만 각자에게 맡기겠습니다.  
  	yrw801.rom  
  
  ・C64 에뮬레이션 시, 아래의 ROM 파일이 필요합니다.  
  작성 방법은 죄송하지만 각자에게 맡기겠습니다.  
  	Kernal , Basic , Character  
  
  ・어느 정도 빠른 CPU.  
  사용하는 Chip 등에 따라 필요한 처리량이 달라집니다.  
  저는 i7-9700K 3.6GHz를 사용하고 있습니다.  
  
  ・MGSDRV의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (미리 동봉해 두었지만, 필요하다면 공식 사이트에서 입수해 주세요.)
    MGSDRV.COM  

  ・MuSICA의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (미리 동봉해 두었지만, 필요하다면 공식 사이트에서 입수해 주세요.)
    KINROU5.DRV  

  ・NDP의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (공식 사이트에서 입수해 주세요.)
    NDP.BIN  

  ・FMP의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (공식 사이트, VECTOR 등에서 입수해 주세요.)
    FMP.COM  
    FMC.EXE  
    PPZ8.COM  

  ・ZMUSICv2의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (공식 사이트 등에서 입수해 주세요.)
    ZMUSIC.X  

  ・ZMUSICv3의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (공식 사이트 등에서 입수해 주세요.)
    ZMC.X  
    ZMSC3.X  
  
  ・みゅあっぷ의 파일을 연주하려면 아래의 파일이 필요합니다.  
  (공식 사이트 등에서 입수해 주세요.)
    TONES.DTA  
  
  ・SCCI2를 사용하여 실제 칩으로 연주하려면 아래의 파일이 필요합니다.  
  (공식 사이트 등에서 입수해 주세요.)
    scci2.dll  
    scci2config.exe  

[동기화 가이드]  
    
  ・SCCI2/GIMIC(C86ctl)과 에뮬레이션(이하 EMU로 약칭)에 의한 소리를 동기화시키는 데에는 요령이 필요합니다.  
  환경에 따라서도 다르기 때문에 무엇이 정답인지는 알 수 없지만, 제 환경에서의 조정 순서를 소개합니다.  
      
    １．먼저, [Output] 탭에서 음성 출력에 사용할 디바이스를 선택합니다.  
    추천하는 것은 WasapiOut에서 공유를 선택하거나, 또는 ASIO를 선택하는 패턴입니다.  
      
    ２．지연 시간은 50ms나 100ms를 선택합니다. 여기서 한 번 [OK]를 눌러 EMU만 사용하는 곡을 재생하고  
    소리가 거칠어지거나 지직거리는 노이즈가 섞이지 않는지 확인합니다.  
    (만약 깨끗하게 재생되지 않는 경우에는 지연 시간을 한 단계 크게 설정합니다.)  
      
    ３．[Sound] 탭에서 YM2612(YM3438)의 SCCI를 선택하고 사용할 모듈을 선택합니다.  
    SCCI만  
    체크박스는 「Send Wait Signal」과 「Emulate PCM only」에 체크해 주세요.  
    「Emulate PCM only」에 체크하면 PCM만 에뮬레이션하게 됩니다.  
    체크하지 않은 경우에는 SCCI에 PCM 데이터를 보내게 되지만 음질, 템포가 안정되지 않습니다.  
    「Send Wait Signal」을 하면 SCCI의 템포가 안정되는 것 같습니다.  
    하지만 「Double wait」에 체크하면 PCM의 음질은 올라가지만 템포가 흐트러지는 경향이 있습니다.  
      
    ４．지연 연주 그룹은 일단 SCCI/GIMIC도 EMU도 0ms로 설정하고 「日和見モード(관망 모드)」에는 체크해 주세요.  
    「日和見モード(관망 모드)」는, 예를 들어 연주 중에 큰 부하가 걸려 SCCI/GIMIC의 재생과 EMU의 재생이 크게 어긋난 경우에  
    SCCI/GIMIC의 재생 속도를 조정하여 어긋남을 줄이는 기능입니다. 단, 지연 연주에서 설정한(의도한) 어긋남은 계속 유지합니다.  
      
    ５．SCCI/GIMIC과 EMU 양쪽이 사용되는 곡을 재생하고, 어느 쪽이 먼저 울리는지 주의 깊게 확인합니다.  
    SCCI/GIMIC과 EMU 중 먼저 연주되는 쪽의 지연 연주 시간을 늘리고 곡을 재생하여 확인합니다.  
      
    ６．５의 순서를 어긋남이 없어질 때까지 반복하면 동기화 작업은 완료입니다. 즐기세요!  
      
    ７．SCCI와 GIMIC의 연주 어긋남에 대해.  
    SCCI가 빠른 경우에는 SCCI의 딜레이 설정 항목을 조정합니다.  
    GIMIC이 빠른 경우에는 GIMIC의 딜레이 설정 항목을 조정합니다.  
  
  
[MIDI 건반 가이드]  
  ・MIDI 키보드를 준비하면, 그것을 사용하여 YM2612(YM3438)(EMU)로 발음시킬 수 있습니다.  
  이것은 주로 MML 입력 지원을 위해 준비된 기능입니다.  
  (현재로서는 구현 도중인 상태라 사용할 수 없는 기능이 있습니다.)  
  
  ・우선 간단한 사용법  
      
    １. 설정 화면에서 사용할 MIDI 키보드를 선택합니다.  
       
    ２. YM2612(YM3438)의 데이터를 재생하는 중에 (CC:97)을 송신합니다.  
        YM2612(YM3438)의 1Ch 음색이 모든 채널에 세트됩니다.  
       
    ３. 나머지는 연주하기만 하면 됩니다.  
    
  ・주요 기능  
      
    １. 음색 데이터 가져오기  
      각 OPN 계열 음원 또는 OPM 음원, 건반 표시의 음색 데이터 부분을 클릭하면  
      음색 데이터가 선택 채널로 복사됩니다.  
      
    ２. 연주 모드 전환  
      MONO 모드(단일 채널을 사용하여 연주)와  
      POLY 모드(여러 채널(최대 6Ch)을 사용하여 연주)를  
      전환할 수 있습니다.  
      MONO 모드  입력 시에 짧은 프레이즈를 연주하고, MML로 출력하는 것을 상정.  
      POLY 모드  입력 시에 화음을 확인하기 위해 사용하는 것을 상정.  
      
    ３. 채널 노트 로그  
      채널별로 최대 100음의 발음 기록을 남길 수 있습니다.  
      
    ４. 채널 노트 로그 MML 변환 기능  
      로그란을 클릭하면 발음 기록이 MML로 클립보드에 복사됩니다.  
      음 길이는 출력되지 않습니다. 대응 커맨드는 c d e f g a b o < > 입니다.  
      옥타브 정보는 첫 한 음만 o 커맨드로 절대 지정되고,  
      그 후에는 < 커맨드와 > 커맨드에 의한 상대 지정으로 전개됩니다.  
      
    ５. 음색 저장, 불러오기  
      메모리 상에 256종류의 음색을 저장하거나, 또는 불러올 수 있습니다.  
      그 데이터를 지정된 형식으로 파일에 출력하거나, 또는 읽어 들일 수 있습니다.  
      아래 소프트웨어용 형식으로 저장, 불러오기가 가능합니다.  
        FMP7  
        MUCOM88(MUSIC LALF/mucomMD2vgm)  
        NRTDRV  
        MXDRV  
        mml2vgm  
      
    ６. 간이 음색 편집(TBD)  
      입력하고 싶은 파라미터를 선택한 후, 수치를 입력하여 편집할 수 있습니다.  
      
  ・화면  
      
    ０. 건반(TBD)  
      연주 중인 노트가 표시됩니다.  
      
    １．MONO  
      클릭하면 MONO 모드로 전환합니다. 전환되면 ♪ 아이콘이 됩니다.  
      
    ２．POLY  
      클릭하면 POLY 모드로 전환합니다. 전환되면 ♪ 아이콘이 됩니다.  
      
    ３．PANIC  
      모든 채널에 키 오프를 송신합니다. (소리가 계속 울리는 경우에 사용합니다.)  
      
    ４．L.CLS  
      모든 채널의 노트 로그를 지웁니다.  
      
    ５．TP.PUT  
      TonePallet(메모리 상의 음색 보관 영역)에 선택 채널의 음색을 저장합니다.  
      
    ６．TP.GET  
      TonePallet에서 음색을 선택 채널로 불러옵니다.  
      
    ７．T.SAVE  
      TonePallet를 파일로 저장합니다.  
      
    ８．T.LOAD  
      파일에서 TonePallet를 불러옵니다.  
      
    ９．음색 데이터(6Ch분)  
      ・「-」 또는 「♪」를 클릭하여 채널을 선택, 선택 해제할 수 있습니다.  
      ・파라미터를 오른쪽 클릭하여 그 파라미터를 변경할 수 있습니다. (TBD)  
      ・파라미터를 왼쪽 클릭하면 컨텍스트 메뉴가 표시됩니다.  
        복사     : 클릭한 음색을 클립보드에 복사합니다.  
        붙여넣기 : 클립보드의 음색을 클릭한 음색에 붙여넣습니다.  
        위 기능에서 사용되는 텍스트 형식은 FORMAT 란의 소프트웨어 이름을 클릭하여 변경할 수 있습니다.  
        키 조작으로도 복사와 붙여넣기가 가능합니다. 이 경우에는 선택되어 있는 채널이 대상이 됩니다.  
        또한, 붙여넣기 시에 형식의 자동 판별은 이루어지지 않습니다.  
      ・「LOG」 옆의 「♪」를 클릭하면 그 채널의 노트 로그가 지워집니다.  
      ・LOG를 클릭하면 MML 데이터를 클립보드에 설정합니다.  
      
  ・MIDI 건반에서의 조작  
    아래는 기본 설정의 경우입니다. (설정에서 커스터마이즈 가능. 설정값을 공백으로 두면 사용하지 않을 수도 있습니다.)  
      
    CC:97(DATA DEC)  
      YM2612(YM3438)의 1Ch 음색을 모든 채널에 복사합니다. (선택 상황 무시)  
      
    CC:96(DATA INC)  
      직전의 로그를 하나만 삭제합니다. (잘못 친 음 등을 취소하는 기능)  
      
    CC:66(SOSTENUTO)  
      MONO 모드일 때만, 선택 행의 로그를 MML로 변환하여 클립보드에 설정합니다.  
      화면 클릭 시의 처리와의 차이  
        Ctrl+V(붙여넣기) 키 입력을 송신합니다.  
        선택 채널의 노트 로그를 지웁니다.  
        처음의 옥타브 커맨드는 출력하지 않습니다.  
      
      
[SpecialThanks]  
  본 툴은 아래 분들께 신세를 지고 있습니다. 또한 아래의 소프트웨어, 웹 페이지를 참고, 사용하고 있습니다.  
  정말 감사합니다.  
     
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
    ・NDP  
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
    ・C86ctl의 소스  
    ・MGSDRV  
    ・Z80dotNET  
    ・blueMSX  
    ・FMP  
    ・PPZ8  
    ・ZMUSICv2/v3    
    ・RCSMP  
    ・みゅあっぷ  
     
    ・SMS Power!  
    ・DOBON.NET  
    ・Wikipedia  
    ・GitHub  
    ・ぬるり。  
    ・Gigamix Online  
    ・MSX Datapack wiki化計画  
    ・MSX Resource Center  
    ・msxnet  
    ・Xyz님의 트윗의 링크 대상(https://twitter.com/XyzGonGivItToYa/status/1216942514902634496?s=20)  
    ・がんず Work's Diary  
    ・pastraider.com(https://www.pastraiser.com/cpu/gameboy/gameboy_opcodes.html)  
    ・Pan Docs(http://bgb.bircd.org/pandocs.htm#memorymap)  
    ・プラスウイングTV(https://youtu.be/p13EdWrQFjY?si=2L93LDE6SyvINzXX)  


[FAQ]  
  
  ・기동하지 않는다  
  
    Case1  
      존 식별자(Zone Identifier)가 파일에 부가되어 있어서 기동 중에 에러가 발생하기 때문입니다.  
    존 식별자는 OS의 보호 기능 중 하나로, 인터넷에서 다운로드한 파일에 자동으로 부가되어 의도하지 않은 파일의 실행을 막습니다.  
    하지만, 의도적으로 다운로드한 것에 대해서는 이번처럼 방해가 되어 버립니다.  
    →압축을 풀면 생기는 removeZoneIdent.bat을 더블 클릭하여 실행해 주세요.  
    이 배치 파일은 존 식별자를 일괄 삭제합니다.  
    참고로 아래와 같은 메시지가 표시됩니다.  
        알 수 없는 오류가 발생했습니다.  
        Exception Message:  
        Could not load file or assembly  
        'file://.....dll' or one of its dependencies,Operation is not supported.  
        (Exception from HRESULT:xxxx)  

    Case2  
      주로 실제 칩 사용 시에 발생합니다. SCCI2가 c86ctl을 사용하는 상태로 되어 있기 때문입니다.  
    MDPlayer도 c86ctl을 사용하기 때문에 서로 점유하려고 다투게 되어 기동에 실패합니다.  
    →scci2config.exe를 사용하여 c86ctl의 설정 항목인 「enable」의 체크를 해제해 주세요.  
  
    Case3  
      .NETframework의 버전이 다르기 때문입니다.  
    →최신 .NETframework를 설치하면 개선되는 경우가 있습니다.  
    참고로 아래와 같은 메시지가 표시됩니다.  
        알 수 없는 오류가 발생했습니다.  
        Exception Message:  
        Could not load file or assembly  
        'netstandard, Version=..., Culture=..., PublicKeyToken=...' or one of its dependencies.지정된 파일을 찾을 수 없습니다.    

    CaseX  
      TBD  
  
  
  ・템포가 안정되지 않는다, 연주 시작 시에 곡의 처음이 연주되지 않는다, 빨리 감기처럼 된다  
  
    Case1  
      주로 실제 칩 사용 시에 발생합니다. 실제 칩은 연주 시작 시의 처리에 조금 시간이 걸립니다.  
    한편 에뮬레이션은 연주 시작 시의 처리가 바로 완료됩니다.  
    그 시간차를 좁히기 위해 실제 칩이 에뮬레이션을 따라잡으려고 하기 때문입니다.  
    →「옵션」 화면: 「Sound」 탭: 왼쪽 아래의 「日和見～(관망 모드)」 체크박스의 체크를 해제해 주세요.  
  
    CaseX  
      TBD  
  
  
  ・소리가 뚝뚝 끊긴다. 표시가 매우 무겁다  
  
    Case1  
    제한된 시간 안에 필요한 처리를 모두 끝내지 못하는 경우에 발생합니다.  
    「옵션」 화면에서 「Output」 탭을 열고 디바이스를 전환해 주세요.  
    어떤 디바이스가 좋은지는 환경에 따라 다르므로 여러 가지를 시험해 보시기를 권장합니다.  
    Wasapi와 ASIO에서 좋은 응답성을 얻을 수 있는 경우가 많습니다.  
    디바이스에 따라서는 「지연 시간(렌더링 버퍼)」의 수치를 조정하면 개선되는 경우도 있습니다.  
  
    CaseX  
      TBD  
  
  
  ・실제 칩으로 연주했을 때, PCM 소리가 다소 이상한 음원이 있다  

    YM2612(YM3438)나 SSG에 의한 PCM 재생 등은 실제 칩의 경우 정확한 인터럽트 처리를 할 수 없기 때문에 PCM 소리가 이상한 상태가 됩니다.  
  
  
  
[저작권・면책]  
  MDPlayer는 GPLv3 라이선스에 준하는 것으로 합니다. LICENSE.txt를 참조.  
  저작권은 작자가 보유하고 있습니다.  
  이 소프트웨어는 무보증이며, 이 소프트웨어를 사용함으로써 발생한  
  어떠한 손해에 대해서도 작자는 일절 책임을 지지 않습니다.  
  또한, 저작권 표시 및 본 허락 표시는 본 소프트웨어에서는 필요하지 않습니다.  
  그리고 아래 소프트웨어의 소스 코드를 C#용으로 이식 개변하거나, 또는 그대로 사용하고 있습니다.  
  이러한 소스, 소프트웨어는 각 저작자가 저작권을 가집니다. 라이선스에 관해서는 각 문서를 참조해 주세요.  
  
  ・VGMPlay  
  ・MAME  
  ・DOSBOX  
  ・FMGen  
  ・NSFPlay  
  ・NEZ Plug++  
  ・libsidplayfp  
  ・sidplayfp  
  ・NRTDRV  
  ・NDP  
  ・MoonDriver  
  ・MXP  
  ・MXDRV  
  ・MNDRV  
  ・X68Sound  
  (m_puusan님/rururutan님 버전 모두)  
  ・MUCOM88  
  ・MUCOM88windows(mucomDotNET)  
  ・M86(M86DotNET)  
  ・VST.NET  
  ・NAudio  
  ・NAudio.Vorbis 
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
  ・みゅあっぷ  
  
  
  
