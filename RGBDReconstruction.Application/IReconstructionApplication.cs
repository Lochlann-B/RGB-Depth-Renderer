using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace RGBDReconstruction.Application;

public interface IReconstructionApplication
{
    void Init(int windowWidth, int windowHeight);
    void RenderFrame(FrameEventArgs args);
    void Resize(ResizeEventArgs e);
    void UpdateFrame(bool IsFocused, Vector2 MousePosition, KeyboardState keyboardState, FrameEventArgs args);
    void MouseWheel(MouseWheelEventArgs e);
}