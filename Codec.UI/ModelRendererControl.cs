namespace Codec.UI
{
    using Codec.Archives;
    using Codec.Files;
    using Codec.Rendering;

    public partial class ModelRendererControl : SilkControl
    {
        private readonly Model model;
        private readonly string path;
        private readonly NestedFileSystemManager fsm;
        private readonly GLModelViewer modelViewer;

        public ModelRendererControl(string path, NestedFileSystemManager fsm, Model model = null)
        {
            this.path = path;
            this.fsm = fsm;
            this.model = model;
            this.modelViewer = new GLModelViewer(this.path, this.fsm, this.model);
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
