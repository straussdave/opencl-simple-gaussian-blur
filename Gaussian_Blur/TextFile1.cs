//using System;
//using System.IO;
//using OpenCL.Net;
//using SixLabors.ImageSharp;
//using SixLabors.ImageSharp.PixelFormats;

//namespace Gaussian_Blur
//{
//    internal class test
//    {
//        static void CheckStatus(ErrorCode err)
//        {
//            if (err != ErrorCode.Success)
//            {
//                Console.WriteLine("OpenCL Error: " + err.ToString());
//                System.Environment.Exit(1);
//            }
//        }

//        /// <summary>
//        /// Gets the KernelSize from args or sets it to 9 if no args are given.
//        /// </summary>
//        static int GetKernelSize(string[] args)
//        {
//            int kernelSize = args.Length > 0 ? int.Parse(args[0]) : 9;
//            if (kernelSize % 2 == 0)
//            {
//                Console.WriteLine("Kernel size must be odd!");
//                System.Environment.Exit(1);
//            }
//            if (kernelSize < 1 || kernelSize > 9)
//            {
//                Console.WriteLine("Kernel size must be between 1 and 9!");
//                System.Environment.Exit(1);
//            }
//            return kernelSize;
//        }

//        /// <summary>
//        /// Calculates the 1D Gaussian weights (length = kernelSize).
//        /// </summary>
//        static float[] CreateGaussianKernel(int kernelSize)
//        {
//            float[] kernel = new float[kernelSize];
//            float sum = 0f;
//            float half = (kernelSize - 1) / 2f;

//            for (int i = 0; i < kernelSize; i++)
//            {
//                float x = i - half;
//                float value = (float)Math.Exp(-(x * x) / (2f * 3.0f * 3.0f));
//                kernel[i] = value;
//                sum += value;
//            }
//            for (int i = 0; i < kernelSize; i++)
//                kernel[i] /= sum;

//            // (Optional) Print the kernel to console
//            for (int i = 0; i < kernelSize; i++)
//                Console.Write($"{kernel[i]:F6} ");
//            Console.WriteLine();

//            return kernel;
//        }

//        static string GetUniqueFilename(string filename)
//        {
//            int count = 0;
//            string candidate = filename;
//            while (File.Exists(candidate))
//            {
//                count++;
//                candidate = Path.GetFileNameWithoutExtension(filename)
//                            + $"({count})"
//                            + Path.GetExtension(filename);
//            }
//            return candidate;
//        }

//        static void Main(string[] args)
//        {
//            int kernelSize = GetKernelSize(args);
//            int channels = 4; // RGBA

//            // ─── Load image ────────────────────────────────────────────────────────────────
//            using var image = Image.Load<Rgba32>("shuttle_small.png");
//            int width = image.Width;
//            int height = image.Height;
//            int imageDataSize = width * height * channels;
//            byte[] pixelData = new byte[imageDataSize];
//            image.CopyPixelDataTo(pixelData);

//            // ─── Create 1D Gaussian kernel on host ─────────────────────────────────────────
//            float[] gausKernel1D = CreateGaussianKernel(kernelSize);

//            #region OpenCL setup

//            ErrorCode status;

//            // (1) Query platforms
//            uint numPlatforms = 0;
//            CheckStatus(Cl.GetPlatformIDs(0, null, out numPlatforms));
//            if (numPlatforms == 0)
//            {
//                Console.WriteLine("Error: No OpenCL platform available!");
//                System.Environment.Exit(1);
//            }
//            Platform[] platforms = new Platform[numPlatforms];
//            CheckStatus(Cl.GetPlatformIDs((uint)platforms.Length, platforms, out _));
//            Platform platform = platforms[0];

//            // (2) Query devices
//            uint numDevices = 0;
//            CheckStatus(Cl.GetDeviceIDs(platform, DeviceType.All, 0, null, out numDevices));
//            if (numDevices == 0)
//            {
//                Console.WriteLine("Error: No OpenCL device available for platform!");
//                System.Environment.Exit(1);
//            }
//            Device[] devices = new Device[numDevices];
//            CheckStatus(Cl.GetDeviceIDs(platform, DeviceType.All, numDevices, devices, out numDevices));
//            Device device = devices[0];

//            // (3) Create context & queue
//            Context context = Cl.CreateContext(null, 1, new[] { device }, null, IntPtr.Zero, out status);
//            CheckStatus(status);
//            CommandQueue commandQueue = Cl.CreateCommandQueue(context, device, (CommandQueueProperties)0, out status);
//            CheckStatus(status);

//            #endregion

//            // ─── Build OpenCL program ──────────────────────────────────────────────────────
//            string kernelSource = File.ReadAllText("kernel.cl");
//            OpenCL.Net.Program clProgram = Cl.CreateProgramWithSource(context, 1, new[] { kernelSource }, null, out status);
//            CheckStatus(status);

//            status = Cl.BuildProgram(clProgram, 1, new[] { device }, string.Empty, null, IntPtr.Zero);
//            if (status != ErrorCode.Success)
//            {
//                InfoBuffer buildLog = Cl.GetProgramBuildInfo(clProgram, device, ProgramBuildInfo.Log, out status);
//                Console.WriteLine("Build Error:");
//                Console.WriteLine(buildLog.ToString());
//                System.Environment.Exit(1);
//            }

//            // Create kernel instance
//            Kernel kernel = Cl.CreateKernel(clProgram, "print_id", out status);
//            CheckStatus(status);

//            // ─── Create OpenCL buffers ──────────────────────────────────────────────────────

