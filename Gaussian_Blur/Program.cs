using System;
using System.IO;
using OpenCL.Net;
using SixLabors.ImageSharp;
using FreeImageAPI;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace Gaussian_Blur
{
    internal class Program
    {
        struct TGAHeader
        {
            public byte idLength;
            public byte colorMapType;
            public byte imageType;
            public ushort colorMapFirstEntryIndex;
            public ushort colorMapLength;
            public byte colorMapEntrySize;
            public ushort xOrigin;
            public ushort yOrigin;
            public ushort width;
            public ushort height;
            public byte pixelDepth;
            public byte imageDescriptor;
        }


        static byte[] LoadTGA(string filename, out int width, out int height, out int channels)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                TGAHeader header = new TGAHeader
                {
                    idLength = reader.ReadByte(),
                    colorMapType = reader.ReadByte(),
                    imageType = reader.ReadByte(),
                    colorMapFirstEntryIndex = reader.ReadUInt16(),
                    colorMapLength = reader.ReadUInt16(),
                    colorMapEntrySize = reader.ReadByte(),
                    xOrigin = reader.ReadUInt16(),
                    yOrigin = reader.ReadUInt16(),
                    width = reader.ReadUInt16(),
                    height = reader.ReadUInt16(),
                    pixelDepth = reader.ReadByte(),
                    imageDescriptor = reader.ReadByte()
                };

                if (header.imageType != 2)
                    throw new Exception("Only uncompressed RGB(A) TGA is supported.");

                width = header.width;
                height = header.height;
                channels = header.pixelDepth / 8;

                if (channels != 3 && channels != 4)
                    throw new Exception("Only 24-bit or 32-bit TGA is supported.");

                // Skip ID field if present
                if (header.idLength > 0)
                    reader.BaseStream.Seek(header.idLength, SeekOrigin.Current);

                int imageSize = width * height * channels;
                byte[] data = reader.ReadBytes(imageSize);

                // Convert BGR(A) to RGB(A)
                for (int i = 0; i < imageSize; i += channels)
                {
                    byte temp = data[i];
                    data[i] = data[i + 2];
                    data[i + 2] = temp;
                }

                return data;
            }
        }



        static Image<Rgba32> CreateImageFromByteArray(byte[] data, int width, int height)
        {
            Image<Rgba32> image = new Image<Rgba32>(width, height);

            int index = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte r = data[index++];
                    byte g = data[index++];
                    byte b = data[index++];
                    byte a = data[index++]; // Falls dein Kernel Alpha beibehält oder generiert

                    image[x, y] = new Rgba32(r, g, b, a);
                }
            }

            return image;
        }




        static void CheckStatus(ErrorCode err)
        {
            if (err != ErrorCode.Success)
            {
                Console.WriteLine("OpenCL Error: " + err.ToString());
                System.Environment.Exit(1);
            }
        }
        static void Main(string[] args)
        {
            //get image
            string imagePath = "shuttle.tga";
            int height, width;
            var pixelData = LoadTGA(imagePath, out width, out height, out int channels);

            // input and output arrays  
            int elementSize = width * height;
            int dataSize = elementSize * sizeof(int);

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

            // allocate two input and one output buffer for the three vectors
            IMem<int> imageBuffer = Cl.CreateBuffer<int>(context, MemFlags.ReadOnly, dataSize, out status);
            CheckStatus(status);
            IMem<int> outputBuffer = Cl.CreateBuffer<int>(context, MemFlags.WriteOnly, dataSize, out status); ;
            CheckStatus(status);

            // write data from the input vectors to the buffers
            CheckStatus(Cl.EnqueueWriteBuffer(commandQueue, imageBuffer, Bool.True, IntPtr.Zero, new IntPtr(dataSize), pixelData, 0, null, out var _));


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

            // create the vector addition kernel
            OpenCL.Net.Kernel kernel = Cl.CreateKernel(program, "print_id", out status);
            CheckStatus(status);

            // set the kernel arguments
            CheckStatus(Cl.SetKernelArg(kernel, 0, imageBuffer));
            CheckStatus(Cl.SetKernelArg(kernel, 1, outputBuffer));


            byte[] output = new byte[dataSize];
            CheckStatus(Cl.EnqueueNDRangeKernel(commandQueue, kernel, 2, null, new IntPtr[] {(IntPtr)width, (IntPtr)height }, null, 0, null, out var _));
            CheckStatus(Cl.EnqueueReadBuffer(commandQueue, outputBuffer, Bool.True, IntPtr.Zero, new IntPtr(dataSize), output, 0, null, out var _));

            CreateImageFromByteArray(output, width, height).Save("output.png");
            Console.WriteLine("Bild wurde gespeichert als output.png");

        }
    }
}
