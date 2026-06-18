// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.Files
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    public class Model(Model.Mesh[] meshes)
    {
        public Mesh[] Meshes { get; } = meshes;

        public class Mesh : INotifyPropertyChanging, INotifyPropertyChanged
        {
            private Mesh? relativeMesh;
            private Matrix4x4 rotation = Matrix4x4.Identity;
            private Matrix4x4 modelToWorld;
            private Vector3[] vertices;

            public Mesh(Vector3 relativeOrigin, Vector3[] relativeVertices, Vector2[] textureCoords, Vector3[] normals, Face[] faces, Mesh? relativeMesh = null)
            {
                this.RelativeOrigin = relativeOrigin;
                this.RelativeVertices = relativeVertices;
                this.TextureCoords = textureCoords;
                this.Normals = normals;
                this.Faces = faces;

                this.relativeMesh = relativeMesh;
                this.relativeMesh?.PropertyChanged += this.RelativeMesh_PropertyChanged;
                this.Recalculate();
            }

            /// <inheritdoc/>
            public event PropertyChangingEventHandler? PropertyChanging;

            /// <inheritdoc/>
            public event PropertyChangedEventHandler? PropertyChanged;

            public Vector3 RelativeOrigin { get; }

            public Vector3[] RelativeVertices { get; }

            public Vector2[] TextureCoords { get; }

            public Vector3[] Normals { get; }

            public Face[] Faces { get; }

            public Mesh? RelativeMesh
            {
                get => this.relativeMesh;
                set
                {
                    if (ReferenceEquals(this.relativeMesh, value))
                    {
                        return;
                    }

                    this.OnPropertyChanging();
                    this.relativeMesh?.PropertyChanged -= this.RelativeMesh_PropertyChanged;
                    this.relativeMesh = value;
                    this.relativeMesh?.PropertyChanged += this.RelativeMesh_PropertyChanged;
                    this.OnPropertyChanged();

                    this.Recalculate();
                }
            }

            public Matrix4x4 Rotation
            {
                get => this.rotation;
                set
                {
                    if (this.rotation == value)
                    {
                        return;
                    }

                    this.OnPropertyChanging();
                    this.rotation = value;
                    this.OnPropertyChanged();

                    this.Recalculate();
                }
            }

            public Matrix4x4 ModelToWorld
            {
                get => this.modelToWorld;
                private set
                {
                    if (this.modelToWorld == value)
                    {
                        return;
                    }

                    this.OnPropertyChanging();
                    this.modelToWorld = value;
                    this.OnPropertyChanged();
                }
            }

            public Vector3[] Vertices
            {
                get => this.vertices;
                private set
                {
                    this.OnPropertyChanging();
                    this.vertices = value;
                    this.OnPropertyChanged();
                }
            }

            private (Matrix4x4 ModelToWorld, Vector3[] Vertices) ComputeDerived()
            {
                var parentToWorld = this.relativeMesh?.ModelToWorld ?? Matrix4x4.Identity;
                var mtw = this.rotation * Matrix4x4.CreateTranslation(this.RelativeOrigin) * parentToWorld;
                var verts = Array.ConvertAll(this.RelativeVertices, v => Vector3.Transform(v, mtw));
                return (mtw, verts);
            }

            private void Recalculate() =>
                (this.ModelToWorld, this.Vertices) = this.ComputeDerived();

            private void RelativeMesh_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName is nameof(this.ModelToWorld) or nameof(this.Vertices))
                {
                    this.Recalculate();
                }
            }

            protected void OnPropertyChanging([CallerMemberName] string? name = null) =>
                this.PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(name));

            protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
                this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            public class Face(IList<uint> vertexIndices, IList<uint> normalIndices, IList<uint> textureIndices)
            {
                public IList<uint> VertexIndices { get; } = vertexIndices;

                public IList<uint> NormalIndices { get; } = normalIndices;

                public IList<uint> TextureIndices { get; } = textureIndices;
            }
        }
    }
}