//            // (1) Input image buffer (read-only). We'll copy pixelData into it via EnqueueWriteBuffer.
//            IMem imageBuffer = Cl.CreateBuffer(
//                context,
//                MemFlags.ReadOnly,
//                new IntPtr(imageDataSize),
//                out status);
//            CheckStatus(status);
//            CheckStatus(Cl.EnqueueWriteBuffer(
//                commandQueue,
//                imageBuffer,
//                Bool.True,
//                IntPtr.Zero,
//                new IntPtr(imageDataSize),
//                pixelData,
//                0,
//                null,
//                out _));

//            // (2) 1D Gaussian weights buffer (read-only). We copy gausKernel1D into it.
//            IMem gausKernelBuffer = Cl.CreateBuffer(
//                context,
//                MemFlags.ReadOnly,
//                new IntPtr(kernelSize * sizeof(float)),
//                out status);
//            CheckStatus(status);
//            CheckStatus(Cl.EnqueueWriteBuffer(
//                commandQueue,
//                gausKernelBuffer,
//                Bool.True,
//                IntPtr.Zero,
//                new IntPtr(kernelSize * sizeof(float)),
//                gausKernel1D,
//                0,
//                null,
//                out _));

//            // (3) Temp buffer for horizontal pass (read/write)
//            IMem tempBuffer = Cl.CreateBuffer(
//                context,
//                MemFlags.ReadWrite,
//                new IntPtr(imageDataSize),
//                out status);
//            CheckStatus(status);

//            // (4) Final output buffer (write-only)
//            IMem outputBuffer = Cl.CreateBuffer(
//                context,
//                MemFlags.WriteOnly,
//                new IntPtr(imageDataSize),
//                out status);
//            CheckStatus(status);

//            #region Set Kernel Args That Don’t Change

//            // Arg #2 = width
//            CheckStatus(Cl.SetKernelArg(kernel, 2, width));

//            // Arg #3 = height
//            CheckStatus(Cl.SetKernelArg(kernel, 3, height));

//            // Arg #4 = channels
//            CheckStatus(Cl.SetKernelArg(kernel, 4, channels));

//            // Arg #5 = kernelSize
//            CheckStatus(Cl.SetKernelArg(kernel, 5, kernelSize));

//            // Arg #7 = gausKernelBuffer (1D weights)
//            CheckStatus(Cl.SetKernelArg(kernel, 7, gausKernelBuffer));

//            #endregion

//            //
//            // ─── PASS #1: HORIZONTAL BLUR ───────────────────────────────────────────────────
//            //
//            // We bind:
//            //   0 = imageBuffer (input)
//            //   1 = tempBuffer  (output)
//            //   6 = is_horizontal = 1
//            //   8 = __local uchar *lineBuffer  → size = (width * channels) bytes
//            //

//            CheckStatus(Cl.SetKernelArg(kernel, 0, imageBuffer));
//            CheckStatus(Cl.SetKernelArg(kernel, 1, tempBuffer));
//            CheckStatus(Cl.SetKernelArg(kernel, 6, 1)); // is_horizontal = 1

//            // Allocate (width * channels) bytes of __local
//            IntPtr localBytesRow = new IntPtr(width * channels * sizeof(byte));
//            CheckStatus(Cl.SetKernelArg(kernel, 8, localBytesRow, null));

//            // Launch with local_size = (width, 1)
//            IntPtr[] globalSizeRow = new IntPtr[] { (IntPtr)width, (IntPtr)height };
//            IntPtr[] localSizeRow = new IntPtr[] { (IntPtr)width, (IntPtr)1 };
//            CheckStatus(Cl.EnqueueNDRangeKernel(
//                commandQueue,
//                kernel,
//                2,
//                null,
//                globalSizeRow,
//                localSizeRow,
//                0, null, out _));

//            //
//            // ─── PASS #2: VERTICAL BLUR ─────────────────────────────────────────────────────
//            //
//            // We bind:
//            //   0 = tempBuffer  (input)
//            //   1 = outputBuffer (output)
//            //   6 = is_horizontal = 0
//            //   8 = __local uchar *lineBuffer  → size = (height * channels) bytes
//            //

//            CheckStatus(Cl.SetKernelArg(kernel, 0, tempBuffer));
//            CheckStatus(Cl.SetKernelArg(kernel, 1, outputBuffer));
//            CheckStatus(Cl.SetKernelArg(kernel, 6, 0)); // is_horizontal = 0

//            // Allocate (height * channels) bytes of __local
//            IntPtr localBytesCol = new IntPtr(height * channels * sizeof(byte));
//            CheckStatus(Cl.SetKernelArg(kernel, 8, localBytesCol, null));

//            // Launch with local_size = (1, height)
//            IntPtr[] globalSizeCol = new IntPtr[] { (IntPtr)width, (IntPtr)height };
//            IntPtr[] localSizeCol = new IntPtr[] { (IntPtr)1, (IntPtr)height };
//            CheckStatus(Cl.EnqueueNDRangeKernel(
//                commandQueue,
//                kernel,
//                2,
//                null,
//                globalSizeCol,
//                localSizeCol,
//                0, null, out _));

//            //
//            // ─── READ BACK RESULT ───────────────────────────────────────────────────────────
//            //
//            byte[] output = new byte[imageDataSize];
//            CheckStatus(Cl.EnqueueReadBuffer(
//                commandQueue,
//                outputBuffer,
//                Bool.True,               // blocking read
//                IntPtr.Zero,
//                new IntPtr(imageDataSize),
//                output,
//                0, null, out _));

//            // Convert to Image<Rgba32> and save
//            Image<Rgba32> outputImage = Image.LoadPixelData<Rgba32>(output, width, height);
//            outputImage.Save(GetUniqueFilename("output.png"));

//            Console.WriteLine("Result saved as “" + GetUniqueFilename("output.png") + "”");
//        }
//    }
//}
