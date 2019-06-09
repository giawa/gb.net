using ImGuiNET;
using OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace GB
{
    public static class Gui
    {
        private static ShaderProgram guiProgram;

        private static int g_AttribLocationTex = 0, g_AttribLocationProjMtx = 0;                                // Uniforms location
        private static int g_AttribLocationVtxPos = 0, g_AttribLocationVtxUV = 0, g_AttribLocationVtxColor = 0;

        private static Texture _fontTexture;
        private static IntPtr _fontAtlasID = (IntPtr)1;

        #region GLSL Shader
        public static string GuiVertexShader = @"
#version 330 core

uniform mat4 projection_matrix;

in vec2 in_position;
in vec2 in_texCoord;
in vec4 in_color;

out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;
	texCoord = in_texCoord;
}
";

        public static string GuiFragmentShader = @"
#version 330 core

uniform sampler2D FontTexture;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    outputColor = color * texture(FontTexture, texCoord);
}
";
        #endregion

        private static uint g_VboHandle, g_ElementsHandle;

        private static int _width, _height;

        public static void Reshape(int width, int height)
        {
            _width = width;
            _height = height;
        }

        public static void Init()
        {
            // compile the shader program
            guiProgram = new ShaderProgram(GuiVertexShader, GuiFragmentShader);

            guiProgram.Use();
            g_AttribLocationTex = guiProgram["FontTexture"].Location;
            g_AttribLocationProjMtx = guiProgram["projection_matrix"].Location;
            g_AttribLocationVtxPos = Gl.GetAttribLocation(guiProgram.ProgramID, "in_position");
            g_AttribLocationVtxUV = Gl.GetAttribLocation(guiProgram.ProgramID, "in_texCoord");
            g_AttribLocationVtxColor = Gl.GetAttribLocation(guiProgram.ProgramID, "in_color");

            g_VboHandle = Gl.GenBuffer();
            g_ElementsHandle = Gl.GenBuffer();
        }

        public static void Dispose()
        {
            // dispose of all of the resources that were created
            _fontTexture.Dispose();
            guiProgram.DisposeChildren = true;
            guiProgram.Dispose();
        }

        public static void RecreateFontDeviceTexture()
        {
            ImGuiIOPtr io = ImGui.GetIO();
            // Build
            //byte[] pixels;
            IntPtr pixels;
            int width, height, bytesPerPixel;
            io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out bytesPerPixel);
            // Store our identifier
            io.Fonts.SetTexID(_fontAtlasID);

            _fontTexture = new Texture(pixels, width, height, PixelFormat.Rgba, PixelInternalFormat.Rgba);

            io.Fonts.ClearTexData();
        }

        public static void RenderImDrawData(ImDrawDataPtr draw_data, Texture frameTexture)
        {
            if (draw_data.CmdListsCount == 0)
            {
                return;
            }

            Gl.Enable(EnableCap.Blend);
            Gl.BlendEquation(BlendEquationMode.FuncAdd);
            Gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            Gl.Disable(EnableCap.CullFace);
            Gl.Disable(EnableCap.DepthTest);
            Gl.Enable(EnableCap.ScissorTest);

            ImGuiIOPtr io = ImGui.GetIO();
            io.DisplaySize = new Vector2(_width, _height);

            Matrix4 mvp = Matrix4.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0.0f, -1.0f, 1.0f);

            guiProgram.Use();
            Gl.Uniform1f(g_AttribLocationTex, 0);
            Gl.UniformMatrix4fv(g_AttribLocationProjMtx, mvp);

            Gl.BindBuffer(BufferTarget.ArrayBuffer, g_VboHandle);
            Gl.BindBuffer(BufferTarget.ElementArrayBuffer, g_ElementsHandle);

            Gl.EnableVertexAttribArray(g_AttribLocationVtxPos);
            Gl.EnableVertexAttribArray(g_AttribLocationVtxUV);
            Gl.EnableVertexAttribArray(g_AttribLocationVtxColor);
            Gl.VertexAttribPointer(g_AttribLocationVtxPos, 2, VertexAttribPointerType.Float, false, 20, (IntPtr)0);
            Gl.VertexAttribPointer(g_AttribLocationVtxUV, 2, VertexAttribPointerType.Float, false, 20, (IntPtr)8);
            Gl.VertexAttribPointer(g_AttribLocationVtxColor, 4, VertexAttribPointerType.UnsignedByte, true, 20, (IntPtr)16);

            var clip_off = draw_data.DisplayPos;         // (0,0) unless using multi-viewports
            var clip_scale = draw_data.FramebufferScale; // (1,1) unless using retina display which are often (2,2)

            for (int n = 0; n < draw_data.CmdListsCount; n++)
            {
                ImDrawListPtr cmd_list = draw_data.CmdListsRange[n];

                Gl.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(cmd_list.VtxBuffer.Size * 20), cmd_list.VtxBuffer.Data, BufferUsageHint.DynamicDraw);
                Gl.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(cmd_list.IdxBuffer.Size * 2), cmd_list.IdxBuffer.Data, BufferUsageHint.DynamicDraw);

                Vector2[] vertices = new Vector2[cmd_list.VtxBuffer.Size];
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = cmd_list.VtxBuffer[i].pos;

                int idx_offset = 0;
                int vtx_offset = 0;

                for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
                {
                    var pcmd = cmd_list.CmdBuffer[cmd_i];

                    if (pcmd.UserCallback != IntPtr.Zero)
                    {

                    }
                    else
                    {
                        Vector4 clip_rect;
                        clip_rect.X = (pcmd.ClipRect.X - clip_off.X) * clip_scale.X;
                        clip_rect.Y = (pcmd.ClipRect.Y - clip_off.Y) * clip_scale.Y;
                        clip_rect.Z = (pcmd.ClipRect.Z - clip_off.X) * clip_scale.X;
                        clip_rect.W = (pcmd.ClipRect.W - clip_off.Y) * clip_scale.Y;

                        if (clip_rect.X < _width && clip_rect.Y < _height && clip_rect.Z >= 0.0f && clip_rect.W >= 0.0f)
                        {
                            // Apply scissor/clipping rectangle
                            Gl.Scissor((int)clip_rect.X, (int)(_height - clip_rect.W), (int)(clip_rect.Z - clip_rect.X), (int)(clip_rect.W - clip_rect.Y));

                            if (pcmd.TextureId == (IntPtr)1 || frameTexture == null) Gl.BindTexture(TextureTarget.Texture2D, _fontTexture.TextureID);
                            else Gl.BindTexture(TextureTarget.Texture2D, frameTexture.TextureID);
                            Gl.DrawElementsBaseVertex(BeginMode.Triangles, (int)pcmd.ElemCount, DrawElementsType.UnsignedShort, (IntPtr)(idx_offset * 2), vtx_offset);
                        }

                        idx_offset += (int)pcmd.ElemCount;
                    }
                }
            }

            Gl.Disable(EnableCap.ScissorTest);
            Gl.Enable(EnableCap.DepthTest);
            Gl.Enable(EnableCap.CullFace);
            Gl.Disable(EnableCap.Blend);
        }
    }
}
