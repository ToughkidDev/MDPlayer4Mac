// Common surface every per-chip channel visualizer (Sn76489Visualizer, Ym2612Visualizer,
// etc.) already exposed informally (a `Screen` to dock, `ScreenChangeParams()` to pull fresh
// register state, `ScreenDrawParams()` to redraw only what changed) - pulled out as an
// interface so MainWindow.axaml.cs can hold a second-chip-instance visualizer generically
// (see ShowVisualizersFor's dual-chip handling) instead of needing a second named field +
// second docking/redraw/hide call site per chip type. Only used for the *secondary* (chip
// instance 1) visualizer of a dual-chip VGM; the primary (instance 0) fields stay as named
// fields exactly as before, since those are still constructed unconditionally and it isn't
// worth losing the readability of a named field for the common case.
namespace MDPlayer.UI.Visualizer
{
    public interface IChannelVisualizer
    {
        PixelScreen Screen { get; }
        void ScreenChangeParams();
        void ScreenDrawParams();
    }
}
