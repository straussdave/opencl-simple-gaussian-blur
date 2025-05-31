using System;
using System.IO;
using OpenCL.Net;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;


namespace Gaussian_Blur
{
    internal class Program
    {
       
        static void CheckStatus(ErrorCode err)
        {
            if (err != ErrorCode.Success)
            {
                Console.WriteLine("OpenCL Error: " + err.ToString());
                System.Environment.Exit(1);
            }
        }

        /// <summary>
        /// Gets the KernelSize from args or sets it to 3 if no args are given.
        /// </summary>
        /// <param name="args"></param>
        /// <returns>kernelSize</returns>
        static int GetKernelSize(string[] args)
        {
            int kernelSize = args.Length > 0 ? int.Parse(args[0]) : 9;
            if (kernelSize % 2 == 0)
            {
                Console.WriteLine("Kernel size must be odd!");
                System.Environment.Exit(1);
            }

            if (kernelSize > 9 || kernelSize < 1)
            {
                Console.WriteLine("Kernel size must be between 1 and 9!");
                System.Environment.Exit(1);
            }
            
            return kernelSize;
        }

        /// <summary>
        /// Calculates the Values that will be multiplied with each pixel in the kernel.
        /// </summary>
        /// <param name="kernelSize"></param>
        /// <returns>kernel as 1 dim float array</returns>
        static float[] CreateGaussianKernel(int kernelSize)
        {
            float[] kernel = new float[kernelSize];
            float sum = 0f;

            float half = (kernelSize - 1) / 2f;

            for (int i = 0; i < kernelSize; i++)
            {
                float x = i - half;
                float value = (float)Math.Exp(-(x * x) / (2f * 3.0f * 3.0f)); // nur x-Achse
                kernel[i] = value;
                sum += value;
            }

            for (int i = 0; i < kernelSize; i++)
                kernel[i] /= sum;

            for (int i = 0; i < kernelSize; i++)
                Console.Write($"{kernel[i]:F6} ");

            Console.WriteLine();

            return kernel;
        }

        static string GetUniqueFilename(string filename)
        {
            int count = 0;

            do
            {
                filename = count == 0 ? "output.png" : $"output({count}).png";
                count++;
            }
            while (File.Exists(filename));

            return filename;
        }

