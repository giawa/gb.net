using ImGuiNET;
using OpenGL;
using SDL2;
using System;
using System.Numerics;

namespace GBCS
{
    class Program
    {
        private static ShaderProgram program, guiProgram;
        private static VBO<Vector3> cube;
        private static VBO<Vector2> cubeUV;
        private static VBO<uint> cubeQuads;
        private static Texture crateTexture;
        private static System.Diagnostics.Stopwatch watch;
        private static float angle;

        public static string VertexShader = @"
#version 130

in vec3 vertexPosition;
in vec2 vertexUV;

out vec2 uv;

uniform mat4 projection_matrix;
uniform mat4 view_matrix;
uniform mat4 model_matrix;

void main(void)
{
    uv = vertexUV;
    gl_Position = projection_matrix * view_matrix * model_matrix * vec4(vertexPosition, 1);
}
";

        public static string FragmentShader = @"
#version 130

uniform sampler2D texture;

in vec2 uv;

out vec4 fragment;

void main(void)
{
    fragment = texture2D(texture, vec2(1 - uv.x, 1 - uv.y));
}
";

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

        private static uint g_VboHandle, g_ElementsHandle;

        private static void Init()
        {
            // compile the shader program
            program = new ShaderProgram(VertexShader, FragmentShader);
            guiProgram = new ShaderProgram(GuiVertexShader, GuiFragmentShader);

            guiProgram.Use();
            g_AttribLocationTex = guiProgram["FontTexture"].Location;
            g_AttribLocationProjMtx = guiProgram["projection_matrix"].Location;
            g_AttribLocationVtxPos = Gl.GetAttribLocation(guiProgram.ProgramID, "in_position");
            g_AttribLocationVtxUV = Gl.GetAttribLocation(guiProgram.ProgramID, "in_texCoord");
            g_AttribLocationVtxColor = Gl.GetAttribLocation(guiProgram.ProgramID, "in_color");

            g_VboHandle = Gl.GenBuffer();
            g_ElementsHandle = Gl.GenBuffer();

            // set the view and projection matrix, which are static throughout this tutorial
            program.Use();
            program["projection_matrix"].SetValue(Matrix4.CreatePerspectiveFieldOfView(0.45f, (float)width / height, 0.1f, 1000f));
            program["view_matrix"].SetValue(Matrix4.LookAt(new Vector3(0, 0, 10), Vector3.Zero, new Vector3(0, 1, 0)));

            // create a crate with vertices and UV coordinates
            cube = new VBO<Vector3>(new Vector3[] {
                new Vector3(1, 1, -1), new Vector3(-1, 1, -1), new Vector3(-1, 1, 1), new Vector3(1, 1, 1),
                new Vector3(1, -1, 1), new Vector3(-1, -1, 1), new Vector3(-1, -1, -1), new Vector3(1, -1, -1),
                new Vector3(1, 1, 1), new Vector3(-1, 1, 1), new Vector3(-1, -1, 1), new Vector3(1, -1, 1),
                new Vector3(1, -1, -1), new Vector3(-1, -1, -1), new Vector3(-1, 1, -1), new Vector3(1, 1, -1),
                new Vector3(-1, 1, 1), new Vector3(-1, 1, -1), new Vector3(-1, -1, -1), new Vector3(-1, -1, 1),
                new Vector3(1, 1, -1), new Vector3(1, 1, 1), new Vector3(1, -1, 1), new Vector3(1, -1, -1) });
            cubeUV = new VBO<Vector2>(new Vector2[] {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });

            cubeQuads = new VBO<uint>(new uint[] { /*0, 1, 2, 3, 4, 5, 6, 7,*/ 8, 9, 10, 11, /*12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23*/ }, BufferTarget.ElementArrayBuffer);

            // load a crate texture
            crateTexture = new Texture("crate.jpg");
            watch = System.Diagnostics.Stopwatch.StartNew();
        }

        private static void Render()
        {
            // calculate how much time has elapsed since the last frame
            watch.Stop();
            float deltaTime = (float)watch.ElapsedTicks / System.Diagnostics.Stopwatch.Frequency;
            watch.Restart();

            // use the deltaTime to adjust the angle of the cube
            //angle += deltaTime;

            // use our shader program and bind the crate texture
            Gl.Enable(EnableCap.Blend);
            Gl.BlendEquation(BlendEquationMode.FuncAdd);
            Gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            Gl.UseProgram(program);
            Gl.BindTexture(_fontTexture);

            // set the transformation of the cube
            program["model_matrix"].SetValue(Matrix4.CreateRotationY(angle / 2) * Matrix4.CreateRotationX(angle));

            // bind the vertex positions, UV coordinates and element array
            Gl.BindBufferToShaderAttribute(cube, program, "vertexPosition");
            Gl.BindBufferToShaderAttribute(cubeUV, program, "vertexUV");
            Gl.BindBuffer(cubeQuads);

            // draw the textured cube
            Gl.DrawElements(BeginMode.Quads, cubeQuads.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
        }

        private static void Dispose()
        {
            // dispose of all of the resources that were created
            cube.Dispose();
            cubeUV.Dispose();
            cubeQuads.Dispose();
            _fontTexture.Dispose();
            program.DisposeChildren = true;
            program.Dispose();
            guiProgram.DisposeChildren = true;
            guiProgram.Dispose();
        }

        private static Texture _fontTexture;
        private static IntPtr _fontAtlasID = (IntPtr)1;

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

        private static int width = 1280, height = 720;
        //private static int width = 1280, height = 720;

        private static bool mouseleft, mouseright;
        private static int mouseX, mouseY, mouseWheel;

        static void Main(string[] args)
        {
            if (SDL.SDL_Init(SDL.SDL_INIT_EVERYTHING) < 0)
            {
                Console.WriteLine("SDL failed to init.");
                return;
            }

            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DOUBLEBUFFER, 1);
            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_DEPTH_SIZE, 24);

