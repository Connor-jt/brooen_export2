using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace brooen_export2{
    class tex_process {
        public enum tex_format : byte {
            TEXTURE_FORMAT_L8 = 0x02,
            TEXTURE_FORMAT_R5G6B5 = 0x44,
            TEXTURE_FORMAT_A8L8 = 0x4a,
            TEXTURE_FORMAT_X4R4G4B4 = 0x4f,
            TEXTURE_FORMAT_DXT1 = 0x52,
            TEXTURE_FORMAT_DXT3 = 0x53,
            TEXTURE_FORMAT_DXT5 = 0x54,
            TEXTURE_FORMAT_DXN = 0x71,
            TEXTURE_FORMAT_A8R8G8B8 = 0x86
        }



        public static bool IsFormatSupported(tex_format format){
            switch (format){
                case tex_format.TEXTURE_FORMAT_A8L8:
                case tex_format.TEXTURE_FORMAT_X4R4G4B4:
                case tex_format.TEXTURE_FORMAT_R5G6B5:
                case tex_format.TEXTURE_FORMAT_A8R8G8B8:
                case tex_format.TEXTURE_FORMAT_L8:
                case tex_format.TEXTURE_FORMAT_DXT1:
                case tex_format.TEXTURE_FORMAT_DXN:
                case tex_format.TEXTURE_FORMAT_DXT3:
                case tex_format.TEXTURE_FORMAT_DXT5:
                    return true;
                default: return false;
        }}
        public static int TexelPartsSqrt(tex_format format){ // result * result to get pixels per chunk
            switch (format){
                case tex_format.TEXTURE_FORMAT_A8L8:
                case tex_format.TEXTURE_FORMAT_X4R4G4B4:
                case tex_format.TEXTURE_FORMAT_R5G6B5:
                case tex_format.TEXTURE_FORMAT_A8R8G8B8:
                case tex_format.TEXTURE_FORMAT_L8:
                    return 1;
                case tex_format.TEXTURE_FORMAT_DXT1:
                case tex_format.TEXTURE_FORMAT_DXN:
                case tex_format.TEXTURE_FORMAT_DXT3:
                case tex_format.TEXTURE_FORMAT_DXT5:
                    return 4;
                default: throw new Exception("unsupported type");
        }}
        public static int TexelByteLength(tex_format format){ // amount of bytes per texel
            switch (format){
                case tex_format.TEXTURE_FORMAT_L8:
                    return 1;
                case tex_format.TEXTURE_FORMAT_A8L8:
                case tex_format.TEXTURE_FORMAT_X4R4G4B4:
                case tex_format.TEXTURE_FORMAT_R5G6B5:
                    return 2;
                case tex_format.TEXTURE_FORMAT_A8R8G8B8:
                    return 4;
                case tex_format.TEXTURE_FORMAT_DXT1:
                    return 8;
                case tex_format.TEXTURE_FORMAT_DXN:
                case tex_format.TEXTURE_FORMAT_DXT3:
                case tex_format.TEXTURE_FORMAT_DXT5:
                    return 16;
                default: throw new Exception("unsupported type");
        }}
        public static double BytesPerPixel(tex_format format){
            int parts = TexelPartsSqrt(format); parts *= parts;
            return (double)TexelByteLength(format) / parts;
        }
        public static int GetTextureDataSize(int width, int height, tex_format format) 
            => (int)Math.Round(width * height * BytesPerPixel(format)); // rounding just to be safe
        



        public static byte[] Detile(byte[] data, int width, int height, tex_format format){
            byte[] destData = new byte[data.Length];

            int parts = TexelPartsSqrt(format); // the x of x*x to get the pixel count
            int texel_bytes = TexelByteLength(format); // byte size of chunk

            int blockWidth = width / parts;
            int blockHeight = height / parts;

            for (int j = 0; j < blockHeight; j++){
                for (int i = 0; i < blockWidth; i++){
                    int blockOffset = j * blockWidth + i;

                    int x = XGAddress2DTiledX(blockOffset, blockWidth, texel_bytes);
                    int y = XGAddress2DTiledY(blockOffset, blockWidth, texel_bytes);

                    int srcOffset = j * blockWidth * texel_bytes + i * texel_bytes;
                    int destOffset = y * blockWidth * texel_bytes + x * texel_bytes;
                    //TODO: ConvertToLinearTexture apparently breaks on on textures with a height of 64... // Epic
                    if (destOffset >= destData.Length) continue;
                    Array.Copy(data, srcOffset, destData, destOffset, texel_bytes);
                }
            }

            return destData;
        }

        private static int XGAddress2DTiledX(int Offset, int Width, int TexelPitch){
            int AlignedWidth = (Width + 31) & ~31;

            int LogBpp = (TexelPitch >> 2) + ((TexelPitch >> 1) >> (TexelPitch >> 2));
            int OffsetB = Offset << LogBpp;
            int OffsetT = ((OffsetB & ~4095) >> 3) + ((OffsetB & 1792) >> 2) + (OffsetB & 63);
            int OffsetM = OffsetT >> (7 + LogBpp);

            int MacroX = ((OffsetM % (AlignedWidth >> 5)) << 2);
            int Tile = ((((OffsetT >> (5 + LogBpp)) & 2) + (OffsetB >> 6)) & 3);
            int Macro = (MacroX + Tile) << 3;
            int Micro = ((((OffsetT >> 1) & ~15) + (OffsetT & 15)) & ((TexelPitch << 3) - 1)) >> LogBpp;

            return Macro + Micro;
        }

        private static int XGAddress2DTiledY(int Offset, int Width, int TexelPitch){
            int AlignedWidth = (Width + 31) & ~31;

            int LogBpp = (TexelPitch >> 2) + ((TexelPitch >> 1) >> (TexelPitch >> 2));
            int OffsetB = Offset << LogBpp;
            int OffsetT = ((OffsetB & ~4095) >> 3) + ((OffsetB & 1792) >> 2) + (OffsetB & 63);
            int OffsetM = OffsetT >> (7 + LogBpp);

            int MacroY = ((OffsetM / (AlignedWidth >> 5)) << 2);
            int Tile = ((OffsetT >> (6 + LogBpp)) & 1) + (((OffsetB & 2048) >> 10));
            int Macro = (MacroY + Tile) << 3;
            int Micro = ((((OffsetT & (((TexelPitch << 6) - 1) & ~31)) + ((OffsetT & 15) << 1)) >> (3 + LogBpp)) & ~1);

            return Macro + Micro + ((OffsetT & 16) >> 4);
        }


        public static byte[] DecodeTexture(byte[] _textureData, tex_format format, int _width, int _height){
            switch (format){
                case tex_format.TEXTURE_FORMAT_DXT1:
                    return DecodeDXT1(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_DXT3:
                    return DecodeDXT3(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_DXT5:
                    return DecodeDXT5(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_DXN:
                    return DecodeDXN(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_A8L8:
                    return DecodeA8L8(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_A8R8G8B8:
                    return DecodeA8R8G8B8(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_L8:
                    return DecodeL8(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_X4R4G4B4:
                    return DecodeX4R4G4B4(_textureData, _width, _height);
                case tex_format.TEXTURE_FORMAT_R5G6B5:
                    return DecodeR5G6B5(_textureData, _width, _height);
                default:
                    return _textureData;
            }
        }

        public static byte[] DecodeX4R4G4B4(byte[] data, int width, int height){
            var pixData = new byte[(width * height) * 4];
            for (int i = 0; i < (width * height); i++){
                int srcOffset = i * 2;
                int destOffset = i * 4;
                byte b = (byte)(data[srcOffset] & 0xF);
                byte x = (byte)((data[srcOffset] >> 4) & 0xf);
                byte r = (byte)(data[srcOffset + 1] & 0xF);
                byte g = (byte)((data[srcOffset + 1] >> 4) & 0xf);
                pixData[destOffset + 0] = (byte)((r << 4 | r) & 0xFF);
                pixData[destOffset + 1] = (byte)((g << 4 | g) & 0xFF);
                pixData[destOffset + 2] = (byte)((b << 4 | b) & 0xFF);
                pixData[destOffset + 3] = 0xff; // WARNING: val x is disregarded???
            }
            return pixData;
        }


        public static byte[] DecodeDXT1(byte[] data, int width, int height)
        {
            byte[] pixData = new byte[width * height * 4];
            int xBlocks = width / 4;
            int yBlocks = height / 4;
            for (int y = 0; y < yBlocks; y++)
            {
                for (int x = 0; x < xBlocks; x++)
                {
                    int blockDataStart = ((y * xBlocks) + x) * 8;

                    uint color0 = ((uint)data[blockDataStart + 0] << 8) + data[blockDataStart + 1];
                    uint color1 = ((uint)data[blockDataStart + 2] << 8) + data[blockDataStart + 3];

                    uint code = BitConverter.ToUInt32(data, blockDataStart + 4);

                    ushort r0 = 0, g0 = 0, b0 = 0, r1 = 0, g1 = 0, b1 = 0;
                    r0 = (ushort)(8 * (color0 & 31));
                    g0 = (ushort)(4 * ((color0 >> 5) & 63));
                    b0 = (ushort)(8 * ((color0 >> 11) & 31));

                    r1 = (ushort)(8 * (color1 & 31));
                    g1 = (ushort)(4 * ((color1 >> 5) & 63));
                    b1 = (ushort)(8 * ((color1 >> 11) & 31));

                    for (int k = 0; k < 4; k++)
                    {
                        int j = k ^ 1;

                        for (int i = 0; i < 4; i++)
                        {
                            int pixDataStart = (width * (y * 4 + j) * 4) + ((x * 4 + i) * 4);
                            uint codeDec = code & 0x3;

                            switch (codeDec)
                            {
                                case 0:
                                    pixData[pixDataStart + 0] = (byte)r0;
                                    pixData[pixDataStart + 1] = (byte)g0;
                                    pixData[pixDataStart + 2] = (byte)b0;
                                    pixData[pixDataStart + 3] = 255;
                                    break;
                                case 1:
                                    pixData[pixDataStart + 0] = (byte)r1;
                                    pixData[pixDataStart + 1] = (byte)g1;
                                    pixData[pixDataStart + 2] = (byte)b1;
                                    pixData[pixDataStart + 3] = 255;
                                    break;
                                case 2:
                                    pixData[pixDataStart + 3] = 255;
                                    if (color0 > color1)
                                    {
                                        pixData[pixDataStart + 0] = (byte)((2 * r0 + r1) / 3);
                                        pixData[pixDataStart + 1] = (byte)((2 * g0 + g1) / 3);
                                        pixData[pixDataStart + 2] = (byte)((2 * b0 + b1) / 3);
                                    }
                                    else
                                    {
                                        pixData[pixDataStart + 0] = (byte)((r0 + r1) / 2);
                                        pixData[pixDataStart + 1] = (byte)((g0 + g1) / 2);
                                        pixData[pixDataStart + 2] = (byte)((b0 + b1) / 2);
                                    }
                                    break;
                                case 3:
                                    if (color0 > color1)
                                    {
                                        pixData[pixDataStart + 0] = (byte)((r0 + 2 * r1) / 3);
                                        pixData[pixDataStart + 1] = (byte)((g0 + 2 * g1) / 3);
                                        pixData[pixDataStart + 2] = (byte)((b0 + 2 * b1) / 3);
                                        pixData[pixDataStart + 3] = 255;
                                    }
                                    else
                                    {
                                        pixData[pixDataStart + 0] = 0;
                                        pixData[pixDataStart + 1] = 0;
                                        pixData[pixDataStart + 2] = 0;
                                        pixData[pixDataStart + 3] = 0;
                                    }
                                    break;
                            }

                            code >>= 2;
                        }
                    }


                }
            }
            return pixData;
        }

        public static byte[] DecodeDXT3(byte[] data, int width, int height)
        {
            byte[] pixData = new byte[width * height * 4];
            int xBlocks = width / 4;
            int yBlocks = height / 4;
            for (int y = 0; y < yBlocks; y++)
            {
                for (int x = 0; x < xBlocks; x++)
                {
                    int blockDataStart = ((y * xBlocks) + x) * 16;
                    ushort[] alphaData = new ushort[4];

                    alphaData[0] = (ushort)((data[blockDataStart + 0] << 8) + data[blockDataStart + 1]);
                    alphaData[1] = (ushort)((data[blockDataStart + 2] << 8) + data[blockDataStart + 3]);
                    alphaData[2] = (ushort)((data[blockDataStart + 4] << 8) + data[blockDataStart + 5]);
                    alphaData[3] = (ushort)((data[blockDataStart + 6] << 8) + data[blockDataStart + 7]);

                    byte[,] alpha = new byte[4, 4];
                    for (int j = 0; j < 4; j++)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            alpha[i, j] = (byte)((alphaData[j] & 0xF) * 16);
                            alphaData[j] >>= 4;
                        }
                    }

                    ushort color0 = (ushort)((data[blockDataStart + 8] << 8) + data[blockDataStart + 9]);
                    ushort color1 = (ushort)((data[blockDataStart + 10] << 8) + data[blockDataStart + 11]);

                    uint code = BitConverter.ToUInt32(data, blockDataStart + 8 + 4);

                    ushort r0 = 0, g0 = 0, b0 = 0, r1 = 0, g1 = 0, b1 = 0;
                    r0 = (ushort)(8 * (color0 & 31));
                    g0 = (ushort)(4 * ((color0 >> 5) & 63));
                    b0 = (ushort)(8 * ((color0 >> 11) & 31));

                    r1 = (ushort)(8 * (color1 & 31));
                    g1 = (ushort)(4 * ((color1 >> 5) & 63));
                    b1 = (ushort)(8 * ((color1 >> 11) & 31));

                    for (int k = 0; k < 4; k++)
                    {
                        int j = k ^ 1;

                        for (int i = 0; i < 4; i++)
                        {
                            int pixDataStart = (width * (y * 4 + j) * 4) + ((x * 4 + i) * 4);
                            uint codeDec = code & 0x3;

                            pixData[pixDataStart + 3] = alpha[i, j];

                            switch (codeDec)
                            {
                                case 0:
                                    pixData[pixDataStart + 0] = (byte)r0;
                                    pixData[pixDataStart + 1] = (byte)g0;
                                    pixData[pixDataStart + 2] = (byte)b0;
                                    break;
                                case 1:
                                    pixData[pixDataStart + 0] = (byte)r1;
                                    pixData[pixDataStart + 1] = (byte)g1;
                                    pixData[pixDataStart + 2] = (byte)b1;
                                    break;
                                case 2:
                                    if (color0 > color1)
                                    {
                                        pixData[pixDataStart + 0] = (byte)((2 * r0 + r1) / 3);
                                        pixData[pixDataStart + 1] = (byte)((2 * g0 + g1) / 3);
                                        pixData[pixDataStart + 2] = (byte)((2 * b0 + b1) / 3);
                                    }
                                    else
                                    {
                                        pixData[pixDataStart + 0] = (byte)((r0 + r1) / 2);
                                        pixData[pixDataStart + 1] = (byte)((g0 + g1) / 2);
                                        pixData[pixDataStart + 2] = (byte)((b0 + b1) / 2);
                                    }
                                    break;
                                case 3:
                                    if (color0 > color1)
                                    {
                                        pixData[pixDataStart + 0] = (byte)((r0 + 2 * r1) / 3);
                                        pixData[pixDataStart + 1] = (byte)((g0 + 2 * g1) / 3);
                                        pixData[pixDataStart + 2] = (byte)((b0 + 2 * b1) / 3);
                                    }
                                    else
                                    {
                                        pixData[pixDataStart + 0] = 0;
                                        pixData[pixDataStart + 1] = 0;
                                        pixData[pixDataStart + 2] = 0;
                                    }
                                    break;
                            }

                            code >>= 2;
                        }
                    }


                }
            }
            return pixData;
        }

        public static ulong ReadDXNBlockBits(byte[] data, int blockStart)
        {
            ulong blockBits = 0;

            blockBits |= data[blockStart + 6];
            blockBits <<= 8;
            blockBits |= data[blockStart + 7];
            blockBits <<= 8;
            blockBits |= data[blockStart + 4];
            blockBits <<= 8;
            blockBits |= data[blockStart + 5];
            blockBits <<= 8;
            blockBits |= data[blockStart + 2];
            blockBits <<= 8;
            blockBits |= data[blockStart + 3];

            return blockBits;
        }

        public static byte[] DecodeDXN(byte[] data, int width, int height)
        {
            byte[] pixData = new byte[width * height * 4];

            int xBlocks = width / 4;
            int yBlocks = height / 4;
            for (int y = 0; y < yBlocks; y++)
            {
                for (int x = 0; x < xBlocks; x++)
                {
                    int blockStart = ((y * xBlocks) + x) * 16;
                    byte[] red = new byte[8];
                    red[1] = data[blockStart];
                    red[0] = data[blockStart + 1];
                    if (red[0] > red[1])
                    {
                        red[2] = (byte)((6 * red[0] + 1 * red[1]) / 7);
                        red[3] = (byte)((5 * red[0] + 2 * red[1]) / 7);
                        red[4] = (byte)((4 * red[0] + 3 * red[1]) / 7);
                        red[5] = (byte)((3 * red[0] + 4 * red[1]) / 7);
                        red[6] = (byte)((2 * red[0] + 5 * red[1]) / 7);
                        red[7] = (byte)((1 * red[0] + 6 * red[1]) / 7);
                    }
                    else
                    {
                        red[2] = (byte)((4 * red[0] + 1 * red[1]) / 5);
                        red[3] = (byte)((3 * red[0] + 2 * red[1]) / 5);
                        red[4] = (byte)((2 * red[0] + 3 * red[1]) / 5);
                        red[5] = (byte)((1 * red[0] + 4 * red[1]) / 5);
                        red[6] = 0;
                        red[7] = 0xff;
                    }

                    ulong blockBits = 0;
                    blockBits = ReadDXNBlockBits(data, blockStart);

                    byte[] redIndices = new byte[16];
                    for (int i = 0; i < 16; i++)
                    {
                        redIndices[i] = (byte)((blockBits >> (3 * i)) & 0x7);
                    }

                    blockStart += 8;

                    byte[] green = new byte[8];
                    green[1] = data[blockStart];
                    green[0] = data[blockStart + 1];

                    if (green[0] > green[1])
                    {
                        green[2] = (byte)((6 * green[0] + 1 * green[1]) / 7);
                        green[3] = (byte)((5 * green[0] + 2 * green[1]) / 7);
                        green[4] = (byte)((4 * green[0] + 3 * green[1]) / 7);
                        green[5] = (byte)((3 * green[0] + 4 * green[1]) / 7);
                        green[6] = (byte)((2 * green[0] + 5 * green[1]) / 7);
                        green[7] = (byte)((1 * green[0] + 6 * green[1]) / 7);
                    }
                    else
                    {
                        green[2] = (byte)((4 * green[0] + 1 * green[1]) / 5);
                        green[3] = (byte)((3 * green[0] + 2 * green[1]) / 5);
                        green[4] = (byte)((2 * green[0] + 3 * green[1]) / 5);
                        green[5] = (byte)((1 * green[0] + 4 * green[1]) / 5);
                        green[6] = 0;
                        green[7] = 0xff;
                    }

                    blockBits = 0;
                    blockBits = ReadDXNBlockBits(data, blockStart);

                    byte[] greenIndices = new byte[16];
                    for (int i = 0; i < 16; i++)
                    {
                        greenIndices[i] = (byte)((blockBits >> (i * 3)) & 0x7);
                    }


                    for (int pY = 0; pY < 4; pY++)
                    {
                        int j = pY;// ^ 1;
                        for (int pX = 0; pX < 4; pX++)
                        {
                            int pixDataStart = (width * (y * 4 + j) * 4) + ((x * 4 + pX) * 4);
                            int colID = pY * 4 + pX;
                            byte colRed = red[redIndices[colID]];
                            byte colBlue = green[greenIndices[colID]];
                            pixData[pixDataStart] = 0xff;
                            pixData[pixDataStart + 1] = colBlue;
                            pixData[pixDataStart + 2] = colRed;
                            pixData[pixDataStart + 3] = 0xff;
                        }
                    }
                }
            }

            return pixData;

        }

        public static byte[] DecodeDXT5(byte[] data, int width, int height)
        {
            byte[] pixData = new byte[width * height * 4];
            int xBlocks = width / 4;
            int yBlocks = height / 4;
            for (int y = 0; y < yBlocks; y++)
            {
                for (int x = 0; x < xBlocks; x++)
                {
                    int blockDataStart = ((y * xBlocks) + x) * 16;
                    uint[] alphas = new uint[8];
                    ulong alphaMask = 0;

                    alphas[0] = data[blockDataStart + 1];
                    alphas[1] = data[blockDataStart + 0];

                    alphaMask |= data[blockDataStart + 6];
                    alphaMask <<= 8;
                    alphaMask |= data[blockDataStart + 7];
                    alphaMask <<= 8;
                    alphaMask |= data[blockDataStart + 4];
                    alphaMask <<= 8;
                    alphaMask |= data[blockDataStart + 5];
                    alphaMask <<= 8;
                    alphaMask |= data[blockDataStart + 2];
                    alphaMask <<= 8;
                    alphaMask |= data[blockDataStart + 3];


                    // 8-alpha or 6-alpha block
                    if (alphas[0] > alphas[1])
                    {
                        // 8-alpha block: derive the other 6
                        // Bit code 000 = alpha_0, 001 = alpha_1, others are interpolated.
                        alphas[2] = (byte)((6 * alphas[0] + 1 * alphas[1] + 3) / 7);    // bit code 010
                        alphas[3] = (byte)((5 * alphas[0] + 2 * alphas[1] + 3) / 7);    // bit code 011
                        alphas[4] = (byte)((4 * alphas[0] + 3 * alphas[1] + 3) / 7);    // bit code 100
                        alphas[5] = (byte)((3 * alphas[0] + 4 * alphas[1] + 3) / 7);    // bit code 101
                        alphas[6] = (byte)((2 * alphas[0] + 5 * alphas[1] + 3) / 7);    // bit code 110
                        alphas[7] = (byte)((1 * alphas[0] + 6 * alphas[1] + 3) / 7);    // bit code 111
                    }
                    else
                    {
                        // 6-alpha block.
                        // Bit code 000 = alpha_0, 001 = alpha_1, others are interpolated.
                        alphas[2] = (byte)((4 * alphas[0] + 1 * alphas[1] + 2) / 5);    // Bit code 010
                        alphas[3] = (byte)((3 * alphas[0] + 2 * alphas[1] + 2) / 5);    // Bit code 011
                        alphas[4] = (byte)((2 * alphas[0] + 3 * alphas[1] + 2) / 5);    // Bit code 100
                        alphas[5] = (byte)((1 * alphas[0] + 4 * alphas[1] + 2) / 5);    // Bit code 101
                        alphas[6] = 0x00;                                               // Bit code 110
                        alphas[7] = 0xFF;                                               // Bit code 111
                    }

                    byte[,] alpha = new byte[4, 4];

                    for (int i = 0; i < 4; i++)
                    {
                        for (int j = 0; j < 4; j++)
                        {
                            alpha[j, i] = (byte)alphas[alphaMask & 7];
                            alphaMask >>= 3;
                        }
                    }

                    ushort color0 = (ushort)((data[blockDataStart + 8] << 8) + data[blockDataStart + 9]);
                    ushort color1 = (ushort)((data[blockDataStart + 10] << 8) + data[blockDataStart + 11]);

                    uint code = BitConverter.ToUInt32(data, blockDataStart + 8 + 4);

                    ushort r0 = 0, g0 = 0, b0 = 0, r1 = 0, g1 = 0, b1 = 0;
                    r0 = (ushort)(8 * (color0 & 31));
                    g0 = (ushort)(4 * ((color0 >> 5) & 63));
                    b0 = (ushort)(8 * ((color0 >> 11) & 31));

                    r1 = (ushort)(8 * (color1 & 31));
                    g1 = (ushort)(4 * ((color1 >> 5) & 63));
                    b1 = (ushort)(8 * ((color1 >> 11) & 31));

                    for (int k = 0; k < 4; k++)
                    {
                        int j = k ^ 1;

                        for (int i = 0; i < 4; i++)
                        {
                            int pixDataStart = (width * (y * 4 + j) * 4) + ((x * 4 + i) * 4);
                            uint codeDec = code & 0x3;

                            pixData[pixDataStart + 3] = alpha[i, j];

                            switch (codeDec)
                            {
                                case 0:
                                    pixData[pixDataStart + 0] = (byte)r0;
                                    pixData[pixDataStart + 1] = (byte)g0;
                                    pixData[pixDataStart + 2] = (byte)b0;
                                    break;
                                case 1:
                                    pixData[pixDataStart + 0] = (byte)r1;
                                    pixData[pixDataStart + 1] = (byte)g1;
                                    pixData[pixDataStart + 2] = (byte)b1;
                                    break;
                                case 2:
                                    if (color0 > color1)
                                    {
                                        pixData[pixDataStart + 0] = (byte)((2 * r0 + r1) / 3);
                                        pixData[pixDataStart + 1] = (byte)((2 * g0 + g1) / 3);
                                        pixData[pixDataStart + 2] = (byte)((2 * b0 + b1) / 3);
                                    }
                                    else
                                    {
                                        pixData[pixDataStart + 0] = (byte)((r0 + r1) / 2);
                                        pixData[pixDataStart + 1] = (byte)((g0 + g1) / 2);
                                        pixData[pixDataStart + 2] = (byte)((b0 + b1) / 2);
                                    }
                                    break;
                                case 3:
                                    if (color0 > color1)
                                    {
                                        pixData[pixDataStart + 0] = (byte)((r0 + 2 * r1) / 3);
                                        pixData[pixDataStart + 1] = (byte)((g0 + 2 * g1) / 3);
                                        pixData[pixDataStart + 2] = (byte)((b0 + 2 * b1) / 3);
                                    }
                                    else
                                    {
                                        pixData[pixDataStart + 0] = 0;
                                        pixData[pixDataStart + 1] = 0;
                                        pixData[pixDataStart + 2] = 0;
                                    }
                                    break;
                            }

                            code >>= 2;
                        }
                    }


                }
            }
            return pixData;
        }

        public static byte[] DecodeL8(byte[] data, int width, int height)
        {
            var pixData = new byte[(width * height) * 4];
            for (int i = 0; i < (width * height); i++)
            {
                int destOffset = i * 4;
                pixData[destOffset] = data[i];
                pixData[destOffset + 1] = data[i];
                pixData[destOffset + 2] = data[i];
                pixData[destOffset + 3] = 0xff;
            }

            return pixData;
        }

        public static byte[] DecodeA8L8(byte[] data, int width, int height)
        {
            var pixData = new byte[(width * height) * 4];
            for (int i = 0; i < (width * height); i++)
            {
                int srcOffset = i * 2;
                int destOffset = i * 4;
                pixData[destOffset] = data[srcOffset + 1];
                pixData[destOffset + 1] = data[srcOffset + 1];
                pixData[destOffset + 2] = data[srcOffset + 1];
                pixData[destOffset + 3] = data[srcOffset];
            }

            return pixData;
        }

        public static byte[] DecodeA8R8G8B8(byte[] data, int width, int height)
        {
            byte[] pixData = new byte[(width * height) * 4];

            for (int i = 0; i < (width * height); i++)
            {
                int offset = i * 4;
                pixData[offset] = data[offset + 3];
                pixData[offset + 1] = data[offset + 2];
                pixData[offset + 2] = data[offset + 1];
                pixData[offset + 3] = data[offset];
            }

            return pixData;
        }

        public static byte[] DecodeR5G6B5(byte[] data, int width, int height)
        {
            var tmpS = new byte[2];
            var pixData = new byte[(width * height) * 4];
            for (int i = 0; i < (width * height); i++)
            {
                int srcOffset = i * 2;
                int destOffst = i * 4;

                tmpS[0] = data[srcOffset + 1];
                tmpS[1] = data[srcOffset];
                short color = BitConverter.ToInt16(tmpS, 0);

                byte b5 = (byte)((color & 0xf800) >> 11);
                byte g6 = (byte)((color & 0x07e0) >> 5);
                byte r5 = (byte)((color & 0x1f));

                pixData[destOffst] = (byte)((r5 << 3) | (r5 >> 2));
                pixData[destOffst + 1] = (byte)((g6 << 2) | (g6 >> 4));
                pixData[destOffst + 2] = (byte)((b5 << 3) | (b5 >> 2));
                pixData[destOffst + 3] = 0xff;
            }

            return pixData;
        }
    }
}

