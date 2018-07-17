﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using SFGraphics.GLObjects;
using SFGraphics.GLObjects.Textures;

namespace Smash_Forge.Rendering
{
    static class TextureToBitmap
    {
        // TODO: Use separate thread.
        public static Bitmap RenderBitmap(Texture2D texture, 
            bool r = true, bool g = true, bool b = true, bool a = false)
        {
            // Set up the framebuffer and context to match the texture's dimensions.
            SetUpContextWindow(texture.Width, texture.Height);
            BufferObject screenVbo = RenderTools.CreateScreenQuadBuffer();
            Framebuffer framebuffer = new Framebuffer(FramebufferTarget.Framebuffer, texture.Width, texture.Height, PixelInternalFormat.Rgba);
            framebuffer.Bind();

            // Draw the specified color channels.
            GL.Viewport(0, 0, texture.Width, texture.Height);
            RenderTools.DrawTexturedQuad(texture.Id, 1, 1, r, g, b, a);

            return framebuffer.ReadImagePixels(a);
        }

        private static void SetUpContextWindow(int width, int height)
        {
            // Set up a context for this thread.
            GraphicsMode mode = new GraphicsMode(new ColorFormat(8, 8, 8, 8), 24, 0, 0, ColorFormat.Empty, 1);
            GameWindow window = new GameWindow(width, height, mode, "", OpenTK.GameWindowFlags.Default, OpenTK.DisplayDevice.Default, 3, 0, GraphicsContextFlags.Default);
            window.Visible = false;
            window.MakeCurrent();
        }
    }
}