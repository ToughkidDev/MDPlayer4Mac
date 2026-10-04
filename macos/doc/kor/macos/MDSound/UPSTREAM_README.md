# MDSound
메가 드라이브 사운드 칩 에뮬레이션 .DLL
  
[개요]  
 이 DLL은 VGM Player의 소스 등을 바탕으로, 메가 드라이브 등에 탑재된 다음 음원 칩의 동작을 C#용 코드로 이식한 것입니다.  
  FM 음원  
    YM2612     OPN2  
	YM3438     OPN2(cmos)  
    YM2151(mame)     OPM  
    YM2151(FMGEN)    OPM  
    YM2151(X68sound) OPM  
    YM2203     OPN  
    YM2608     OPNA  
    YM2610/B   OPNB  
    YM2413     OPLL  
    YMF262     OPL3  
    YMF278B    OPL4  
  PCM 음원  
    RF5C164    RF5C  
    PWM        PWM  
    C140       C140  
    OKIM6258   OKI65  
    MPCM(OKIM6258)   MPCM  
    OKIM6295   OKI69  
    SEGAPCM    SEGAPCM  
    C352       C352  
    K054539    K054  
    NES_DMC  
    PPZ8       PPZ8  
    PPSDRV     PPSDRV  
    PC-9801-86 P86  
  파형 메모리 음원  
    HuC6280(FM적인 부분 있음)  HuC6  
    K051649    K051  
    NES_FDS(FM적인 부분 있음)  
  PSG 음원  
    SN76489  
    AY8910  
    NES_APU  
  기타(가상 음원)  
    YM2609   OPNA2  
    AY8910-2 PSG2  
  
[기능, 특징]  
 ・비교적 .NET 문화에 맞는 방식으로 작성할 수 있습니다.  
 ・매니지드 프로그램입니다. (test 프로그램은 SDLNET을 사용하고 있지만요.)  
  
[저작권・면책]  
  MDSound는 프리 소프트웨어입니다. 저작권은 작자가 보유하고 있습니다.  
  이 소프트웨어는 무보증이며, 이 소프트웨어를 사용함으로써 발생한  
  어떠한 손해에 대해서도 작자는 일절 책임을 지지 않습니다.  
  라이선스에 관해서는 GPLv3 라이선스에 준하는 것으로 합니다.  
  
  MDSound는 다음 소프트웨어의 소스 코드를 C#용으로 이식하여 사용하고 있습니다.  
  이들 소스는 각 저작자가 저작권을 가집니다.  
  라이선스에 관해서는 각 문서를 참조해 주세요.  
  
 ・VGMPlay  
 ・MAME  
 ・Gens  
 ・Ootake  
 ・fmgen  
 ・NSFPlay  
 ・X68Sound.dll  
 ・TinyMPCM(가칭)  
 ・Nuked-OPN2  
 ・PMDWin  
 ・うつぼかずら 님이 만든 필터, 이펙터  
  
[SpecialThanks]  
 본 도구는 다음 분들의 도움을 받고 있습니다. 또한 다음 소프트웨어와 웹 페이지를 참고, 사용하고 있습니다.  
 정말 감사합니다.  
  
 ・Visual Studio Community 2015  
 ・SGDK  
 ・VGM Player  
 ・Nuked-OPN2  
 ・Git  
 ・SDL/SDLNET  
 ・SourceTree  
 ・사쿠라 에디터(さくらエディター)  
 ・QUASI88의 문서  
  
 ・SMS Power!  
 ・DOBON.NET  
 ・C++でVST作り(C++로 VST 만들기)  
 ・Wikipedia  
