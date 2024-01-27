
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using static brooen_export2.tex_process;
using static System.Net.Mime.MediaTypeNames;

namespace brooen_export2{
    internal class Program{


        public struct dat_texture{ // sizeof = 20 bytes (0x14)
            public int width;
            public int height;
            public uint image_count;
            public ushort unk1;
            public byte unk2;
            public byte format;
            public int data_length;
        };

        static void Main(string[] args)
        {
            // get filepath & open
            string path = "C:\\Users\\Joe bingle\\Downloads\\brooen_stuff\\test4\\Banshee.dat";
            string output = "C:\\Users\\Joe bingle\\Downloads\\brooen_stuff\\test4\\Banshee_converted.png";



            dat_texture header = new();
            byte[] texture_data;
            using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open)))
            {
                // read & convert header
                header.width = BitConverter.ToInt32(read(reader, 4).Reverse().ToArray(), 0);
                header.height = BitConverter.ToInt32(read(reader, 4).Reverse().ToArray(), 0);
                header.image_count = BitConverter.ToUInt32(read(reader, 4).Reverse().ToArray(), 0);
                header.unk1 = BitConverter.ToUInt16(read(reader, 2).Reverse().ToArray(), 0);
                header.unk2 = read(reader, 1)[0];
                header.format = read(reader, 1)[0];
                header.data_length = BitConverter.ToInt32(read(reader, 4).Reverse().ToArray(), 0);

                // get all file bytes after that
                texture_data = read(reader, header.data_length);
            }

            // maybe flip all values for endianness?
            //flip_bytes(4, texture_data, texture_data.Length);


            // begin conversion
            tex_format format = (tex_format)header.format;
            texture_data = Detile(texture_data, header.width, header.height, format);
            byte[] rgba_data = DecodeTexture(texture_data, format, header.width, header.height);

            // save to file
            var pixelBufferHandle = GCHandle.Alloc(rgba_data, GCHandleType.Pinned);
            var bitmap = new Bitmap(
                header.width,
                header.height,
                header.width * 4,
                PixelFormat.Format32bppPArgb,
                pixelBufferHandle.AddrOfPinnedObject()
            );

            //Bitmap bmp = new Bitmap(header.width, header.height, rgba_data.Length, PixelFormat.Format32bppArgb,GCHandle.Alloc(rgba_data, GCHandleType.Pinned).AddrOfPinnedObject());
            bitmap.Save(output, ImageFormat.Png);
            pixelBufferHandle.Free();
        }
        public static byte[] read(BinaryReader reader, int size){
            byte[] bytes = new byte[size];
            reader.Read(bytes, 0, size);
            return bytes;
        }
        
        static void flip_bytes(int int_size, byte[] source, int byte_count) {
            if (int_size % 2 == 1) throw new Exception("cant flip odd int size");

            for (int i = 0; i < byte_count; i += int_size) {
                if (byte_count < i + (int_size-1)) throw new Exception("cant flip final bytes");

                for (int int_byte_index = 0; int_byte_index < int_size/2; int_byte_index++) {
                    int opposite_index = int_size - (int_byte_index+1);
                    byte opposite = source[i + opposite_index];
                    // then swap
                    source[i + opposite_index] = source[i + int_byte_index];
                    source[i + int_byte_index] = opposite;
            }}
        }
    }
}