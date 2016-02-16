﻿namespace AtlusLibSharp.SMT3.Graphics
{
    using System;
    using System.IO;
    using System.Drawing;

    using PS2.Graphics;
    using Common;
    using Common.Utilities;
    using nQuant;

    public enum TMXWrapMode : byte
    {
        Repeat = 0x00,
        Clamp  = 0x01
    }

    public class TMXFile : BinaryFileBase, ITextureFile
    {
        // Internal Constants
        internal const byte HEADER_SIZE = 0x10;
        internal const short FLAG = 0x0002;
        internal const string TAG = "TMX0";
        internal const byte COMMENT_MAX_LENGTH = 28;

        // Private Fields
        private byte _numPalettes;
        private PixelFormat _paletteFmt;
        private ushort _width;
        private ushort _height;
        private PixelFormat _pixelFmt;
        private byte _numMipMaps;
        private ushort _mipKL;
        private byte _reserved;
        private byte _wrapModes;
        private int _userTextureID;
        private int _userCLUTID;
        private string _userComment;

        // Pixel, palette and bitmap private fields
        private int _paletteColorCount;
        private Color[][] _paletteEntries;
        private byte[] _pixelIndices;
        private byte[][] _mipMapPixelIndices;
        private Color[] _pixels;
        private Color[][] _mipMapPixels;
        private Bitmap _bitmap;

        // Constructors
        internal TMXFile(BinaryReader reader)
        {
            InternalRead(reader);
        }

        public TMXFile(Bitmap bitmap, PixelFormat pixelFormat, string comment = "")
        {
            _width = (ushort)bitmap.Width;
            _height = (ushort)bitmap.Height;
            _pixelFmt = pixelFormat;
            _mipKL = 0;
            _wrapModes = byte.MaxValue;
            _userComment = comment;

            switch (pixelFormat)
            {
                case PixelFormat.PSMCT32:
                case PixelFormat.PSMCT24:
                case PixelFormat.PSMCT16:
                case PixelFormat.PSMCT16S: // Non-indexed
                    _paletteFmt = 0;
                    _pixels = BitmapHelper.GetColors(bitmap);
                    _bitmap = bitmap;
                    break;
                case PixelFormat.PSMT8:
                case PixelFormat.PSMT8H:
                    SetupIndexedBitmap(bitmap, 256);
                    break;
                case PixelFormat.PSMT4:
                case PixelFormat.PSMT4HL:
                case PixelFormat.PSMT4HH:
                    SetupIndexedBitmap(bitmap, 16);
                    break;
                default:
                    throw new ArgumentException("This pixel format is not supported for encoding.");
            }
        }

        #region Properties

        public byte PaletteCount
        {
            get { return _numPalettes; }
        }

        public PixelFormat PaletteFormat
        {
            get { return _paletteFmt; }
        }

        public ushort Width
        {
            get { return _width; }
        }

        public ushort Height
        {
            get { return _height; }
        }

        public PixelFormat PixelFormat
        {
            get { return _pixelFmt; }
        }

        public byte MipMapCount
        {
            get { return _numMipMaps; }
        }

        public float MipMapKValue
        {
            get
            {
                if (_mipKL != ushort.MaxValue)
                {
                    return ((float)(_mipKL & 0x0FFF)) / (1 << 4);
                }
                else
                {
                    return -0.0625f;
                }
            }
        }

        public byte MipMapLValue
        {
            get
            {
                if (_mipKL != ushort.MaxValue)
                {
                    return (byte)(_mipKL & 0xF000);
                }
                else
                {
                    return 3;
                }
            }
        }

        public TMXWrapMode HorizontalWrappingMode
        {
            get
            {
                if (_wrapModes != byte.MaxValue)
                {
                    return (TMXWrapMode)((_wrapModes & 0xC) >> 2);
                }
                else
                {
                    return TMXWrapMode.Repeat;
                }
            }
            set
            {
                if (_wrapModes != byte.MaxValue)
                {
                    _wrapModes = (byte)(_wrapModes & ~0xC);
                    _wrapModes |= (byte)(((byte)value & 0x3) << 2);
                }
            }
        }

        public TMXWrapMode VerticalWrappingMode
        {
            get
            {
                if (_wrapModes != byte.MaxValue)
                {
                    return (TMXWrapMode)(_wrapModes & 0x3);
                }
                else
                {
                    return TMXWrapMode.Repeat;
                }
            }
            set
            {
                if (_wrapModes != byte.MaxValue)
                {
                    _wrapModes = (byte)(_wrapModes & ~0x3);
                    _wrapModes |= (byte)((byte)value & 0x3);
                }
            }
        }

        public int UserTextureID
        {
            get { return _userTextureID; }
            set { _userTextureID = value; }
        }

        public int UserClutID
        {
            get { return _userCLUTID; }
            set { _userCLUTID = value; }
        }

        public string UserComment
        {
            get
            {
                return _userComment;
            }
            set
            {
                _userComment = value;
                if (_userComment.Length > COMMENT_MAX_LENGTH)
                {
                    // Remove excess characters
                    _userComment = _userComment.Remove(COMMENT_MAX_LENGTH-1);
                }
            }
        }

        public int PaletteColorCount
        {
            get { return _paletteColorCount; }
        }

        public Color[][] Palettes
        {
            get { return _paletteEntries; }
        }

        public byte[] PixelIndices
        {
            get { return _pixelIndices; }
        }

        public byte[][] MipMapPixelIndices
        {
            get { return _mipMapPixelIndices; }
        }

        public Color[] Pixels
        {
            get { return _pixels; }
        }

        public Color[][] MipMapPixels
        {
            get { return _mipMapPixels; }
        }

        public bool UsesPalette
        {
            get { return _numPalettes > 0; }
        }

        #endregion

        // Public Static Methods
        public static TMXFile LoadFrom(string path)
        {
            using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
                return new TMXFile(reader);
        }

        public static TMXFile LoadFrom(Stream stream, bool leaveStreamOpen)
        {
            using (BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveStreamOpen))
                return new TMXFile(reader);
        }

        // Public Methods
        public Bitmap GetBitmap()
        {
            return GetBitmap(0, -1);
        }

        public Bitmap GetBitmap(int paletteIndex = 0, int mipMapIndex = -1)
        {
            if (mipMapIndex == -1)
            {
                // Check if the bitmap hasn't been created already
                if (_bitmap == null || paletteIndex != 0 || (_bitmap.Width != _width && _bitmap.Height != _height))
                {
                    CreateBitmap(paletteIndex, mipMapIndex);
                }
            }
            else
            {
                CreateBitmap(paletteIndex, mipMapIndex);
            }

            return _bitmap;
        }

        // Internal methods
        internal override void InternalWrite(BinaryWriter writer)
        {
            int posFileStart = (int)writer.BaseStream.Position;

            // Seek past chunk header
            writer.BaseStream.Seek(HEADER_SIZE, SeekOrigin.Current);

            writer.Write(_numPalettes);
            writer.Write((byte)_paletteFmt);
            writer.Write(_width);
            writer.Write(_height);
            writer.Write((byte)_pixelFmt);
            writer.Write(_numMipMaps);
            writer.Write(_mipKL);
            writer.Write(_reserved);
            writer.Write(_wrapModes);
            writer.Write(_userTextureID);
            writer.Write(_userCLUTID);
            writer.WriteCString(_userComment, COMMENT_MAX_LENGTH);

            // Check if there's any palettes and write them
            if (_numPalettes > 0)
            {
                WritePalette(writer);
            }

            // Write the pixels for the image and mipmaps
            WritePixels(writer);

            // Calculate the length
            int posFileEnd = (int)writer.BaseStream.Position;
            int length = posFileEnd - posFileStart;

            // Seek back to the chunk header and write it
            writer.BaseStream.Seek(posFileStart, SeekOrigin.Begin);
            writer.Write(FLAG);
            writer.Write((short)0); // userID
            writer.Write(length);
            writer.WriteCString(TAG, 4);

            // Seek back to the end of the data
            writer.BaseStream.Seek(posFileEnd, SeekOrigin.Begin);
        }

        // Private Methods
        private void InternalRead(BinaryReader reader)
        {
            long posFileStart = reader.GetPosition();
            short flag = reader.ReadInt16();
            short userID = reader.ReadInt16();
            int length = reader.ReadInt32();
            string tag = reader.ReadCString(4);
            reader.AlignPosition(16);

            if (tag != TAG)
            {
                throw new InvalidDataException();
            }

            _numPalettes = reader.ReadByte();
            _paletteFmt = (PixelFormat)reader.ReadByte();
            _width = reader.ReadUInt16();
            _height = reader.ReadUInt16();
            _pixelFmt = (PixelFormat)reader.ReadByte();
            _numMipMaps = reader.ReadByte();
            _mipKL = reader.ReadUInt16();
            _reserved = reader.ReadByte();
            _wrapModes = reader.ReadByte();
            _userTextureID = reader.ReadInt32();
            _userCLUTID = reader.ReadInt32();
            _userComment = reader.ReadCString(28);

            // Check if there's any palettes and read them
            if (_numPalettes > 0)
            {
                ReadPalette(reader);
            }

            // Read the pixels for the image and mipmaps
            ReadPixels(reader);
        }

        private void ReadPalette(BinaryReader reader)
        {
            int paletteWH;

            if (_pixelFmt == PixelFormat.PSMT4 || _pixelFmt == PixelFormat.PSMT4HL || _pixelFmt == PixelFormat.PSMT4HH)
            {
                _paletteColorCount = 16;
                paletteWH = 4;
            }
            else
            {
                _paletteColorCount = 256;
                paletteWH = 16;
            }

            _paletteEntries = new Color[_numPalettes][];

            for (int i = 0; i < _numPalettes; i++)
            {
                PixelFormatHelper.ReadPixelData(_paletteFmt, reader, paletteWH, paletteWH, out _paletteEntries[i]);

                if (_paletteColorCount == 256)
                {
                    PixelFormatHelper.TilePalette(_paletteEntries[i], out _paletteEntries[i]);
                }
            }
        }

        private void ReadPixels(BinaryReader reader)
        {
            if (UsesPalette)
            {
                PixelFormatHelper.ReadPixelData(_pixelFmt, reader, _width, _height, out _pixelIndices);

                if (_numMipMaps > 0)
                {
                    _mipMapPixelIndices = new byte[_numMipMaps][];
                    for (int i = 0; i < _numMipMaps; i++)
                    {
                        int div = 2 * (2 * i);
                        PixelFormatHelper.ReadPixelData(_pixelFmt, reader, _width / div, _height / div, out _mipMapPixelIndices[i]);
                    }
                }
            }
            else
            {
                PixelFormatHelper.ReadPixelData(_pixelFmt, reader, _width, _height, out _pixels);

                if (_numMipMaps > 0)
                {
                    _mipMapPixels = new Color[_numMipMaps][];
                    for (int i = 0; i < _numMipMaps; i++)
                    {
                        int div = 2 * (2 * i);
                        PixelFormatHelper.ReadPixelData(_pixelFmt, reader, _width / div, _height / div, out _mipMapPixels[i]);
                    }
                }
            }
        }

        private void WritePalette(BinaryWriter writer)
        {
            Color[][] outPaletteEntries = _paletteEntries;

            int paletteWH = 16;

            if (_paletteColorCount == 16)
            {
                paletteWH = 4;
            }

            if (_paletteColorCount == 256)
            {
                // Tile the palette
                for (int i = 0; i < _numPalettes; i++)
                {
                    PixelFormatHelper.TilePalette(_paletteEntries[i], out outPaletteEntries[i]);
                }
            }

            switch (_paletteFmt)
            {
                case PixelFormat.PSMCT32:
                    for (int i = 0; i < _numPalettes; i++)
                    {
                        PixelFormatHelper.WritePSMCT32(writer, paletteWH, paletteWH, outPaletteEntries[i]);
                    }
                    break;

                case PixelFormat.PSMCT24:
                    for (int i = 0; i < _numPalettes; i++)
                    {
                        PixelFormatHelper.WritePSMCT24(writer, paletteWH, paletteWH, outPaletteEntries[i]);
                    }
                    break;

                case PixelFormat.PSMCT16:
                    for (int i = 0; i < _numPalettes; i++)
                    {
                        PixelFormatHelper.WritePSMCT16(writer, paletteWH, paletteWH, outPaletteEntries[i]);
                    }
                    break;

                case PixelFormat.PSMCT16S:
                    for (int i = 0; i < _numPalettes; i++)
                    {
                        PixelFormatHelper.WritePSMCT16S(writer, paletteWH, paletteWH, outPaletteEntries[i]);
                    }
                    break;

                default:
                    throw new NotImplementedException("WritePalette: Unknown palette format.");
            }
        }

        private void WritePixels(BinaryWriter writer)
        {
            switch (_pixelFmt)
            {
                case PixelFormat.PSMCT32:
                case PixelFormat.PSMZ32:
                    {
                        PixelFormatHelper.WritePSMCT32(writer, _width, _height, _pixels);
                        if (_numMipMaps > 0)
                        {
                            for (int i = 0; i < _numMipMaps; i++)
                            {
                                int div = 2 * (2 * i);
                                PixelFormatHelper.WritePSMCT32(writer, _width / div, _height / div, _mipMapPixels[i]);
                            }
                        }
                    }
                    break;

                case PixelFormat.PSMCT24:
                case PixelFormat.PSMZ24:
                    {
                        PixelFormatHelper.WritePSMCT24(writer, _width, _height, _pixels);
                        if (_numMipMaps > 0)
                        {
                            for (int i = 0; i < _numMipMaps; i++)
                            {
                                int div = 2 * (2 * i);
                                PixelFormatHelper.WritePSMCT24(writer, _width / div, _height / div, _mipMapPixels[i]);
                            }
                        }
                    }
                    break;

                case PixelFormat.PSMCT16:
                case PixelFormat.PSMZ16:
                    {
                        PixelFormatHelper.WritePSMCT16(writer, _width, _height, _pixels);
                        if (_numMipMaps > 0)
                        {
                            for (int i = 0; i < _numMipMaps; i++)
                            {
                                int div = 2 * (2 * i);
                                PixelFormatHelper.WritePSMCT16(writer, _width / div, _height / div, _mipMapPixels[i]);
                            }
                        }
                    }
                    break;

                case PixelFormat.PSMCT16S:
                case PixelFormat.PSMZ16S:
                    {
                        PixelFormatHelper.WritePSMCT16S(writer, _width, _height, _pixels);
                        if (_numMipMaps > 0)
                        {
                            for (int i = 0; i < _numMipMaps; i++)
                            {
                                int div = 2 * (2 * i);
                                PixelFormatHelper.WritePSMCT16S(writer, _width / div, _height / div, _mipMapPixels[i]);
                            }
                        }
                    }
                    break;

                case PixelFormat.PSMT8:
                case PixelFormat.PSMT8H:
                    {
                        PixelFormatHelper.WritePSMT8(writer, _width, _height, _pixelIndices);
                        if (_numMipMaps > 0)
                        {
                            for (int i = 0; i < _numMipMaps; i++)
                            {
                                int div = 2 * (2 * i);
                                PixelFormatHelper.WritePSMT8(writer, _width / div, _height / div, _mipMapPixelIndices[i]);
                            }
                        }
                    }
                    break;

                case PixelFormat.PSMT4:
                case PixelFormat.PSMT4HL:
                case PixelFormat.PSMT4HH:
                    {
                        PixelFormatHelper.WritePSMT4(writer, _width, _height, _pixelIndices);
                        if (_numMipMaps > 0)
                        {
                            for (int i = 0; i < _numMipMaps; i++)
                            {
                                int div = 2 * (2 * i);
                                PixelFormatHelper.WritePSMT4(writer, _width / div, _height / div, _mipMapPixelIndices[i]);
                            }
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException("WritePixels: Unknown pixel format.");
            }
        }

        private void CreateBitmap(int palIdx, int mipIdx)
        {
            if (UsesPalette)
            {
                if (mipIdx == -1)
                {
                    _bitmap = BitmapHelper.Create(_paletteEntries[palIdx], _pixelIndices, _width, _height);
                }
                else
                {
                    _bitmap = BitmapHelper.Create(_paletteEntries[palIdx], _mipMapPixelIndices[mipIdx],
                        GetMipDimension(_width, mipIdx), GetMipDimension(_height, mipIdx));
                }
            }
            else
            {
                if (mipIdx == -1)
                {
                    _bitmap = BitmapHelper.Create(_pixels, _width, _height);
                }
                else
                {
                    _bitmap = BitmapHelper.Create(_mipMapPixels[mipIdx], GetMipDimension(_width, mipIdx), GetMipDimension(_height, mipIdx));
                }
            }
        }

        private int GetMipDimension(int dim, int mipIdx)
        {
            int div = 2 * (2 * mipIdx);
            return dim / div;
        }

        private void SetupIndexedBitmap(Bitmap bitmap, int paletteColorCount)
        {
            WuQuantizer quantizer = new WuQuantizer();
            _bitmap = (Bitmap)quantizer.QuantizeImage(bitmap, paletteColorCount, 0, 1);
            _numPalettes = 1;
            _paletteColorCount = paletteColorCount;
            _paletteEntries = new Color[1][];
            _paletteEntries[0] = BitmapHelper.GetPalette(_bitmap, paletteColorCount);
            _pixelIndices = BitmapHelper.GetIndices(_bitmap);
        }
    }
}