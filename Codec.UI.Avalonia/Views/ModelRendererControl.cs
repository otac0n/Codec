namespace Codec.UI.Avalonia.Views
{
    using Codec.Archives;
    using Codec.Files;
    using Codec.Rendering;
    using Codec.Rendering.Input;
    using global::Avalonia.OpenGL;
    using global::Avalonia.OpenGL.Controls;
    using global::Avalonia.Threading;
    using Silk.NET.OpenGL;

    public class ModelRendererControl(string path, NestedFileSystemManager fsm, ControlChangeTracker changeTracker, RenderableScene? scene = null) : OpenGlControlBase
    {
        private GL? gl;
        private GLModelViewer modelViewer;

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);
            this.gl = GL.GetApi(gl.GetProcAddress);
            this.modelViewer = new GLModelViewer(path, fsm, changeTracker, scene);
            this.modelViewer.Initialize(this.gl);
        }


        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            this.modelViewer.Dispose();
            base.OnOpenGlDeinit(gl);
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            var scaling = this.VisualRoot?.RenderScaling ?? 1.0;
            var width = (int)(this.Bounds.Width * scaling);
            var height = (int)(this.Bounds.Height * scaling);
            this.modelViewer.Render(width, height);
            Dispatcher.UIThread.Post(this.RequestNextFrameRendering, DispatcherPriority.Background);
        }
    }
}
