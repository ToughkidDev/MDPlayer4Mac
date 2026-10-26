namespace MDPlayer.MucoNative;

/// <summary>Ordered musical output; the host owns chip synthesis and audio devices.</summary>
public enum NativeChip { Opna1, Opna2, Opnb1, Opnb2, Opm, Psg, Opll, Scc }

public readonly record struct NativeChipWrite(NativeChip Chip, int Port, int Address, int Value);
