namespace Codec.Files
{
    using Assimp;

    public record class RenderableScene(Scene Scene)
    {
        public static explicit operator RenderableScene(Scene scene) => scene != null ? new(scene) : null;

        public static implicit operator Scene(RenderableScene renderableScene) => renderableScene?.Scene;
    }
}
