﻿using IOModelFormats.SourceModelData;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace AtlusLibSharp.Persona3.RenderWare
{
    public class RWGeometry : RWNode
    {
        private RWGeometryStruct _struct;
        private RWMaterialList _materialList;
        private RWExtension _extension;

        public RWGeometryStruct Struct
        {
            get { return _struct; }
            set
            {
                _struct = value;
                _struct.Parent = this;
            }
        }

        public RWMaterialList MaterialList
        {
            get { return _materialList; }
            set
            {
                _materialList = value;
                _materialList.Parent = this;
            }
        }

        public RWExtension Extension
        {
            get { return _extension; }
            set
            {
                _extension = value;
                _extension.Parent = this;
            }
        }

        internal RWGeometry(uint size, uint version, RWNode parent, BinaryReader reader)
                : base(RWType.Geometry, size, version, parent)
        {
            Struct = ReadNode(reader, this) as RWGeometryStruct;
            MaterialList = ReadNode(reader, this) as RWMaterialList;
            Extension = ReadNode(reader, this) as RWExtension;
        }

        public RWGeometry()
            : base(RWType.Geometry) { }

        public RWGeometry(RWGeometryStruct geo, RWMaterialList materialList, RWExtension extension)
            : base(RWType.Geometry)
        {
            Struct = geo;
            MaterialList = materialList;
            Extension = extension;
        }

        protected override void InternalWriteData(BinaryWriter writer)
        {
            Struct.Write(writer);
            MaterialList.Write(writer);
            Extension.Write(writer);
        }

        public static RWGeometry FromSMD(RWClump refClump, string filename)
        {
            SMDFile smd = new SMDFile(filename);
            Vector3[] pos = new Vector3[smd.Triangles.Count * 3];
            Vector3[] nrm = new Vector3[smd.Triangles.Count * 3];
            Vector2[] uv = new Vector2[smd.Triangles.Count * 3];
            byte[][] skinBoneIndices = new byte[smd.Triangles.Count * 3][];
            float[][] skinBoneWeights = new float[smd.Triangles.Count * 3][];
            List<string> textureList = new List<string>();
            List<ushort> textureIDList = new List<ushort>();

            int vIdx = -1;
            foreach (SMDTriangle smdTri in smd.Triangles)
            {
                string materialName = Path.GetFileNameWithoutExtension(smdTri.MaterialName);
                if (!textureList.Contains(materialName))
                    textureList.Add(materialName);
                textureIDList.Add((ushort)textureList.IndexOf(materialName));

                foreach (SMDVertex smdVtx in smdTri.Vertices)
                {
                    ++vIdx;
                    pos[vIdx] = smdVtx.Position;
                    nrm[vIdx] = smdVtx.Normal;
                    //pos[vIdx] = Vector3.Transform(smdVtx.Position, refClump.FrameList.Struct.Frames[2].WorldMatrix.Inverted());
                    //nrm[vIdx] = Vector3.TransformNormal(smdVtx.Normal, refClump.FrameList.Struct.Frames[2].WorldMatrix.Inverted());
                    uv[vIdx] = new Vector2(smdVtx.UV.X, smdVtx.UV.Y);
                    skinBoneIndices[vIdx] = new byte[4];
                    skinBoneWeights[vIdx] = new float[4];

                    float weightSum = 0.0f;

                    SMDLink[] links = smdVtx.Links;

                    if (smdVtx.LinkCount > 4)
                    {
                        links = smdVtx.Links.OrderBy(l => l.Weight).ToArray();
                    }

                    for (int i = 0; i < smdVtx.LinkCount; i++)
                    {
                        if (i == 4)
                            break;

                        uint boneNameID = uint.Parse(smd.Nodes[smdVtx.Links[i].BoneID].NodeName);
                        skinBoneIndices[vIdx][i] = (byte)refClump.FrameList.GetHierarchyIndexByNameID(boneNameID);
                        skinBoneWeights[vIdx][i] = smdVtx.Links[i].Weight;
                        weightSum += smdVtx.Links[i].Weight;
                    }

                    if (weightSum != 1.0f)
                    {
                        float addWeight = (1.0f - weightSum) / 4;
                        for (int i = 0; i < 4; i++)
                        {
                            skinBoneWeights[vIdx][i] += addWeight;
                        }
                    }

                }
            }

            Vector2[][] uvs = new Vector2[1][];
            uvs[0] = uv;

            RWGeometry geometry = new RWGeometry
            {
                Struct = new RWGeometryStruct(ref skinBoneIndices, ref skinBoneWeights, pos, nrm, textureIDList.ToArray(), uvs),
                MaterialList = new RWMaterialList(textureList.ToArray()),
            };

            RWSkinPlugin skin = new RWSkinPlugin(refClump.FrameList, geometry, skinBoneIndices, skinBoneWeights);

            geometry.Extension = new RWExtension(skin);

            return geometry;
        }

    }
}