        static void Main(string[] args)
        {
            int kernelSize = GetKernelSize(args);


            //always creates an image with 4 channels rgba
            using var image = Image.Load<Rgba32>("shuttle_small.png");
            int width = image.Width;
            int height = image.Height;

            int imageDataSize = width * height * 4;

            byte[] pixelData = new byte[imageDataSize]; 
            image.CopyPixelDataTo(pixelData);

            float[] gausKernel = CreateGaussianKernel(kernelSize);

            #region OpenCL setup
            // used for checking error status of api calls
            ErrorCode status;

            uint numPlatforms = 0;
            CheckStatus(Cl.GetPlatformIDs(0, null, out numPlatforms));

            if (numPlatforms == 0)
            {
                Console.WriteLine("Error: No OpenCL platform available!");
                System.Environment.Exit(1);
            }

            // select the platform
            Platform[] platforms = new Platform[numPlatforms];
            CheckStatus(Cl.GetPlatformIDs(1, platforms, out numPlatforms));
            Platform platform = platforms[0];

            // retrieve the number of devices
            uint numDevices = 0;
            CheckStatus(Cl.GetDeviceIDs(platform, DeviceType.All, 0, null, out numDevices));

            if (numDevices == 0)
            {
                Console.WriteLine("Error: No OpenCL device available for platform!");
                System.Environment.Exit(1);
            }

            // select the device
            Device[] devices = new Device[numDevices];
            CheckStatus(Cl.GetDeviceIDs(platform, DeviceType.All, numDevices, devices, out numDevices));
            Device device = devices[0];

            // create context
            Context context = Cl.CreateContext(null, 1, new Device[] { device }, null, IntPtr.Zero, out status);
            CheckStatus(status);

            // create command queue
            CommandQueue commandQueue = Cl.CreateCommandQueue(context, device, 0, out status);
            CheckStatus(status);

            #endregion

            string programSource = File.ReadAllText("kernel.cl");
            OpenCL.Net.Program program = Cl.CreateProgramWithSource(context, 1, new string[] { programSource }, null, out status);
            CheckStatus(status);

            // build the program
            status = Cl.BuildProgram(program, 1, new Device[] { device }, "", null, IntPtr.Zero);
            if (status != ErrorCode.Success)
            {
                InfoBuffer infoBuffer = Cl.GetProgramBuildInfo(program, device, ProgramBuildInfo.Log, out status);
                CheckStatus(status);
                Console.WriteLine("Build Error: " + infoBuffer.ToString());
                System.Environment.Exit(1);
            }

            // create Kernel
            OpenCL.Net.Kernel kernel = Cl.CreateKernel(program, "print_id", out status);
            CheckStatus(status);

            //create buffers with the size of the image
            IMem<byte> imageBuffer = Cl.CreateBuffer<byte>(context, MemFlags.ReadOnly, imageDataSize, out status);
            CheckStatus(status);
            IMem<float> gausKernelBuffer = Cl.CreateBuffer<float>(context, MemFlags.ReadOnly, gausKernel.Length * sizeof(float), out status);
            CheckStatus(status);
            IMem<byte> tempBuffer = Cl.CreateBuffer<byte>(context, MemFlags.ReadWrite, imageDataSize, out status);
            CheckStatus(status);
            IMem<byte> outputBuffer = Cl.CreateBuffer<byte>(context, MemFlags.WriteOnly, imageDataSize, out status);
            CheckStatus(status);

            // write data image data to the buffer
            CheckStatus(Cl.EnqueueWriteBuffer(commandQueue, imageBuffer, Bool.True, IntPtr.Zero, new IntPtr(imageDataSize), pixelData, 0, null, out var _));
            //write GaussKernel to buffer
            CheckStatus(Cl.EnqueueWriteBuffer(commandQueue, gausKernelBuffer, Bool.True, IntPtr.Zero, new IntPtr(gausKernel.Length * sizeof(float)), gausKernel, 0, null, out var _));

            // set the kernel arguments for horizontal run
            CheckStatus(Cl.SetKernelArg(kernel, 0, imageBuffer));
            CheckStatus(Cl.SetKernelArg(kernel, 1, outputBuffer));
            CheckStatus(Cl.SetKernelArg(kernel, 2, width));
            CheckStatus(Cl.SetKernelArg(kernel, 3, height));
            CheckStatus(Cl.SetKernelArg(kernel, 4, 4));//channels
            CheckStatus(Cl.SetKernelArg(kernel, 5, kernelSize));
            CheckStatus(Cl.SetKernelArg(kernel, 6, 1)); //isHorizontal
            CheckStatus(Cl.SetKernelArg(kernel, 7, gausKernelBuffer));
            CheckStatus(Cl.SetKernelArg(kernel, 8, null));

            byte[] output = new byte[imageDataSize];
            CheckStatus(Cl.EnqueueNDRangeKernel(commandQueue, kernel, 2, null, new IntPtr[] {(IntPtr)width, (IntPtr)height }, null, 0, null, out var _));

            //// write data image data to the buffer
            //CheckStatus(Cl.EnqueueWriteBuffer(commandQueue, tempBuffer, Bool.True, IntPtr.Zero, new IntPtr(imageDataSize), pixelData, 0, null, out var _));
            ////write GaussKernel to buffer
            //CheckStatus(Cl.EnqueueWriteBuffer(commandQueue, gausKernelBuffer, Bool.True, IntPtr.Zero, new IntPtr(gausKernel.Length * sizeof(float)), gausKernel, 0, null, out var _));

            //// set the kernel arguments for vertical run
            //CheckStatus(Cl.SetKernelArg(kernel, 0, tempBuffer));
            //CheckStatus(Cl.SetKernelArg(kernel, 1, outputBuffer));
            //CheckStatus(Cl.SetKernelArg(kernel, 2, width));
            //CheckStatus(Cl.SetKernelArg(kernel, 3, height));
            //CheckStatus(Cl.SetKernelArg(kernel, 4, 4));//channels
            //CheckStatus(Cl.SetKernelArg(kernel, 5, kernelSize));
            //CheckStatus(Cl.SetKernelArg(kernel, 6, 0)); //isHorizontal
            //CheckStatus(Cl.SetKernelArg(kernel, 7, gausKernelBuffer));
            //CheckStatus(Cl.SetKernelArg(kernel, 8, null));

            //CheckStatus(Cl.EnqueueNDRangeKernel(commandQueue, kernel, 2, null, new IntPtr[] { (IntPtr)width, (IntPtr)height }, null, 0, null, out var _));
            CheckStatus(Cl.EnqueueReadBuffer(commandQueue, outputBuffer, Bool.True, IntPtr.Zero, new IntPtr(imageDataSize), output, 0, null, out var _));

            // Bild aus byte[] erzeugen
            Image<Rgba32> outputImage = Image.LoadPixelData<Rgba32>(output, width, height);

            // Als PNG speichern
            outputImage.Save(GetUniqueFilename("output"));

            //CreateImageFromByteArray(output, width, height, 4).Save("output.png");
            Console.WriteLine("Bild wurde gespeichert als output.png");
        }
    }
}
