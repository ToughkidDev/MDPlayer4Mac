using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MDPlayer.Driver.ZMS.nise68
{
    public static class cy
    {
        public static int[] And_bDnEA = new int[] { 12, 12, 14, 16, 18, 16, 20 };
        public static int[] And_wDnEA = new int[] { 12, 12, 14, 16, 18, 16, 20 };
        public static int[] And_lDnEA = new int[] { 20, 20, 22, 24, 26, 24, 28 };
        public static int[] And_bEADn = new int[] { 4, 8, 8,10, 12, 14, 12, 16, 12, 14,8 };
        public static int[] And_wEADn = new int[] { 4, 8, 8,10, 12, 14, 12, 16, 12, 14,8 };
        public static int[] And_lEADn = new int[] { 8,14,14,16, 18, 20, 18, 22, 18, 20,14 };
        public static int[] Andi_b = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Andi_w = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Andi_l = new int[] { 16, 28, 28, 30, 32, 24, 32, 36 };
        public static int[] Ori_b = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Ori_w = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Ori_l = new int[] { 16, 30, 30, 32, 34, 36, 34, 38 };
        public static int[] Btst08 = new int[] { 10, 12, 12, 14, 16, 18, 16, 20, 16, 18 };
        public static int[] Btst = new int[] { 6, 8, 8, 10, 12, 14, 12, 16, 12, 14, 10 };
        public static int[] Bset_i = new int[] { 12, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Bset = new int[] { 8, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Bclr_i = new int[] { 14, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Cmpi_b = new int[] { 8, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Cmpi_w = new int[] { 8, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Cmpi_l = new int[] { 14, 20, 20, 22, 24, 26, 24, 28 };
        public static int[] Or_b = new int[] { 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Or_w = new int[] { 12, 12, 14, 16, 18, 16, 20 };
        public static int[] OrEaDn_b = new int[] { 4, -1, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] OrEaDn_w = new int[] { 4, -1, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] OrEaDn_l = new int[] { 8, -1, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };
        public static int[] Eor_b = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Eor_w = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Eor_l = new int[] { 8, 20, 20, 22, 24, 26, 24, 28 };
        public static int[] Eori_b = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Eori_w = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Eori_l = new int[] { 16, 28, 28, 30, 32, 34, 32, 36 };
        public static int[] Clsrlsl_wea = new int[] { -1, -1, 12, 12, 14, 16, 18, 16, 20, -1, -1, -1 };

        public static int[] Movea_w = new int[]
        {
            4,4,8,8,10,12,14,12,16,12,14,8
        };
        public static int[] Movea_l = new int[]
        {
            4,4,12,12,14,16,18,16,20,16,18,12
        };

        public static int[][] Move_b = new int[][]
        {
            new int[]{4,8,8,8,12,14,12,16 },
            new int[]{-1,-1,-1,-1,-1,-1,-1,-1 },
            new int[]{8,12,12,12,16,18,16,20 },
            new int[]{8,12,12,12,16,18,16,20 },
            new int[]{10,14,14,14,18,20,18,22 },
            new int[]{12,16,16,16,20,22,20,24 },
            new int[]{14,18,18,18,22,24,22,26 },
            new int[]{12,16,16,16,20,22,20,24 },
            new int[]{16,20,20,20,24,26,24,28 },
            new int[]{12,16,16,16,20,22,20,24 },
            new int[]{14,18,18,18,22,24,22,26 },
            new int[]{8,12,12,12,16,18,16,20 }
        };
        public static int[][] Move_w = new int[][]
        {
            new int[]{4,8,8,8,12,14,12,16 },
            new int[]{4,8,8,8,12,14,12,16 },
            new int[]{8,12,12,12,16,18,16,20 },
            new int[]{8,12,12,12,16,18,16,20 },
            new int[]{10,14,14,14,18,20,18,22 },
            new int[]{12,16,16,16,20,22,20,24 },
            new int[]{14,18,18,18,22,24,22,26 },
            new int[]{12,16,16,16,20,22,20,24 },
            new int[]{16,20,20,20,24,26,24,28 },
            new int[]{12,16,16,16,20,22,20,24 },
            new int[]{14,18,18,18,22,24,22,26 },
            new int[]{8,12,12,12,16,18,16,20 }
        };
        public static int[][] Move_l = new int[][]
        {
            new int[]{4,12,12,12,16,18,16,20 },
            new int[]{4,12,12,12,16,18,16,20 },
            new int[]{12,20,20,20,24,26,24,28 },
            new int[]{12,20,20,20,24,26,24,28 },
            new int[]{14,22,22,22,26,28,26,30 },
            new int[]{16,24,24,24,28,30,28,32 },
            new int[]{18,26,26,26,30,32,30,34 },
            new int[]{16,24,24,24,28,30,28,32 },
            new int[]{20,28,28,28,32,34,32,36 },
            new int[]{16,24,24,24,28,30,28,32 },
            new int[]{18,26,26,26,30,32,30,34 },
            new int[]{12,20,20,20,24,26,24,28 }
        };

        public static int[] MovemFromReg_w = new int[] { 4,4,4,4,4,4 };
        public static int[] MovemFromReg_l = new int[] { 8,8,8,8,8,8 };
        public static int[] MovemToReg_w0 = new int[] { 12, 12, 16, 18, 16, 20, 16, 18 };
        public static int[] MovemToReg_w1 = new int[] { 4, 4, 4, 4, 4, 4, 4, 4 };
        public static int[] MovemToReg_l0 = new int[] { -1, -1, 12, 12, -1, 16, 18, 16, 20, 16, 18 };
        public static int[] MovemToReg_l1 = new int[] { -1, -1, 8, 8, -1, 8, 8, 8, 8, 8, 8 };
        public static int[] MoveToCcr_w = new int[] { 12, -1, 16, 16, 18, 20, 22, 20, 24, 20, 22, 16 };
        public static int[] MoveFromSr_w = new int[] { 6, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] MoveToSr_w = new int[] { 12, -1, 16, 16, 18, 20, 22, 20, 24, 20, 22, 16 };

        public static int[] Addq_b = new int[] { 4, -1, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Addq_w = new int[] { 4, 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Addq_l = new int[] { 8, 8, 20, 20, 22, 24, 26, 24, 28 };
        public static int[] Addi_b = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Addi_w = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Addi_l = new int[] { 16, 28, 28, 30, 32, 34, 32, 36 };
        public static int[] Add0_b = new int[] { 4, -1, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] Add0_w = new int[] { 4, 4, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] Add0_l = new int[] { 8, 8, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };
        public static int[] Add1_b = new int[] { 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Add1_w = new int[] { 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Add1_l = new int[] { 20, 20, 22, 24, 26, 24, 28 };
        public static int[] Adda_w = new int[] { 8, 8, 12, 12, 14, 16, 18, 16, 20, 16, 18, 12 };
        public static int[] Adda_l = new int[] { 8, 8, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };

        public static int[] Subq_b = new int[] { 4, -1, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Subq_w = new int[] { 4, 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Subq_l = new int[] { 8, 8, 16, 16, 22, 24, 26, 24, 28 };
        public static int[] Sub_b = new int[] { 4, -1, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] Sub_w = new int[] { 4, 4, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] Sub_l = new int[] { 8, 8, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };
        public static int[] Suba_w = new int[] { 8, 8, 12, 12, 14, 16, 18, 16, 20, 16, 18, 12 };
        public static int[] Suba_l = new int[] { 8, 8, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };
        public static int[] Sub_bDn = new int[] { -1, -1, 12, 12, 14, 16, 18, 16, 20, -1, -1, -1 };
        public static int[] Sub_wDn = new int[] { -1, -1, 12, 12, 14, 16, 18, 16, 20, -1, -1, -1 };
        public static int[] Sub_lDn = new int[] { -1, -1, 20, 20, 22, 24, 26, 24, 28, -1, -1, -1 };
        public static int[] Subi_b = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Subi_w = new int[] { 8, 16, 16, 18, 20, 22, 20, 24 };
        public static int[] Subi_l = new int[] { 16, 28, 28, 30, 32, 34, 32, 36 };

        public static int[] Cmp_b = new int[] { 4, -1, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] Cmp_w = new int[] { 4, 4, 8, 8, 10, 12, 14, 12, 16, 12, 14, 8 };
        public static int[] Cmp_l = new int[] { 6, 6, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };
        public static int[] Cmpa_l = new int[] { 6, 6, 14, 14, 16, 18, 20, 18, 22, 18, 20, 14 };

        public static int[] Mulu_w = new int[] { 70, -1, 74, 74, 76, 78, 80, 78, 82, 78, 80, 74 };
        public static int[] Muls_w = new int[] { 70, -1, 74, 74, 76, 78, 80, 78, 82, 78, 80, 74 };
        public static int[] Divu_w = new int[] { 140, -1, 144, 144, 146, 148, 150, 148, 152, 148, 150, 144 };
        public static int[] Divs_w = new int[] { 158, -1, 162, 162, 164, 166, 168, 166, 170, 166, 168, 162 };

        public static int[] Pea_l = new int[] { 0, 0, 14, 0, 0, 18, 22, 18, 22, 18, 22, 0 };

        public static int[] Clr_b = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Clr_w = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Clr_l = new int[] { 6, 20, 20, 22, 24, 26, 24, 28 };

        public static int[] Not_b = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };

        public static int[] Tst_b = new int[] { 4, 8, 8, 10, 12, 14, 12, 16 };
        public static int[] Tst_w = new int[] { 4, 8, 8, 10, 12, 14, 12, 16 };
        public static int[] Tst_l = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };

        public static int[] Neg_b = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Neg_w = new int[] { 4, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Neg_l = new int[] { 6, 20, 20, 22, 24, 26, 24, 28 };

        public static int[] Tas = new int[] { 4, 14, 14, 16, 18, 20, 18, 22 };

        public static int[] Scc_b = new int[] { 6, 12, 12, 14, 16, 18, 16, 20 };
        public static int[] Jsr_l = new int[] { 16, 18, 22, 18, 20, 18, 22 };
        public static int[] Jmp = new int[] { 8, 10, 14, 10, 12, 10, 14 };

    }
}
