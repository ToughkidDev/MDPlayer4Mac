using MDPlayer.Driver.ZMS.nise68;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayer.Driver.GBS
{
    public partial class CPU
    {
        private Inst[] insts;

        //参考:
        //https://www.pastraiser.com/cpu/gameboy/gameboy_opcodes.html

        private void initInsts()
        {
            insts = [
    // 0x00
    new ( NOP            , 1 , "4    " , "----" ) , new ( LD_BC_d16      , 3 , "12   " , "----" ) ,
new ( LD_pBCs_A      , 1 , "8    " , "----" ) , new ( INC_BC         , 1 , "8    " , "----" ) ,
new ( INC_B          , 1 , "4    " , "Z0H-" ) , new ( DEC_B          , 1 , "4    " , "Z1H-" ) ,
new ( LD_B_d8        , 2 , "8    " , "----" ) , new ( RLCA           , 1 , "4    " , "000C" ) ,
// 0x08
new ( LD_pa16s_SP    , 3 , "20   " , "----" ) , new ( ADD_HL_BC      , 1 , "8    " , "-0HC" ) ,
new ( LD_A_pBCs      , 1 , "8    " , "----" ) , new ( DEC_BC         , 1 , "8    " , "----" ) ,
new ( INC_C          , 1 , "4    " , "Z0H-" ) , new ( DEC_C          , 1 , "4    " , "Z1H-" ) ,
new ( LD_C_d8        , 2 , "8    " , "----" ) , new ( RRCA           , 1 , "4    " , "000C" ) ,
// 0x10
new ( STOP_0         , 2 , "4    " , "----" ) , new ( LD_DE_d16      , 3 , "12   " , "----" ) ,
new ( LD_pDEs_A      , 1 , "8    " , "----" ) , new ( INC_DE         , 1 , "8    " , "----" ) ,
new ( INC_D          , 1 , "4    " , "Z0H-" ) , new ( DEC_D          , 1 , "4    " , "Z1H-" ) ,
new ( LD_D_d8        , 2 , "8    " , "----" ) , new ( RLA            , 1 , "4    " , "000C" ) ,
// 0x18
new ( JR_r8          , 2 , "12   " , "----" ) , new ( ADD_HL_DE      , 1 , "8    " , "-0HC" ) ,
new ( LD_A_pDEs      , 1 , "8    " , "----" ) , new ( DEC_DE         , 1 , "8    " , "----" ) ,
new ( INC_E          , 1 , "4    " , "Z0H-" ) , new ( DEC_E          , 1 , "4    " , "Z1H-" ) ,
new ( LD_E_d8        , 2 , "8    " , "----" ) , new ( RRA            , 1 , "4    " , "000C" ) ,
// 0x20
new ( JR_NZ_r8       , 2 , "12/8 " , "----" ) , new ( LD_HL_d16      , 3 , "12   " , "----" ) ,
new ( LD_pHLplss_A   , 1 , "8    " , "----" ) , new ( INC_HL         , 1 , "8    " , "----" ) ,
new ( INC_H          , 1 , "4    " , "Z0H-" ) , new ( DEC_H          , 1 , "4    " , "Z1H-" ) ,
new ( LD_H_d8        , 2 , "8    " , "----" ) , new ( DAA            , 1 , "4    " , "Z-0C" ) ,
// 0x28
new ( JR_Z_r8        , 2 , "12/8 " , "----" ) , new ( ADD_HL_HL      , 1 , "8    " , "-0HC" ) ,
new ( LD_A_pHLplss   , 1 , "8    " , "----" ) , new ( DEC_HL         , 1 , "8    " , "----" ) ,
new ( INC_L          , 1 , "4    " , "Z0H-" ) , new ( DEC_L          , 1 , "4    " , "Z1H-" ) ,
new ( LD_L_d8        , 2 , "8    " , "----" ) , new ( CPL            , 1 , "4    " , "-11-" ) ,
// 0x30
new ( JR_NC_r8       , 2 , "12/8 " , "----" ) , new ( LD_SP_d16      , 3 , "12   " , "----" ) ,
new ( LD_pHLmiss_A   , 1 , "8    " , "----" ) , new ( INC_SP         , 1 , "8    " , "----" ) ,
new ( INC_pHLs       , 1 , "12   " , "Z0H-" ) , new ( DEC_pHLs       , 1 , "12   " , "Z1H-" ) ,
new ( LD_pHLs_d8     , 2 , "12   " , "----" ) , new ( SCF            , 1 , "4    " , "-001" ) ,
// 0x38
new ( JR_C_r8        , 2 , "12/8 " , "----" ) , new ( ADD_HL_SP      , 1 , "8    " , "-0HC" ) ,
                new ( LD_A_pHLmiss   , 1 , "8    " , "----" ) , new ( DEC_SP         , 1 , "8    " , "----" ) ,
new ( INC_A          , 1 , "4    " , "Z0H-" ) , new ( DEC_A          , 1 , "4    " , "Z1H-" ) ,
new ( LD_A_d8        , 2 , "8    " , "----" ) , new ( CCF            , 1 , "4    " , "-00C" ) ,
// 0x40
new ( LD_B_B         , 1 , "4    " , "----" ) , new ( LD_B_C         , 1 , "4    " , "----" ) ,
new ( LD_B_D         , 1 , "4    " , "----" ) , new ( LD_B_E         , 1 , "4    " , "----" ) ,
new ( LD_B_H         , 1 , "4    " , "----" ) , new ( LD_B_L         , 1 , "4    " , "----" ) ,
new ( LD_B_pHLs      , 1 , "8    " , "----" ) , new ( LD_B_A         , 1 , "4    " , "----" ) ,
// 0x48
new ( LD_C_B         , 1 , "4    " , "----" ) , new ( LD_C_C         , 1 , "4    " , "----" ) ,
new ( LD_C_D         , 1 , "4    " , "----" ) , new ( LD_C_E         , 1 , "4    " , "----" ) ,
new ( LD_C_H         , 1 , "4    " , "----" ) , new ( LD_C_L         , 1 , "4    " , "----" ) ,
new ( LD_C_pHLs      , 1 , "8    " , "----" ) , new ( LD_C_A         , 1 , "4    " , "----" ) ,
// 0x50
new ( LD_D_B         , 1 , "4    " , "----" ) , new ( LD_D_C         , 1 , "4    " , "----" ) ,
new ( LD_D_D         , 1 , "4    " , "----" ) , new ( LD_D_E         , 1 , "4    " , "----" ) ,
new ( LD_D_H         , 1 , "4    " , "----" ) , new ( LD_D_L         , 1 , "4    " , "----" ) ,
new ( LD_D_pHLs      , 1 , "8    " , "----" ) , new ( LD_D_A         , 1 , "4    " , "----" ) ,
// 0x58
new ( LD_E_B         , 1 , "4    " , "----" ) , new ( LD_E_C         , 1 , "4    " , "----" ) ,
new ( LD_E_D         , 1 , "4    " , "----" ) , new ( LD_E_E         , 1 , "4    " , "----" ) ,
new ( LD_E_H         , 1 , "4    " , "----" ) , new ( LD_E_L         , 1 , "4    " , "----" ) ,
new ( LD_E_pHLs      , 1 , "8    " , "----" ) , new ( LD_E_A         , 1 , "4    " , "----" ) ,
// 0x60
new ( LD_H_B         , 1 , "4    " , "----" ) , new ( LD_H_C         , 1 , "4    " , "----" ) ,
new ( LD_H_D         , 1 , "4    " , "----" ) , new ( LD_H_E         , 1 , "4    " , "----" ) ,
new ( LD_H_H         , 1 , "4    " , "----" ) , new ( LD_H_L         , 1 , "4    " , "----" ) ,
new ( LD_H_pHLs      , 1 , "8    " , "----" ) , new ( LD_H_A         , 1 , "4    " , "----" ) ,
// 0x68
new ( LD_L_B         , 1 , "4    " , "----" ) , new ( LD_L_C         , 1 , "4    " , "----" ) ,
new ( LD_L_D         , 1 , "4    " , "----" ) , new ( LD_L_E         , 1 , "4    " , "----" ) ,
new ( LD_L_H         , 1 , "4    " , "----" ) , new ( LD_L_L         , 1 , "4    " , "----" ) ,
new ( LD_L_pHLs      , 1 , "8    " , "----" ) , new ( LD_L_A         , 1 , "4    " , "----" ) ,
// 0x70
new ( LD_pHLs_B      , 1 , "8    " , "----" ) , new ( LD_pHLs_C      , 1 , "8    " , "----" ) ,
new ( LD_pHLs_D      , 1 , "8    " , "----" ) , new ( LD_pHLs_E      , 1 , "8    " , "----" ) ,
new ( LD_pHLs_H      , 1 , "8    " , "----" ) , new ( LD_pHLs_L      , 1 , "8    " , "----" ) ,
new ( HALT           , 1 , "4    " , "----" ) , new ( LD_pHLs_A      , 1 , "8    " , "----" ) ,
// 0x78
new ( LD_A_B         , 1 , "4    " , "----" ) , new ( LD_A_C         , 1 , "4    " , "----" ) ,
new ( LD_A_D         , 1 , "4    " , "----" ) , new ( LD_A_E         , 1 , "4    " , "----" ) ,
new ( LD_A_H         , 1 , "4    " , "----" ) , new ( LD_A_L         , 1 , "4    " , "----" ) ,
new ( LD_A_pHLs      , 1 , "8    " , "----" ) , new ( LD_A_A         , 1 , "4    " , "----" ) ,
// 0x80
new ( ADD_A_B        , 1 , "4    " , "Z0HC" ) , new ( ADD_A_C        , 1 , "4    " , "Z0HC" ) ,
new ( ADD_A_D        , 1 , "4    " , "Z0HC" ) , new ( ADD_A_E        , 1 , "4    " , "Z0HC" ) ,
new ( ADD_A_H        , 1 , "4    " , "Z0HC" ) , new ( ADD_A_L        , 1 , "4    " , "Z0HC" ) ,
new ( ADD_A_pHLs     , 1 , "8    " , "Z0HC" ) , new ( ADD_A_A        , 1 , "4    " , "Z0HC" ) ,
// 0x88
new ( ADC_A_B        , 1 , "4    " , "Z0HC" ) , new ( ADC_A_C        , 1 , "4    " , "Z0HC" ) ,
new ( ADC_A_D        , 1 , "4    " , "Z0HC" ) , new ( ADC_A_E        , 1 , "4    " , "Z0HC" ) ,
new ( ADC_A_H        , 1 , "4    " , "Z0HC" ) , new ( ADC_A_L        , 1 , "4    " , "Z0HC" ) ,
new ( ADC_A_pHLs     , 1 , "8    " , "Z0HC" ) , new ( ADC_A_A        , 1 , "4    " , "Z0HC" ) ,
// 0x90
new ( SUB_B          , 1 , "4    " , "Z1HC" ) , new ( SUB_C          , 1 , "4    " , "Z1HC" ) ,
new ( SUB_D          , 1 , "4    " , "Z1HC" ) , new ( SUB_E          , 1 , "4    " , "Z1HC" ) ,
new ( SUB_H          , 1 , "4    " , "Z1HC" ) , new ( SUB_L          , 1 , "4    " , "Z1HC" ) ,
new ( SUB_pHLs       , 1 , "8    " , "Z1HC" ) , new ( SUB_A          , 1 , "4    " , "Z1HC" ) ,
// 0x98
new ( SBC_A_B        , 1 , "4    " , "Z1HC" ) , new ( SBC_A_C        , 1 , "4    " , "Z1HC" ) ,
new ( SBC_A_D        , 1 , "4    " , "Z1HC" ) , new ( SBC_A_E        , 1 , "4    " , "Z1HC" ) ,
new ( SBC_A_H        , 1 , "4    " , "Z1HC" ) , new ( SBC_A_L        , 1 , "4    " , "Z1HC" ) ,
new ( SBC_A_pHLs     , 1 , "8    " , "Z1HC" ) , new ( SBC_A_A        , 1 , "4    " , "Z1HC" ) ,
                // 0xa0
new ( AND_B          , 1 , "4    " , "Z010" ) , new ( AND_C          , 1 , "4    " , "Z010" ) ,
new ( AND_D          , 1 , "4    " , "Z010" ) , new ( AND_E          , 1 , "4    " , "Z010" ) ,
new ( AND_H          , 1 , "4    " , "Z010" ) , new ( AND_L          , 1 , "4    " , "Z010" ) ,
new ( AND_pHLs       , 1 , "8    " , "Z010" ) , new ( AND_A          , 1 , "4    " , "Z010" ) ,
// 0xa8
new ( XOR_B          , 1 , "4    " , "Z000" ) , new ( XOR_C          , 1 , "4    " , "Z000" ) ,
new ( XOR_D          , 1 , "4    " , "Z000" ) , new ( XOR_E          , 1 , "4    " , "Z000" ) ,
new ( XOR_H          , 1 , "4    " , "Z000" ) , new ( XOR_L          , 1 , "4    " , "Z000" ) ,
new ( XOR_pHLs       , 1 , "8    " , "Z000" ) , new ( XOR_A          , 1 , "4    " , "Z000" ) ,
// 0xb0
new ( OR_B           , 1 , "4    " , "Z000" ) , new ( OR_C           , 1 , "4    " , "Z000" ) ,
new ( OR_D           , 1 , "4    " , "Z000" ) , new ( OR_E           , 1 , "4    " , "Z000" ) ,
new ( OR_H           , 1 , "4    " , "Z000" ) , new ( OR_L           , 1 , "4    " , "Z000" ) ,
new ( OR_pHLs        , 1 , "8    " , "Z000" ) , new ( OR_A           , 1 , "4    " , "Z000" ) ,
// 0xb8
new ( CP_B           , 1 , "4    " , "Z1HC" ) , new ( CP_C           , 1 , "4    " , "Z1HC" ) ,
new ( CP_D           , 1 , "4    " , "Z1HC" ) , new ( CP_E           , 1 , "4    " , "Z1HC" ) ,
new ( CP_H           , 1 , "4    " , "Z1HC" ) , new ( CP_L           , 1 , "4    " , "Z1HC" ) ,
new ( CP_pHLs        , 1 , "8    " , "Z1HC" ) , new ( CP_A           , 1 , "4    " , "Z1HC" ) ,
// 0xc0
new ( RET_NZ         , 1 , "20/8 " , "----" ) , new ( POP_BC         , 1 , "12   " , "----" ) ,
new ( JP_NZ_a16      , 3 , "16/12" , "----" ) , new ( JP_a16         , 3 , "16   " , "----" ) ,
new ( CALL_NZ_a16    , 3 , "24/12" , "----" ) , new ( PUSH_BC        , 1 , "16   " , "----" ) ,
new ( ADD_A_d8       , 2 , "8    " , "Z0HC" ) , new ( RST_00H        , 1 , "16   " , "----" ) ,
// 0xc8
new ( RET_Z          , 1 , "20/8 " , "----" ) , new ( RET            , 1 , "16   " , "----" ) ,
new ( JP_Z_a16       , 3 , "16/12" , "----" ) , new ( PREFIX_CB      , 1 , "4    " , "----" ) ,
new ( CALL_Z_a16     , 3 , "24/12" , "----" ) , new ( CALL_a16       , 3 , "24   " , "----" ) ,
new ( ADC_A_d8       , 2 , "8    " , "Z0HC" ) , new ( RST_08H        , 1 , "16   " , "----" ) ,
// 0xd0
new ( RET_NC         , 1 , "20/8 " , "----" ) , new ( POP_DE         , 1 , "12   " , "----" ) ,
new ( JP_NC_a16      , 3 , "16/12" , "----" ) , new ( null           , 0 , "     " , "    " ) ,
new ( CALL_NC_a16    , 3 , "24/12" , "----" ) , new ( PUSH_DE        , 1 , "16   " , "----" ) ,
new ( SUB_d8         , 2 , "8    " , "Z1HC" ) , new ( RST_10H        , 1 , "16   " , "----" ) ,
// 0xd8
new ( RET_C          , 1 , "20/8 " , "----" ) , new ( RETI           , 1 , "16   " , "----" ) ,
new ( JP_C_a16       , 3 , "16/12" , "----" ) , new ( null           , 0 , "     " , "    " ) ,
new ( CALL_C_a16     , 3 , "24/12" , "----" ) , new ( null           , 0 , "     " , "    " ) ,
new ( SBC_A_d8       , 2 , "8    " , "Z1HC" ) , new ( RST_18H        , 1 , "16   " , "----" ) ,
// 0xe0
new ( LDH_pa8s_A     , 2 , "12   " , "----" ) , new ( POP_HL         , 1 , "12   " , "----" ) ,
new ( LD_pCs_A       , 1 , "8    " , "----" ) , new ( null           , 0 , "     " , "    " ) ,
new ( null           , 0 , "     " , "    " ) , new ( PUSH_HL        , 1 , "16   " , "----" ) ,
new ( AND_d8         , 2 , "8    " , "Z010" ) , new ( RST_20H        , 1 , "16   " , "----" ) ,
// 0xe8
new ( ADD_SP_r8      , 2 , "16   " , "00HC" ) , new ( JP_pHLs        , 1 , "4    " , "----" ) ,
new ( LD_pa16s_A     , 3 , "16   " , "----" ) , new ( null           , 0 , "     " , "    " ) ,
new ( null           , 0 , "     " , "    " ) , new ( null           , 0 , "     " , "    " ) ,
new ( XOR_d8         , 2 , "8    " , "Z000" ) , new ( RST_28H        , 1 , "16   " , "----" ) ,
// 0xf0
new ( LDH_A_pa8s     , 2 , "12   " , "----" ) , new ( POP_AF         , 1 , "12   " , "ZNHC" ) ,
new ( LD_A_pCs       , 1 , "8    " , "----" ) , new ( DI             , 1 , "4    " , "----" ) ,
new ( null           , 0 , "     " , "    " ) , new ( PUSH_AF        , 1 , "16   " , "----" ) ,
new ( OR_d8          , 2 , "8    " , "Z000" ) , new ( RST_30H        , 1 , "16   " , "----" ) ,
// 0xf8
new ( LD_HL_SPplsr8  , 2 , "12   " , "00HC" ) , new ( LD_SP_HL       , 1 , "8    " , "----" ) ,
new ( LD_A_pa16s     , 3 , "16   " , "----" ) , new ( EI             , 1 , "4    " , "----" ) ,
new ( null           , 0 , "     " , "    " ) , new ( null           , 0 , "     " , "    " ) ,
new ( CP_d8          , 2 , "8    " , "Z1HC" ) , new ( RST_38H        , 1 , "16   " , "----" ) ,
// 0x00 (CB)
new ( RLC_B          , 1 , "4    " , "Z00C" ) , new ( RLC_C          , 1 , "4    " , "Z00C" ) ,
new ( RLC_D          , 1 , "4    " , "Z00C" ) , new ( RLC_E          , 1 , "4    " , "Z00C" ) ,
new ( RLC_H          , 1 , "4    " , "Z00C" ) , new ( RLC_L          , 1 , "4    " , "Z00C" ) ,
new ( RLC_pHLs       , 1 , "12   " , "Z00C" ) , new ( RLC_A          , 1 , "4    " , "Z00C" ) ,
// 0x08 (CB)
new ( RRC_B          , 1 , "4    " , "Z00C" ) , new ( RRC_C          , 1 , "4    " , "Z00C" ) ,
new ( RRC_D          , 1 , "4    " , "Z00C" ) , new ( RRC_E          , 1 , "4    " , "Z00C" ) ,
new ( RRC_H          , 1 , "4    " , "Z00C" ) , new ( RRC_L          , 1 , "4    " , "Z00C" ) ,
new ( RRC_pHLs       , 1 , "12   " , "Z00C" ) , new ( RRC_A          , 1 , "4    " , "Z00C" ) ,
// 0x10 (CB)
new ( RL_B           , 1 , "4    " , "Z00C" ) , new ( RL_C           , 1 , "4    " , "Z00C" ) ,
new ( RL_D           , 1 , "4    " , "Z00C" ) , new ( RL_E           , 1 , "4    " , "Z00C" ) ,
new ( RL_H           , 1 , "4    " , "Z00C" ) , new ( RL_L           , 1 , "4    " , "Z00C" ) ,
new ( RL_pHLs        , 1 , "12   " , "Z00C" ) , new ( RL_A           , 1 , "4    " , "Z00C" ) ,
// 0x18 (CB)
new ( RR_B           , 1 , "4    " , "Z00C" ) , new ( RR_C           , 1 , "4    " , "Z00C" ) ,
new ( RR_D           , 1 , "4    " , "Z00C" ) , new ( RR_E           , 1 , "4    " , "Z00C" ) ,
new ( RR_H           , 1 , "4    " , "Z00C" ) , new ( RR_L           , 1 , "4    " , "Z00C" ) ,
new ( RR_pHLs        , 1 , "12   " , "Z00C" ) , new ( RR_A           , 1 , "4    " , "Z00C" ) ,
// 0x20 (CB)
new ( SLA_B          , 1 , "4    " , "Z00C" ) , new ( SLA_C          , 1 , "4    " , "Z00C" ) ,
new ( SLA_D          , 1 , "4    " , "Z00C" ) , new ( SLA_E          , 1 , "4    " , "Z00C" ) ,
new ( SLA_H          , 1 , "4    " , "Z00C" ) , new ( SLA_L          , 1 , "4    " , "Z00C" ) ,
new ( SLA_pHLs       , 1 , "12   " , "Z00C" ) , new ( SLA_A          , 1 , "4    " , "Z00C" ) ,
// 0x28 (CB) 
new ( SRA_B          , 1 , "4    " , "Z000" ) , new ( SRA_C          , 1 , "4    " , "Z000" ) ,
new ( SRA_D          , 1 , "4    " , "Z000" ) , new ( SRA_E          , 1 , "4    " , "Z000" ) ,
new ( SRA_H          , 1 , "4    " , "Z000" ) , new ( SRA_L          , 1 , "4    " , "Z000" ) ,
new ( SRA_pHLs       , 1 , "12   " , "Z000" ) , new ( SRA_A          , 1 , "4    " , "Z000" ) ,
// 0x30 (CB)
new ( SWAP_B         , 1 , "4    " , "Z000" ) , new ( SWAP_C         , 1 , "4    " , "Z000" ) ,
new ( SWAP_D         , 1 , "4    " , "Z000" ) , new ( SWAP_E         , 1 , "4    " , "Z000" ) ,
new ( SWAP_H         , 1 , "4    " , "Z000" ) , new ( SWAP_L         , 1 , "4    " , "Z000" ) ,
new ( SWAP_pHLs      , 1 , "12   " , "Z000" ) , new ( SWAP_A         , 1 , "4    " , "Z000" ) ,
// 0x38 (CB)
new ( SRL_B          , 1 , "4    " , "Z00C" ) , new ( SRL_C          , 1 , "4    " , "Z00C" ) ,
new ( SRL_D          , 1 , "4    " , "Z00C" ) , new ( SRL_E          , 1 , "4    " , "Z00C" ) ,
new ( SRL_H          , 1 , "4    " , "Z00C" ) , new ( SRL_L          , 1 , "4    " , "Z00C" ) ,
new ( SRL_pHLs       , 1 , "12   " , "Z00C" ) , new ( SRL_A          , 1 , "4    " , "Z00C" ) ,
// 0x40 (CB)
new ( BIT_0_B        , 1 , "4    " , "Z01-" ) , new ( BIT_0_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_0_D        , 1 , "4    " , "Z01-" ) , new ( BIT_0_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_0_H        , 1 , "4    " , "Z01-" ) , new ( BIT_0_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_0_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_0_A        , 1 , "4    " , "Z01-" ) ,
// 0x48 (CB)
new ( BIT_1_B        , 1 , "4    " , "Z01-" ) , new ( BIT_1_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_1_D        , 1 , "4    " , "Z01-" ) , new ( BIT_1_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_1_H        , 1 , "4    " , "Z01-" ) , new ( BIT_1_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_1_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_1_A        , 1 , "4    " , "Z01-" ) ,
// 0x50 (CB)
new ( BIT_2_B        , 1 , "4    " , "Z01-" ) , new ( BIT_2_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_2_D        , 1 , "4    " , "Z01-" ) , new ( BIT_2_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_2_H        , 1 , "4    " , "Z01-" ) , new ( BIT_2_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_2_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_2_A        , 1 , "4    " , "Z01-" ) ,
// 0x58 (CB)
new ( BIT_3_B        , 1 , "4    " , "Z01-" ) , new ( BIT_3_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_3_D        , 1 , "4    " , "Z01-" ) , new ( BIT_3_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_3_H        , 1 , "4    " , "Z01-" ) , new ( BIT_3_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_3_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_3_A        , 1 , "4    " , "Z01-" ) ,
// 0x60 (CB)
new ( BIT_4_B        , 1 , "4    " , "Z01-" ) , new ( BIT_4_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_4_D        , 1 , "4    " , "Z01-" ) , new ( BIT_4_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_4_H        , 1 , "4    " , "Z01-" ) , new ( BIT_4_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_4_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_4_A        , 1 , "4    " , "Z01-" ) ,
// 0x68 (CB)
new ( BIT_5_B        , 1 , "4    " , "Z01-" ) , new ( BIT_5_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_5_D        , 1 , "4    " , "Z01-" ) , new ( BIT_5_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_5_H        , 1 , "4    " , "Z01-" ) , new ( BIT_5_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_5_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_5_A        , 1 , "4    " , "Z01-" ) ,
// 0x70 (CB)
new ( BIT_6_B        , 1 , "4    " , "Z01-" ) , new ( BIT_6_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_6_D        , 1 , "4    " , "Z01-" ) , new ( BIT_6_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_6_H        , 1 , "4    " , "Z01-" ) , new ( BIT_6_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_6_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_6_A        , 1 , "4    " , "Z01-" ) ,
// 0x78 (CB)
new ( BIT_7_B        , 1 , "4    " , "Z01-" ) , new ( BIT_7_C        , 1 , "4    " , "Z01-" ) ,
new ( BIT_7_D        , 1 , "4    " , "Z01-" ) , new ( BIT_7_E        , 1 , "4    " , "Z01-" ) ,
new ( BIT_7_H        , 1 , "4    " , "Z01-" ) , new ( BIT_7_L        , 1 , "4    " , "Z01-" ) ,
new ( BIT_7_pHLs     , 1 , "12   " , "Z01-" ) , new ( BIT_7_A        , 1 , "4    " , "Z01-" ) ,
// 0x80 (CB)
new ( RES_0_B        , 1 , "4    " , "----" ) , new ( RES_0_C        , 1 , "4    " , "----" ) ,
new ( RES_0_D        , 1 , "4    " , "----" ) , new ( RES_0_E        , 1 , "4    " , "----" ) ,
new ( RES_0_H        , 1 , "4    " , "----" ) , new ( RES_0_L        , 1 , "4    " , "----" ) ,
new ( RES_0_pHLs     , 1 , "12   " , "----" ) , new ( RES_0_A        , 1 , "4    " , "----" ) ,
// 0x88 (CB)
new ( RES_1_B        , 1 , "4    " , "----" ) , new ( RES_1_C        , 1 , "4    " , "----" ) ,
new ( RES_1_D        , 1 , "4    " , "----" ) , new ( RES_1_E        , 1 , "4    " , "----" ) ,
new ( RES_1_H        , 1 , "4    " , "----" ) , new ( RES_1_L        , 1 , "4    " , "----" ) ,
new ( RES_1_pHLs     , 1 , "12   " , "----" ) , new ( RES_1_A        , 1 , "4    " , "----" ) ,
// 0x90 (CB)
new ( RES_2_B        , 1 , "4    " , "----" ) , new ( RES_2_C        , 1 , "4    " , "----" ) ,
new ( RES_2_D        , 1 , "4    " , "----" ) , new ( RES_2_E        , 1 , "4    " , "----" ) ,
new ( RES_2_H        , 1 , "4    " , "----" ) , new ( RES_2_L        , 1 , "4    " , "----" ) ,
new ( RES_2_pHLs     , 1 , "12   " , "----" ) , new ( RES_2_A        , 1 , "4    " , "----" ) ,
// 0x98 (CB)
new ( RES_3_B        , 1 , "4    " , "----" ) , new ( RES_3_C        , 1 , "4    " , "----" ) ,
new ( RES_3_D        , 1 , "4    " , "----" ) , new ( RES_3_E        , 1 , "4    " , "----" ) ,
new ( RES_3_H        , 1 , "4    " , "----" ) , new ( RES_3_L        , 1 , "4    " , "----" ) ,
new ( RES_3_pHLs     , 1 , "12   " , "----" ) , new ( RES_3_A        , 1 , "4    " , "----" ) ,
// 0xa0 (CB)
new ( RES_4_B        , 1 , "4    " , "----" ) , new ( RES_4_C        , 1 , "4    " , "----" ) ,
new ( RES_4_D        , 1 , "4    " , "----" ) , new ( RES_4_E        , 1 , "4    " , "----" ) ,
new ( RES_4_H        , 1 , "4    " , "----" ) , new ( RES_4_L        , 1 , "4    " , "----" ) ,
new ( RES_4_pHLs     , 1 , "12   " , "----" ) , new ( RES_4_A        , 1 , "4    " , "----" ) ,
// 0xa8 (CB)
new ( RES_5_B        , 1 , "4    " , "----" ) , new ( RES_5_C        , 1 , "4    " , "----" ) ,
new ( RES_5_D        , 1 , "4    " , "----" ) , new ( RES_5_E        , 1 , "4    " , "----" ) ,
new ( RES_5_H        , 1 , "4    " , "----" ) , new ( RES_5_L        , 1 , "4    " , "----" ) ,
new ( RES_5_pHLs     , 1 , "12   " , "----" ) , new ( RES_5_A        , 1 , "4    " , "----" ) ,
// 0xb0 (CB)
new ( RES_6_B        , 1 , "4    " , "----" ) , new ( RES_6_C        , 1 , "4    " , "----" ) ,
new ( RES_6_D        , 1 , "4    " , "----" ) , new ( RES_6_E        , 1 , "4    " , "----" ) ,
new ( RES_6_H        , 1 , "4    " , "----" ) , new ( RES_6_L        , 1 , "4    " , "----" ) ,
new ( RES_6_pHLs     , 1 , "12   " , "----" ) , new ( RES_6_A        , 1 , "4    " , "----" ) ,
// 0xb8 (CB)
new ( RES_7_B        , 1 , "4    " , "----" ) , new ( RES_7_C        , 1 , "4    " , "----" ) ,
new ( RES_7_D        , 1 , "4    " , "----" ) , new ( RES_7_E        , 1 , "4    " , "----" ) ,
new ( RES_7_H        , 1 , "4    " , "----" ) , new ( RES_7_L        , 1 , "4    " , "----" ) ,
new ( RES_7_pHLs     , 1 , "12   " , "----" ) , new ( RES_7_A        , 1 , "4    " , "----" ) ,
// 0xc0 (CB)
new ( SET_0_B        , 1 , "4    " , "----" ) , new ( SET_0_C        , 1 , "4    " , "----" ) ,
new ( SET_0_D        , 1 , "4    " , "----" ) , new ( SET_0_E        , 1 , "4    " , "----" ) ,
new ( SET_0_H        , 1 , "4    " , "----" ) , new ( SET_0_L        , 1 , "4    " , "----" ) ,
new ( SET_0_pHLs     , 1 , "12   " , "----" ) , new ( SET_0_A        , 1 , "4    " , "----" ) ,
// 0xc8 (CB)
new ( SET_1_B        , 1 , "4    " , "----" ) , new ( SET_1_C        , 1 , "4    " , "----" ) ,
new ( SET_1_D        , 1 , "4    " , "----" ) , new ( SET_1_E        , 1 , "4    " , "----" ) ,
new ( SET_1_H        , 1 , "4    " , "----" ) , new ( SET_1_L        , 1 , "4    " , "----" ) ,
new ( SET_1_pHLs     , 1 , "12   " , "----" ) , new ( SET_1_A        , 1 , "4    " , "----" ) ,
// 0xd0 (CB)
new ( SET_2_B        , 1 , "4    " , "----" ) , new ( SET_2_C        , 1 , "4    " , "----" ) ,
new ( SET_2_D        , 1 , "4    " , "----" ) , new ( SET_2_E        , 1 , "4    " , "----" ) ,
new ( SET_2_H        , 1 , "4    " , "----" ) , new ( SET_2_L        , 1 , "4    " , "----" ) ,
new ( SET_2_pHLs     , 1 , "12   " , "----" ) , new ( SET_2_A        , 1 , "4    " , "----" ) ,
// 0xd8 (CB)
new ( SET_3_B        , 1 , "4    " , "----" ) , new ( SET_3_C        , 1 , "4    " , "----" ) ,
new ( SET_3_D        , 1 , "4    " , "----" ) , new ( SET_3_E        , 1 , "4    " , "----" ) ,
new ( SET_3_H        , 1 , "4    " , "----" ) , new ( SET_3_L        , 1 , "4    " , "----" ) ,
new ( SET_3_pHLs     , 1 , "12   " , "----" ) , new ( SET_3_A        , 1 , "4    " , "----" ) ,
// 0xe0 (CB)
new ( SET_4_B        , 1 , "4    " , "----" ) , new ( SET_4_C        , 1 , "4    " , "----" ) ,
new ( SET_4_D        , 1 , "4    " , "----" ) , new ( SET_4_E        , 1 , "4    " , "----" ) ,
new ( SET_4_H        , 1 , "4    " , "----" ) , new ( SET_4_L        , 1 , "4    " , "----" ) ,
new ( SET_4_pHLs     , 1 , "12   " , "----" ) , new ( SET_4_A        , 1 , "4    " , "----" ) ,
// 0xe8 (CB)
new ( SET_5_B        , 1 , "4    " , "----" ) , new ( SET_5_C        , 1 , "4    " , "----" ) ,
new ( SET_5_D        , 1 , "4    " , "----" ) , new ( SET_5_E        , 1 , "4    " , "----" ) ,
new ( SET_5_H        , 1 , "4    " , "----" ) , new ( SET_5_L        , 1 , "4    " , "----" ) ,
new ( SET_5_pHLs     , 1 , "12   " , "----" ) , new ( SET_5_A        , 1 , "4    " , "----" ) ,
// 0xf0 (CB)
new ( SET_6_B        , 1 , "4    " , "----" ) , new ( SET_6_C        , 1 , "4    " , "----" ) ,
new ( SET_6_D        , 1 , "4    " , "----" ) , new ( SET_6_E        , 1 , "4    " , "----" ) ,
new ( SET_6_H        , 1 , "4    " , "----" ) , new ( SET_6_L        , 1 , "4    " , "----" ) ,
new ( SET_6_pHLs     , 1 , "12   " , "----" ) , new ( SET_6_A        , 1 , "4    " , "----" ) ,
// 0xf8 (CB)
new ( SET_7_B        , 1 , "4    " , "----" ) , new ( SET_7_C        , 1 , "4    " , "----" ) ,
new ( SET_7_D        , 1 , "4    " , "----" ) , new ( SET_7_E        , 1 , "4    " , "----" ) ,
new ( SET_7_H        , 1 , "4    " , "----" ) , new ( SET_7_L        , 1 , "4    " , "----" ) ,
new ( SET_7_pHLs     , 1 , "12   " , "----" ) , new ( SET_7_A        , 1 , "4    " , "----" ) ,
];
        }
    }
}
