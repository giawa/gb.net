using ImGuiNET;
using OpenGL;
using SDL2;
using System;
using System.Collections;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace GB
{
    class Program
    {
        private static int _width = 1280, _height = 720;

        private static bool mouseleft, mouseright;
        private static int mouseX, mouseY, mouseWheel;

        private static IEnumerator cpuState;
        private static Memory ram;
        private static CPU cpu;
        private static LCD lcd;
        private static Timer timer;

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

            SDL.SDL_GL_SetSwapInterval(1);

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
                                Cartridge tetris = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\tetris.gb");
                                Cartridge fullTest = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\cpu_instrs.gb");
                                Cartridge cpuTest1 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\01-special.gb");
                                Cartridge cpuTest2 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\02-interrupts.gb");
                                Cartridge cpuTest3 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\03-op sp,hl.gb");
                                Cartridge cpuTest4 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\04-op r,imm.gb");
                                Cartridge cpuTest5 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\05-op rp.gb");
                                Cartridge cpuTest6 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\06-ld r,r.gb");
                                Cartridge cpuTest7 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\07-jr,jp,call,ret,rst.gb");
                                Cartridge cpuTest8 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\08-misc instrs.gb");
                                Cartridge cpuTest9 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\09-op r,r.gb");
                                Cartridge cpuTest10 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\10-bit ops.gb");
                                Cartridge cpuTest11 = new Cartridge(@"E:\Tutorials\GB.net\GB.net\bin\Debug\netcoreapp2.1\blargg_tests\cpu_instrs\individual\11-op a,(hl).gb");
                                ram = new Memory();
                                timer = new Timer(ram);
                                cpu = new CPU(ram);
                                lcd = new LCD(ram);
                                cpu.LoadCartridge(fullTest);
                                cpu.SetPC(0x100);
                                
                                // run a few clock cycles
                                cpuState = cpu.CreateStateMachine().GetEnumerator();
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
                                ram = new Memory();
                                timer = new Timer(ram);
                                cpu = new CPU(ram);
                                cpu.LoadCartridge(gamecart);
                            }
                        }
                    }

                    // run a full frame of gameboy data
                    if (cpuState != null)
                    {
                        Stopwatch watch = Stopwatch.StartNew();
                        bool frameReady = false;
                        int ticks = 0;

                        while (!frameReady && cpuState.MoveNext() && ticks < 10000)
                        {
                            frameReady = lcd.Tick1MHz();
                            ram.Tick1MHz();
                            timer.Tick1MHz();   // TODO:  "If a TMA write is executed with the same 
                                                // timing of TMA being transferred to TIMA, then the TMA
                                                // write goes to TIMA as well" (p 26 Gameboy Dev Manual)

                            // register all interrupts
                            cpu.Interrupts = (byte)(timer.TimerInterrupt ? 0x04 : 0x00);
                            if (lcd.VBlankInterrupt)
                            {
                                cpu.Interrupts |= 0x01;
                                lcd.VBlankInterrupt = false;
                            }

                            ticks++;
                        }

                        watch.Stop();

                        // did the program terminate?
                        if (ticks >= 10000) ;
                        else if (!frameReady) cpuState = null;
                        else
                        {
                            //lcd.DumpTiles((ram[0xff40] & 0x10) == 0x10 ? 0x8000 : 0x8800);

                            if (frameTexture != null) frameTexture.Dispose();
                            var bitmapHandle = GCHandle.Alloc(lcd.backgroundTexture, GCHandleType.Pinned);
                            frameTexture = new Texture(bitmapHandle.AddrOfPinnedObject(), 160, 144, PixelFormat.Rgba, PixelInternalFormat.Rgba);
                            bitmapHandle.Free();

                            if (bgTexture != null) bgTexture.Dispose();
                            bitmapHandle = GCHandle.Alloc(lcd.DumpBackground(), GCHandleType.Pinned);
                            bgTexture = new Texture(bitmapHandle.AddrOfPinnedObject(), 256, 256, PixelFormat.Rgba, PixelInternalFormat.Rgba);
                            bitmapHandle.Free();
                            //Console.WriteLine("Frame after {0}ms", watch.ElapsedMilliseconds);
                        }
                    }

                    if (frameTexture != null)
                    {
                        ImGui.Begin("Emulator");

                        ImGui.Image((IntPtr)frameTexture.TextureID, new Vector2(160 * 2, 144 * 2));

                        ImGui.End();
                    }

                    if (bgTexture != null)
                    {
                        ImGui.Begin("Background");

                        ImGui.Image((IntPtr)bgTexture.TextureID, new Vector2(256 * 2, 256 * 2));

                        ImGui.End();
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

        private static Texture frameTexture, bgTexture;

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
