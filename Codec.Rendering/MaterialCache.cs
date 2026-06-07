// Copyright © John Gietzen. All Rights Reserved. This source is subject to the MIT license. Please see license.md for more information.

namespace Codec.Rendering
{
    using System;
    using System.Numerics;
    using Codec.Archives;
    using Codec.Files;
    using Silk.NET.OpenGL;

    public abstract class MaterialCache
    {
        public static MaterialCache Create(GL gl, Model model, string path, NestedFileSystemManager fsm)
        {
            return model switch
            {
                KmdFile.Model1 model1 => new MaterialCache1(gl, model1, path, fsm),
                KmsFile.Model2 model2 => new MaterialCache2(gl, model2, path, fsm),
                _ => throw new NotImplementedException(),
            };
        }

        public abstract ShaderHandle<(Vector3 Position, Vector2 UV)>? Resolve(Model.Mesh.Face face);

        protected abstract TextureHandle? GetTexture(Model.Mesh.Face face);
    }
}
