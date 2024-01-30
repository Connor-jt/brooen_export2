
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using static brooen_export2.tex_process;
using static System.Net.Mime.MediaTypeNames;

namespace brooen_export2{
    internal class Program{
        /*

        
 6688 (0x1A20) means default
 6184 (0x1828) means UI
10240 (0x2800) means map texture

0x7 = some kind of array things (12-16 images per thing)
0xA = mostly half mips
0xB = vertical mips?? 





         */

        public struct dat_texture{ // sizeof = 20 bytes (0x14)
            public int width;
            public int height;
            public uint mode;
            public ushort unk1;
            public byte unk2;
            public tex_format format;
            public int data_length;
        };

        static void Main(string[] args){
            // get filepath & open
            //string source = "C:\\Users\\Joe bingle\\Downloads\\extracted\\extracted\\AkTextureAsset";
            Console.WriteLine("enter directory to recursively convert dat textures from");
            string? source = Console.ReadLine();
            if (source == null ){
                Console.WriteLine("No Input, press enter to exit");
                Console.ReadLine();
                return;}
            if (!Directory.Exists(source) ){
                Console.WriteLine("bad directory (doesn't exist), press enter to exit");
                Console.ReadLine();
                return;}
            
            foreach (string file in Directory.EnumerateFiles(source, "*.dat", SearchOption.AllDirectories)){
                try{dump_texture(file);
                    Console.WriteLine("SUCCESS: @ " + file);
                }catch (Exception ex){
                    Console.WriteLine("FAILURE: " + ex.Message + " @ " + file);
            }}
            foreach (string file in Directory.EnumerateFiles(source, "*.dfont", SearchOption.AllDirectories)){
                try{dump_texture(file);
                    Console.WriteLine("SUCCESS: @ " + file);
                }catch (Exception ex){
                    Console.WriteLine("FAILURE: " + ex.Message + " @ " + file);
            }}
            Console.WriteLine("task conpleted, press enter to exit");
            Console.ReadLine();
        }

        //public struct test{
        //    public test(double _ratio, dat_texture tex) { ratio = _ratio; source_img = tex; }
        //    public double ratio;
        //    public dat_texture source_img;
        //}
        ////public static Dictionary<uint, List<test>> image_data_ratio = new();
        //public static Dictionary<uint, List<double>> image_data_ratio = new();

        public static void dump_texture(string source_path){ 
            dat_texture header = new();
            int first_image_size;
            byte[] texture_data;
            using (BinaryReader reader = new BinaryReader(new FileStream(source_path, FileMode.Open))){
                // read & convert header
                header.width = BitConverter.ToInt32(read_reverse(reader, 4), 0);
                header.height = BitConverter.ToInt32(read_reverse(reader, 4), 0);
                header.mode = BitConverter.ToUInt32(read_reverse(reader, 4), 0);
                header.unk1 = BitConverter.ToUInt16(read_reverse(reader, 2), 0);
                header.unk2 = read(reader, 1)[0];
                header.format = (tex_format)read(reader, 1)[0];
                header.data_length = BitConverter.ToInt32(read_reverse(reader, 4), 0);

                // verify header information
                if (header.width <= 0 || header.height <= 0 || header.data_length <= 0)
                    throw new Exception("Invalid header data or texture");
                if (!IsFormatSupported(header.format))
                    throw new Exception("unsupported header type: " + header.format.ToString());

                first_image_size = GetTextureDataSize(header.width, header.height, header.format);
                if (header.data_length < first_image_size)
                    throw new Exception("file too small to contain pixel data");

                // DEBUG //
                //if (header.data_length != first_image_size)
                //    throw new Exception("excess file bytes detected (" + (header.data_length - first_image_size) + " extra bytes) ");

                //if ((header.unk1 != 6688 && header.unk1 != 6184 && header.unk1 != 10240) || header.unk2 != 1)
                //    throw new Exception("Debug");
                //if (source_path == "C:\\Users\\Joe bingle\\Downloads\\extracted\\extracted\\AkTextureAsset\\Powerups\\Boost_blue.dat")
                //    throw new Exception("DEbug");
                //if (header.mode != 0x0b)
                //    throw new Exception("DEbug");

                //if (!image_data_ratio.ContainsKey(header.mode))
                //    image_data_ratio[header.mode] = new();
                //image_data_ratio[header.mode].Add(new test((double)header.data_length / first_image_size, header));
                //image_data_ratio[header.mode].Add((double)header.data_length / first_image_size);

                // get all file bytes after that
                //throw new Exception("read cancelled");
                texture_data = read(reader, header.data_length);
            }


            // b = height
            // a = width

            if (header.mode == 0x0b || header.mode == 0x0a){
                int current_read_data = 0;
                while (true){
                    int next_image_size = GetTextureDataSize(header.width, header.height, header.format);
                    // check if theres enough data left for this size
                    if (header.data_length - current_read_data >= next_image_size){

                        string output_name;
                        if (header.mode == 0x0b) output_name = source_path + header.height.ToString() + ".png";
                        else output_name = source_path + header.width.ToString() + ".png";

                        // then attempt to write to file
                        byte[] curr_data = texture_data.Skip(current_read_data).Take(next_image_size).ToArray();
                        write_pixels_to_file(curr_data, header, output_name);
                        current_read_data += next_image_size;
                        return; // we dont want mip maps, so abort after extracting the first image

                    }else{ // shrink search thing
                        if (header.mode == 0x0b) header.height /= 2;
                        else header.width /= 2;
                    }

                    if (header.width < 16 || header.height < 16 || header.data_length - current_read_data == 0)
                        break;
                }


                return;
            }

            //throw new Exception("read cancelled"); // only testing the weird ones atm

            int images_count = header.data_length / first_image_size;
            List<string> fail_messages = new();
            for (int i = 0; i < images_count; i++){

                // reduce image resolution procedurally until we get all the bytes


                // get the current slice of data
                byte[] curr_data = texture_data.Skip(first_image_size*i).Take(first_image_size).ToArray();

                string output_name;
                if (images_count == 1) output_name = source_path + ".png";
                else output_name = source_path + i.ToString() + ".png";

                // then attempt to write to file
                try{write_pixels_to_file(curr_data, header, output_name);
                }catch (Exception e){fail_messages.Add(e.Message);}
            }

            if (fail_messages.Count > 0){
                if (images_count == 1) throw new Exception(fail_messages[0]); // throw regular error if there was only 1 image to extract
                // else combine all errrors, and count how many failed
                else throw new Exception(fail_messages.Count.ToString() + "/" + images_count.ToString() +" images failed: " + string.Join(", ", fail_messages));
            }
        }
        public static void write_pixels_to_file(byte[] data, dat_texture header, string out_path){
            // begin conversion
            data = Detile(data, header.width, header.height, header.format);
            byte[] rgba_data = DecodeTexture(data, header.format, header.width, header.height);

            // save to file
            var pixelBufferHandle = GCHandle.Alloc(rgba_data, GCHandleType.Pinned);
            var bitmap = new Bitmap(header.width,header.height,header.width*4,PixelFormat.Format32bppPArgb,pixelBufferHandle.AddrOfPinnedObject());

            //Bitmap bmp = new Bitmap(header.width, header.height, rgba_data.Length, PixelFormat.Format32bppArgb,GCHandle.Alloc(rgba_data, GCHandleType.Pinned).AddrOfPinnedObject());
            bitmap.Save(out_path, ImageFormat.Png);
            pixelBufferHandle.Free();
        }
        public static byte[] read_reverse(BinaryReader reader, int size) => read(reader, size).Reverse().ToArray();
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