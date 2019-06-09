﻿using ImGuiNET;
using OpenGL;
using SDL2;
using System;
using System.Numerics;

namespace GB
{
    class Program
    {
        private static int _width = 1280, _height = 720;

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
            IntPtr window = SDL.SDL_CreateWindow("GameBoy Emulator", 128, 128, _width, _height, SDL.SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN | SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);

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

            Gui.Init();
            Gui.Reshape(_width, _height);
            var imguiContext = ImGui.CreateContext();
            ImGui.StyleColorsDark();
            Gui.RecreateFontDeviceTexture();

            bool demoWindow = true;

            while (running)
            {
                //try
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
                        else if (sdlEvent.type == SDL.SDL_EventType.SDL_WINDOWEVENT)
                        {
                            switch (sdlEvent.window.windowEvent)
                            {
                                case SDL.SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                                    OnReshape(sdlEvent.window.data1, sdlEvent.window.data2);
                                    break;
                            }
                        }
                    }

                    // set up the OpenGL viewport and clear both the color and depth bits
                    Gl.Viewport(0, 0, _width, _height);
                    Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                    UpdateImGuiInput();

                    ImGui.NewFrame();
                    //ImGui.ShowDemoWindow(ref demoWindow);
                    if (ImGui.BeginMainMenuBar())
                    {
                        if (ImGui.BeginMenu("File"))
                        {
                            if (ImGui.MenuItem("Open File", "Ctrl+O"))
                            {
                                showOpenDialog = true;
                                ImGui.SetNextWindowPos(new Vector2(100, 100));
                            }
                            if (ImGui.MenuItem("Load Test ROM"))
                            {
                                //Disassembler temp = new Disassembler(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\cpu_instrs.gb", 0x200);
                                Cartridge gamecart = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\tetris.gb");
                                Memory ram = new Memory();
                                CPU cpu = new CPU(ram);
                                LCD lcd = new LCD(ram);
                                cpu.LoadCartridge(gamecart);

                                // run a few clock cycles
                                var cpuStateMachine = cpu.CreateStateMachine();
                                int i = 0;

                                System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

                                //for (i = 0; i < 8192 * 4 + 66 + 47 + 4892; i++) cpuStateMachine.MoveNext();

                                for (i = 0; i < 100000000 && cpuStateMachine.MoveNext(); i++)
                                {
                                    // 4x lcd clocks per CPU single cycle instruction
                                    if ((ram[0xff40] & 0x80) == 0x80)
                                        lcd.Tick1mhz();
                                }

                                watch.Stop();
                                Console.WriteLine($"Executed {i} clocks in {watch.ElapsedMilliseconds}ms.");
                                Console.WriteLine($"Effective clock rate of {(double)i/watch.ElapsedMilliseconds/1000}MHz.");
                            }
                            ImGui.Separator();
                            if (ImGui.MenuItem("Exit"))
                            {
                                running = false;
                            }
                            ImGui.EndMenu();
                        }
                        ImGui.EndMainMenuBar();
                    }

                    if (showOpenDialog)
                    {
                        if (openDialog.DisplayFileDialog("Open File", new string[] { ".gb" }, @"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1", "."))
                        {
                            showOpenDialog = false;

                            if (openDialog.IsOk)
                            {
                                Console.WriteLine("Open file: " + openDialog.FullPath);

                                Cartridge gamecart = new Cartridge(openDialog.FullPath);
                                Memory ram = new Memory();
                                CPU cpu = new CPU(ram);
                                cpu.LoadCartridge(gamecart);
                            }
                        }
                    }

                    ImGui.Render();

                    // try to render imgui
                    var drawData = ImGui.GetDrawData();
                    Gui.RenderImDrawData(drawData);

                    SDL.SDL_GL_SwapWindow(window);
                }
                /*catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }*/
            }

            Gui.Dispose();

            SDL.SDL_GL_DeleteContext(context);
            SDL.SDL_DestroyWindow(window);
        }

        private static bool showOpenDialog = false;
        private static FileDialog openDialog = new FileDialog();

        private static void OnReshape(int width, int height)
        {
            _width = width;
            _height = height;
            Gui.Reshape(width, height);
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
    }
}
