namespace Codec.UI.WinForms
{
    using Codec.Archives;
    using Codec.Files;
    using Codec.Rendering;
    using Codec.Rendering.Input;

    public partial class ModelRendererControl : SilkControl
    {
        private readonly RenderableScene scene;
        private readonly string path;
        private readonly NestedFileSystemManager fsm;
        private readonly GLModelViewer modelViewer;

        public ModelRendererControl(string path, NestedFileSystemManager fsm, ControlChangeTracker changeTracker, RenderableScene? scene = null)
        {
            this.path = path;
            this.fsm = fsm;
            this.scene = scene;
            this.modelViewer = new GLModelViewer(this.path, this.fsm, changeTracker, this.scene);
        }

        protected override void Initialize()
        {
            this.modelViewer.Initialize(this.gl);
        }

        protected override void Render()
        {
            this.modelViewer.Render(this.Width, this.Height);
        }
    }
}