            SDL.SDL_GL_SetAttribute(SDL.SDL_GLattr.SDL_GL_CONTEXT_MAJOR_VERSION, 3);

            // set to the gameboy resolution with 4x scaling for now
            IntPtr window = SDL.SDL_CreateWindow("GameBoy Emulator", 128, 128, width, height, SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);

            if (window == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a window.");
                return;
            }

            IntPtr context = SDL.SDL_GL_CreateContext(window);

            if (context == IntPtr.Zero)
            {
                Console.WriteLine("SDL could not create a valid OpenGL context.");
                return;
            }

            SDL.SDL_Event sdlEvent;
            bool running = true;
            IntPtr sdlSurface = IntPtr.Zero, sdlTexture = IntPtr.Zero;

            var imguiContext = ImGui.CreateContext();
            ImGui.StyleColorsDark();

            RecreateFontDeviceTexture();

            bool demoWindow = true;

            Init();

            while (running)
            {
                try
                {
                    while (SDL.SDL_PollEvent(out sdlEvent) != 0)
                    {
                        if (sdlEvent.type == SDL.SDL_EventType.SDL_QUIT)
                        {
                            running = false;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYDOWN)
                        {
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_KEYUP)
                        {
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEBUTTONDOWN)
                        {
                            if (sdlEvent.button.button == 1) mouseleft = true;
                            if (sdlEvent.button.button == 3) mouseright = true;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEBUTTONUP)
                        {
                            if (sdlEvent.button.button == 1) mouseleft = false;
                            if (sdlEvent.button.button == 3) mouseright = false;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEMOTION)
                        {
                            mouseX = sdlEvent.motion.x;
                            mouseY = sdlEvent.motion.y;
                        }
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_MOUSEWHEEL)
                        {
                            mouseWheel = sdlEvent.wheel.y;
                        }
                    }

                    if (sdlTexture != IntPtr.Zero) SDL.SDL_DestroyTexture(sdlTexture);

                    // set up the OpenGL viewport and clear both the color and depth bits
                    Gl.Viewport(0, 0, width, height);
                    Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    //Render();

                    UpdateImGuiInput();

                    ImGui.NewFrame();
                    ImGui.ShowDemoWindow(ref demoWindow);
                    ImGui.Render();

                    // try to render imgui
                    var drawData = ImGui.GetDrawData();
                    RenderImDrawData(drawData);

                    SDL.SDL_GL_SwapWindow(window);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            Dispose();

            SDL.SDL_GL_DeleteContext(context);
            SDL.SDL_DestroyWindow(window);
        }

        private static void UpdateImGuiInput()
        {
            ImGuiIOPtr io = ImGui.GetIO();

            Vector2 mousePosition = new Vector2(mouseX, mouseY);

            io.MouseDown[0] = mouseleft;// leftPressed || snapshot.IsMouseDown(MouseButton.Left);
            io.MouseDown[1] = mouseright;// rightPressed || snapshot.IsMouseDown(MouseButton.Right);
            io.MouseDown[2] = false;// middlePressed || snapshot.IsMouseDown(MouseButton.Middle);
            io.MousePos = mousePosition;
            io.MouseWheel = mouseWheel;

            mouseWheel = 0;
        }

        private static int g_AttribLocationTex = 0, g_AttribLocationProjMtx = 0;                                // Uniforms location
        private static int g_AttribLocationVtxPos = 0, g_AttribLocationVtxUV = 0, g_AttribLocationVtxColor = 0;

        private static void RenderImDrawData(ImDrawDataPtr draw_data)
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
            io.DisplaySize = new Vector2(width, height);

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

                        if (clip_rect.X < width && clip_rect.Y < height && clip_rect.Z >= 0.0f && clip_rect.W >= 0.0f)
                        {
                            // Apply scissor/clipping rectangle
                            Gl.Scissor((int)clip_rect.X, (int)(height - clip_rect.W), (int)(clip_rect.Z - clip_rect.X), (int)(clip_rect.W - clip_rect.Y));

                            Gl.BindTexture(TextureTarget.Texture2D, _fontTexture.TextureID);
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
