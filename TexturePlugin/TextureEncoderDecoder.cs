﻿using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TexturePlugin
{
    public class TextureEncoderDecoder
    {
        public static int RGBAToFormatByteSize(TextureFormat format, int width, int height)
        {
            int block4RoundX = (width + 3) >> 2 << 2;
            int block4RoundY = (height + 3) >> 2 << 2;
            switch (format)
            {
                case TextureFormat.RGB9e5Float:
                    return width * height * 4;
                case TextureFormat.ARGB32:
                    return width * height * 4;
                case TextureFormat.BGRA32New:
                    return width * height * 4;
                case TextureFormat.RGBA32:
                    return width * height * 4;
                case TextureFormat.RGB24:
                    return width * height * 3;
                case TextureFormat.ARGB4444:
                    return width * height * 2;
                case TextureFormat.RGBA4444:
                    return width * height * 2;
                case TextureFormat.RGB565:
                    return width * height * 2;
                case TextureFormat.Alpha8:
                    return width * height;
                case TextureFormat.R8:
                    return width * height;
                case TextureFormat.R16:
                    return width * height * 2;
                case TextureFormat.RG16:
                    return width * height * 2;
                case TextureFormat.RHalf:
                    return width * height * 2;
                case TextureFormat.RGHalf:
                    return width * height * 4;
                case TextureFormat.RGBAHalf:
                    return width * height * 8;
                case TextureFormat.RFloat:
                    return width * height * 4;
                case TextureFormat.RGFloat:
                    return width * height * 8;
                case TextureFormat.RGBAFloat:
                    return width * height * 16;
                case TextureFormat.YUV2:
                    return width * height * 2;
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                    return width * height * 4; //don't know don't care
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                case TextureFormat.ETC2_RGB4:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ASTC_RGB_4x4:
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_4x4:
                case TextureFormat.ASTC_RGBA_5x5:
                case TextureFormat.ASTC_RGBA_8x8:
                case TextureFormat.ASTC_RGBA_10x10:
                case TextureFormat.ASTC_RGBA_12x12:
                    return -1; //todo
                case TextureFormat.DXT1:
                    return block4RoundX * block4RoundY * 2;
                case TextureFormat.DXT5:
                    return block4RoundX * block4RoundY;
                case TextureFormat.BC4:
                    return block4RoundX * block4RoundY;
                case TextureFormat.BC5:
                    return block4RoundX * block4RoundY;
                case TextureFormat.BC6H:
                    return block4RoundX * block4RoundY;
                case TextureFormat.BC7:
                    return block4RoundX * block4RoundY;
                default:
                    return width * height * 16; //don't know don't care
            }
        }

        public static byte[] Decode(byte[] data, int width, int height, TextureFormat format)
        {
            switch (format)
            {
                //crunch
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5Crunched:
                {
                    byte[] dest = new byte[width * height * 4];
                    uint size = 0;
                    unsafe
                    {
                        fixed (byte* dataPtr = data)
                        fixed (byte* destPtr = dest)
                        {
                            IntPtr dataIntPtr = (IntPtr)dataPtr;
                            IntPtr destIntPtr = (IntPtr)destPtr;
                            size = PInvoke.DecodeByCrunchUnity(dataIntPtr, destIntPtr, (int)format, (uint)width, (uint)height, (uint)data.Length);
                        }
                    }
                    if (size > 0)
                    {
                        if (format == TextureFormat.DXT1Crunched)
                        {
                            byte[] dxt1 = DXTDecoders.ReadDXT1(dest, width, height);
                            for (int i = 0; i < dxt1.Length; i += 4)
                            {
                                byte temp = dxt1[i];
                                dxt1[i] = dxt1[i + 2];
                                dxt1[i + 2] = temp;
                            }
                            return dxt1;
                        }
                        else //if (format == TextureFormat.DXT5Crunched)
                        {
                            byte[] dxt5 = DXTDecoders.ReadDXT5(dest, width, height);
                            for (int i = 0; i < dxt5.Length; i += 4)
                            {
                                byte temp = dxt5[i];
                                dxt5[i] = dxt5[i + 2];
                                dxt5[i + 2] = temp;
                            }
                            return dxt5;
                        }
                    }
                    else
                    {
                        dest = null;
                        return null;
                    }
                }
                //pvrtexlib
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32New:
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA4444:
                case TextureFormat.RGB565:
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                /////////////////////////////////
                case TextureFormat.YUV2:
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                case TextureFormat.ETC2_RGB4:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ASTC_RGB_4x4:
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_4x4:
                case TextureFormat.ASTC_RGBA_5x5:
                case TextureFormat.ASTC_RGBA_8x8:
                case TextureFormat.ASTC_RGBA_10x10:
                case TextureFormat.ASTC_RGBA_12x12:
                {
                    byte[] dest = new byte[width * height * 4];
                    uint size = 0;
                    unsafe
                    {
                        fixed (byte* dataPtr = data)
                        fixed (byte* destPtr = dest)
                        {
                            IntPtr dataIntPtr = (IntPtr)dataPtr;
                            IntPtr destIntPtr = (IntPtr)destPtr;
                            size = PInvoke.DecodeByPVRTexLib(dataIntPtr, destIntPtr, (int)format, (uint)width, (uint)height);
                        }
                    }
                    if (size > 0)
                    {
                        byte[] resizedDest = new byte[size];
                        Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                        dest = null;
                        return resizedDest;
                    }
                    else
                    {
                        dest = null;
                        return null;
                    }
                }
                //detex
                case TextureFormat.DXT1:
                {
                    byte[] dxt1 = DXTDecoders.ReadDXT1(data, width, height);
                    for (int i = 0; i < dxt1.Length; i += 4)
                    {
                        byte temp = dxt1[i];
                        dxt1[i] = dxt1[i + 2];
                        dxt1[i + 2] = temp;
                    }
                    return dxt1;
                }
                case TextureFormat.DXT5:
                {
                    byte[] dxt5 = DXTDecoders.ReadDXT5(data, width, height);
                    for (int i = 0; i < dxt5.Length; i += 4)
                    {
                        byte temp = dxt5[i];
                        dxt5[i] = dxt5[i + 2];
                        dxt5[i + 2] = temp;
                    }
                    return dxt5;
                }
                case TextureFormat.BC7:
                {
                    byte[] bc7 = BC7Decoder.ReadBC7(data, width, height);
                    for (int i = 0; i < bc7.Length; i += 4)
                    {
                        byte temp = bc7[i];
                        bc7[i] = bc7[i + 2];
                        bc7[i + 2] = temp;
                    }
                    return bc7;
                }
                case TextureFormat.BC6H: //pls don't use
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                    return null;
                case TextureFormat.RGB9e5Float: //pls don't use
                    return null;
                default:
                    return null;
            }
        }

        public static byte[] Encode(byte[] data, int width, int height, TextureFormat format, int quality = 5)
        {
            switch (format)
            {
                //crunch
                case TextureFormat.DXT1Crunched:
                case TextureFormat.DXT5Crunched:
                {
                    byte[] dest = new byte[width * height * 4];
                    uint size = 0;
                    unsafe
                    {
                        fixed (byte* dataPtr = data)
                        fixed (byte* destPtr = dest)
                        {
                            IntPtr dataIntPtr = (IntPtr)dataPtr;
                            IntPtr destIntPtr = (IntPtr)destPtr;
                            size = PInvoke.EncodeByCrunchUnity(dataIntPtr, destIntPtr, (int)format, quality, (uint)width, (uint)height);
                        }
                    }
                    if (size > 0)
                    {
                        byte[] resizedDest = new byte[size];
                        Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                        dest = null;
                        return resizedDest;
                    }
                    else
                    {
                        dest = null;
                        return null;
                    }
                }
                //pvrtexlib
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32New:
                case TextureFormat.RGBA32:
                case TextureFormat.RGB24:
                case TextureFormat.ARGB4444:
                case TextureFormat.RGBA4444:
                case TextureFormat.RGB565:
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RHalf:
                case TextureFormat.RGHalf:
                case TextureFormat.RGBAHalf:
                case TextureFormat.RFloat:
                case TextureFormat.RGFloat:
                case TextureFormat.RGBAFloat:
                /////////////////////////////////
                case TextureFormat.YUV2: //looks like this should be YUY2 and the api has a typo
                case TextureFormat.EAC_R:
                case TextureFormat.EAC_R_SIGNED:
                case TextureFormat.EAC_RG:
                case TextureFormat.EAC_RG_SIGNED:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC_RGB4_3DS:
                case TextureFormat.ETC_RGBA8_3DS:
                case TextureFormat.ETC2_RGB4:
                case TextureFormat.ETC2_RGBA1:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.PVRTC_RGB2:
                case TextureFormat.PVRTC_RGBA2:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                case TextureFormat.ASTC_RGB_4x4:
                case TextureFormat.ASTC_RGB_5x5:
                case TextureFormat.ASTC_RGB_6x6:
                case TextureFormat.ASTC_RGB_8x8:
                case TextureFormat.ASTC_RGB_10x10:
                case TextureFormat.ASTC_RGB_12x12:
                case TextureFormat.ASTC_RGBA_4x4:
                case TextureFormat.ASTC_RGBA_5x5:
                case TextureFormat.ASTC_RGBA_8x8:
                case TextureFormat.ASTC_RGBA_10x10:
                case TextureFormat.ASTC_RGBA_12x12:
                {
                    byte[] dest = new byte[width * height * 16]; //just to be safe, buf is 16 times size of original (obv this is prob too big)
                    uint size = 0;
                    unsafe
                    {
                        fixed (byte* dataPtr = data)
                        fixed (byte* destPtr = dest)
                        {
                            IntPtr dataIntPtr = (IntPtr)dataPtr;
                            IntPtr destIntPtr = (IntPtr)destPtr;
                            size = PInvoke.EncodeByPVRTexLib(dataIntPtr, destIntPtr, (int)format, quality, (uint)width, (uint)height);
                        }
                    }
                    if (size > 0)
                    {
                        byte[] resizedDest = new byte[size];
                        Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                        dest = null;
                        return resizedDest;
                    }
                    else
                    {
                        dest = null;
                        return null;
                    }
                }
                case TextureFormat.DXT1:
                case TextureFormat.DXT5:
                case TextureFormat.BC7:
                {
                    byte[] dest = new byte[width * height * 4];
                    uint size = 0;
                    unsafe
                    {
                        fixed (byte* dataPtr = data)
                        fixed (byte* destPtr = dest)
                        {
                            IntPtr dataIntPtr = (IntPtr)dataPtr;
                            IntPtr destIntPtr = (IntPtr)destPtr;
                            size = PInvoke.EncodeByISPC(dataIntPtr, destIntPtr, (int)format, quality, (uint)width, (uint)height);
                        }
                    }
                    if (size > 0)
                    {
                        byte[] resizedDest = new byte[size];
                        Buffer.BlockCopy(dest, 0, resizedDest, 0, (int)size);
                        dest = null;
                        return resizedDest;
                    }
                    else
                    {
                        dest = null;
                        return null;
                    }
                }
                case TextureFormat.BC6H: //pls don't use
                case TextureFormat.BC4:
                case TextureFormat.BC5:
                    return null;
                case TextureFormat.RGB9e5Float: //pls don't use
                    return null;
                default:
                    return null;
            }
        }
    }
}
