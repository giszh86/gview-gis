﻿namespace gView.GraphicsEngine
{
    public struct CanvasRectangleF
    {
        public CanvasRectangleF(float left, float top, float width, float height)
        {
            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;
        }

        public CanvasRectangleF(CanvasRectangleF rectangle)
        {
            this.Left = rectangle.Left;
            this.Top = rectangle.Top;
            this.Width = rectangle.Width;
            this.Height = rectangle.Height;
        }

        public void Offset(float x, float y)
        {
            this.Left += x;
            this.Top += y;
        }

        public CanvasPointF Center => new CanvasPointF(Left + Width / 2f, Top + Height / 2f);

        public float Left { get; set; }
        public float Top { get; set; }

        public float Width { get; set; }
        public float Height { get; set; }
    }
}
