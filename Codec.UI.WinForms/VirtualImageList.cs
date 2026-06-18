// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public class VirtualImageList<T> : Control
        where T : notnull
    {
        private const int ImageSize = 128;
        private readonly Func<T, Task<Bitmap>> getImage;
        private readonly InterpolationMode interpolation;
        private readonly Dictionary<T, Task<Bitmap>> images = [];
        private readonly SemaphoreSlim semaphore = new(5);
        private readonly ScrollBar scrollBar;
        private List<T> items;

        public VirtualImageList(IEnumerable<T> items, Func<T, Task<Bitmap>> getImage, InterpolationMode interpolation = InterpolationMode.Default)
            : this(getImage, interpolation)
        {
            this.items = [.. items];
        }

        public VirtualImageList(Func<T, Task<Bitmap>> getImage, InterpolationMode interpolation = InterpolationMode.Default)
        {
            this.getImage = getImage;
            this.interpolation = interpolation;
            this.DoubleBuffered = true;
            this.ResizeRedraw = true;
            this.scrollBar = new VScrollBar
            {
                Minimum = 0,
                Dock = DockStyle.Right,
            };
            this.scrollBar.SmallChange = ImageSize;
            this.scrollBar.ValueChanged += this.ScrollBar_ValueChanged;
            this.Controls.Add(this.scrollBar);
        }

        public IEnumerable<T> Items
        {
            set
            {
                this.items = [.. value];
                this.images.Clear(); // TODO: Keep images for items that are still present and Dispose images for items that are no longer present.
                this.scrollBar.Value = 0;
                this.Resize();
                this.Invalidate();
            }
        }

        public bool HitTest(Point p, [NotNullWhen(true)] out T? hit)
        {
            var columns = Math.Max(1, (this.ClientSize.Width - this.scrollBar.Width) / ImageSize);
            var col = p.X / ImageSize;
            var row = (p.Y + this.scrollBar.Value) / ImageSize;
            var index = row * columns + col;
            if (index >= 0 && index < this.items.Count)
            {
                hit = this.items[index];
                return true;
            }

            hit = default;
            return false;
        }

        protected override void OnAutoSizeChanged(EventArgs e)
        {
            base.OnAutoSizeChanged(e);
            this.Resize();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Resize();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            this.scrollBar.Value = Math.Clamp(this.scrollBar.Value - e.Delta, this.scrollBar.Minimum, this.scrollBar.Maximum);
        }

        private void ScrollBar_ValueChanged(object? sender, EventArgs e)
        {
            this.Invalidate();
        }

        private void Resize()
        {
            var width = Math.Max(this.ClientSize.Width - this.scrollBar.Width, ImageSize);
            var columns = Math.Max(1, width / ImageSize);
            var rows = (this.items.Count + columns - 1) / columns;
            var height = rows * ImageSize;
            this.scrollBar.Maximum = Math.Max(0, height - this.ClientSize.Height);
            this.scrollBar.LargeChange = this.ClientSize.Height / ImageSize;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var width = this.ClientSize.Width - this.scrollBar.Width;
            var yOffset = Math.Clamp(this.scrollBar.Value, this.scrollBar.Minimum, this.scrollBar.Maximum);
            var clip = e.ClipRectangle;
            clip.Offset(0, yOffset);
            var columns = Math.Max(1, width / ImageSize);

            var rowStart = clip.Top / ImageSize;
            var rowEnd = (clip.Bottom + ImageSize - 1) / ImageSize;

            void ZoomImage(Bitmap bmp, RectangleF destRect)
            {
                var scale = Math.Min(destRect.Width / bmp.Width, destRect.Height / bmp.Height);
                var drawWidth = (int)(bmp.Width * scale);
                var drawHeight = (int)(bmp.Height * scale);

                var dx = destRect.X + (destRect.Width - drawWidth) / 2;
                var dy = destRect.Y + (destRect.Height - drawHeight) / 2;

                var state = e.Graphics.Save();
                e.Graphics.InterpolationMode = this.interpolation;
                e.Graphics.DrawImage(bmp, new RectangleF(dx, dy, drawWidth, drawHeight));
                e.Graphics.Restore(state);
            }

            for (var row = rowStart; row < rowEnd; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var index = row * columns + col;
                    if (index >= this.items.Count)
                    {
                        return;
                    }

                    var task = this.GetBitmapAsync(index);
                    if (!task.IsCompletedSuccessfully || !(task.Result is Bitmap bmp))
                    {
                        continue;
                    }

                    var destRect = new Rectangle(col * ImageSize, row * ImageSize - yOffset, ImageSize, ImageSize);

                    ZoomImage(bmp, destRect);
                }
            }
        }

        private Task<Bitmap> GetBitmapAsync(int index)
        {
            var item = this.items[index];
            Task<Bitmap> task;
            lock (this.images)
            {
                if (!this.images.TryGetValue(item, out task!))
                {
                    async Task<Bitmap> GetAsync(T item)
                    {
                        await this.semaphore.WaitAsync().ConfigureAwait(false);
                        try
                        {
                            var bmp = await this.getImage(item).ConfigureAwait(true);
                            var columns = Math.Max(1, (this.ClientSize.Width - this.scrollBar.Width) / ImageSize);
                            var row = index / columns;
                            var col = index % columns;
                            this.Invalidate(new Rectangle(col * ImageSize, row * ImageSize, ImageSize, ImageSize));
                            return bmp;
                        }
                        finally
                        {
                            this.semaphore.Release();
                        }
                    }

                    this.images[item] = task = GetAsync(item);
                }
            }

            return task;
        }
    }
}
