﻿namespace Amicitia
{
    internal struct SupportedFileInfo
    {
        public string Description;
        public SupportedFileType Type;
        public bool ExportOnly;
        public string[] Extensions;

        public SupportedFileInfo(string description, SupportedFileType type, bool exportOnly, params string[] extensions)
        {
            Description = description;
            Type = type;
            ExportOnly = exportOnly;
            Extensions = extensions;
        }
    }

    internal enum SupportedFileType : int
    {
        Default,
        GenericPAK,
        TMX,
        PNG,
        SPR,
        BVPArchive
    }
}