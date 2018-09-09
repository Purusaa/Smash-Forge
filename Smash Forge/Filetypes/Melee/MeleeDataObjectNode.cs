﻿using MeleeLib.DAT;
using MeleeLib.DAT.Helpers;
using MeleeLib.GCX;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SFGenericModel.Utils;
using SFGraphics.Cameras;
using SFGraphics.GLObjects.Shaders;
using Smash_Forge.Filetypes.Melee;
using Smash_Forge.GUI.Melee;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Smash_Forge
{
    public class MeleeDataObjectNode : MeleeNode
    {
        enum TextureFlag : uint
        {
            BumpMap = 0x00,
            Sphere = 0x01,
            Diffuse = 0x10,
            Specular = 0x20,
            Unk3 = 0x30, // also diffuse?
            AlphaTest = 0x300000, // whispy woods
            Unk4 = 0x80, // diffuse with inverted colors?
        }

        public DatDOBJ DOBJ;

        // For Rendering Only
        private List<MeleeMesh> renderMeshes = new List<MeleeMesh>();
        private List<MeleeRenderTexture> renderTextures = new List<MeleeRenderTexture>();

        public Vector3 BonePosition;

        // for importing
        public List<GXVertex[]> VertsToImport;

        public MeleeDataObjectNode(DatDOBJ DOBJ)
        {
            ImageKey = "mesh";
            SelectedImageKey = "mesh";
            this.DOBJ = DOBJ;
            Checked = true;

            ContextMenu = new ContextMenu();

            MenuItem Edit = new MenuItem("Edit");
            Edit.Click += OpenEditor;
            ContextMenu.MenuItems.Add(Edit);

            MenuItem Clear = new MenuItem("Clear Polygons");
            Clear.Click += ClearPolygons;
            ContextMenu.MenuItems.Add(Clear);

            MenuItem smd = new MenuItem("Import from File");
            smd.Click += ImportModel;
            ContextMenu.MenuItems.Add(smd);
        }

        public void OpenEditor(object sender, EventArgs args)
        {
            DOBJEditor editor = new DOBJEditor(DOBJ, this);
            editor.Show();
        }

        public void ClearPolygons(object sender, EventArgs args)
        {
            DOBJ.Polygons.Clear();
        }

        public void ImportModel(object sender, EventArgs args)
        {
            using (DOBJImportSettings import = new DOBJImportSettings(this))
            {
                import.ShowDialog();
                if (import.exitStatus == DOBJImportSettings.ExitStatus.Opened)
                {
                    GetDatFile().RecompileVertices();

                }
            }
        }

        public void GetVerticesAsTriangles(out int[] indices, out List<GXVertex> Verts)
        {
            Verts = new List<GXVertex>();
            List<int> ind = new List<int>();

            VBN Bones = GetRoot().RenderBones;
            GXVertexDecompressor decompressor = new GXVertexDecompressor(GetDatFile().DatFile);

            int index = 0;
            foreach (DatPolygon p in DOBJ.Polygons)
            {
                foreach (GXDisplayList dl in p.DisplayLists)
                {
                    GXVertex[] verts = decompressor.GetFormattedVertices(dl, p);
                    for (int i = 0; i < verts.Length; i++)
                    {
                        if (verts[i].N != null && verts[i].N.Length == 1)
                        {
                            Vector3 ToTransform = Vector3.TransformPosition(new Vector3(verts[i].Pos.X, verts[i].Pos.Y, verts[i].Pos.Z), Bones.bones[verts[i].N[0]].transform);
                            verts[i].Pos.X = ToTransform.X;
                            verts[i].Pos.Y = ToTransform.Y;
                            verts[i].Pos.Z = ToTransform.Z;
                            Vector3 ToTransformN = Vector3.TransformNormal(new Vector3(verts[i].Nrm.X, verts[i].Nrm.Y, verts[i].Nrm.Z), Bones.bones[verts[i].N[0]].transform);
                            verts[i].Nrm.X = ToTransformN.X;
                            verts[i].Nrm.Y = ToTransformN.Y;
                            verts[i].Nrm.Z = ToTransformN.Z;
                        }
                        // TODO: Transform by attached jobj
                    }
                    Verts.AddRange(verts);

                    List<int> indi = new List<int>();
                    for (int i = 0; i < dl.Indices.Length; i++)
                    {
                        indi.Add(index + i);
                    }
                    switch (dl.PrimitiveType)
                    {
                        case GXPrimitiveType.TriangleStrip: ind.AddRange(TriangleTools.fromTriangleStrip(indi)); break;
                        case GXPrimitiveType.Quads: ind.AddRange(TriangleTools.fromQuad(indi)); break;
                        case GXPrimitiveType.Triangles: ind.AddRange(indi); break;
                        default:
                            Console.WriteLine("Warning: unsupported primitive type " + dl.PrimitiveType.ToString());
                            ind.AddRange(indi);
                            break;
                    }
                    index += indi.Count;
                }
            }

            indices = ind.ToArray();
        }

        public void RecompileVertices(GXVertexDecompressor decompressor, GXVertexCompressor compressor)
        {
            if (VertsToImport != null)
            {

                for (int p = 0; p < VertsToImport.Count; p++)
                {
                    if (p >= DOBJ.Polygons.Count)
                    {
                        MessageBox.Show("Error injecting vertices into DOBJ: Not enough polygons");
                        return;
                    }
                    DatPolygon poly = DOBJ.Polygons[p];
                    List<GXDisplayList> newDL = new List<GXDisplayList>();

                    // maximize vertex groups
                    int size = 3;

                    for (int i = 0; i < VertsToImport[p].Length; i += size)
                    {
                        List<GXVertex> VertList = new List<GXVertex>();
                        for (int j = 0; j < size; j += 3)
                        {
                            VertList.AddRange(new GXVertex[] { VertsToImport[p][i + j + 2], VertsToImport[p][i + j + 1], VertsToImport[p][i + j] });
                        }
                        newDL.Add(compressor.CompressDisplayList(
                            VertList.ToArray(),
                                GXPrimitiveType.Triangles,
                                poly.AttributeGroup));
                    }
                    poly.DisplayLists = newDL;
                }
                VertsToImport = null;
            }
            else
            {
                foreach (DatPolygon p in DOBJ.Polygons)
                {
                    List<GXDisplayList> newDL = new List<GXDisplayList>();
                    if (VertsToImport == null)
                    {
                        foreach (GXDisplayList dl in p.DisplayLists)
                        {
                            newDL.Add(compressor.CompressDisplayList(
                                decompressor.GetFormattedVertices(dl, p),
                                dl.PrimitiveType,
                                p.AttributeGroup));
                        }
                    }
                    p.DisplayLists = newDL;
                }
            }
        }

        public void Render(Camera c, Shader shader)
        {
            shader.SetVector3("BonePosition", BonePosition);

            SetTextureUniforms(shader);

            shader.SetInt("flags", DOBJ.Material.Flags);
            shader.SetBoolToInt("enableSpecular", IsSpecularBitSet());
            shader.SetBoolToInt("enableDiffuseLighting", IsDiffuseLightingBitSet());

            SetRgbaColor(shader, "ambientColor", DOBJ.Material.MaterialColor.AMB);
            SetRgbaColor(shader, "diffuseColor", DOBJ.Material.MaterialColor.DIF);
            SetRgbaColor(shader, "specularColor", DOBJ.Material.MaterialColor.SPC);

            shader.SetFloat("glossiness", DOBJ.Material.MaterialColor.Glossiness);
            shader.SetFloat("transparency", DOBJ.Material.MaterialColor.Transparency);

            if (Checked)
            {
                foreach (var m in renderMeshes)
                {
                    m.SetRenderSettings(DOBJ);

                    if (IsSelected)
                        DrawModelSelection(m, shader, c);
                    else
                        m.Draw(shader, c);
                }
            }
        }

        private bool IsDiffuseLightingBitSet()
        {
            return (DOBJ.Material.Flags & 0x4) > 0;
        }

        private bool IsSpecularBitSet()
        {
            return (DOBJ.Material.Flags & 0x8) > 0;
        }

        private void SetTextureUniforms(Shader shader)
        {
            // Set default values
            shader.SetVector2("diffuseScale", new Vector2(1, 1));
            shader.SetVector2("bumpMapScale", new Vector2(1, 1));
            shader.SetVector2("specularScale", new Vector2(1, 1));

            shader.SetTexture("diffuseTex", Rendering.RenderTools.defaultTex.Id, TextureTarget.Texture2D, 0);
            shader.SetTexture("bumpMapTex", Rendering.RenderTools.defaultTex.Id, TextureTarget.Texture2D, 2);
            shader.SetTexture("specularTex", Rendering.RenderTools.defaultTex.Id, TextureTarget.Texture2D, 3);

            bool hasDiffuse = false;
            bool hasBumpMap = false;
            bool hasSphere = false;
            bool hasSpecular = false;

            foreach (var renderTex in renderTextures)
            {
                uint type = GetTextureType(renderTex);
                if (Enum.IsDefined(typeof(TextureFlag), type))
                {
                    switch ((TextureFlag)type)
                    {
                        default:
                            break;
                        case TextureFlag.BumpMap:
                            hasBumpMap = true;
                            SetBumpMapTexUniforms(shader, renderTex);
                            break;
                    }
                }

                if (IsSphereBitSet(renderTex))
                    hasSphere = true;

                if (IsDiffuseBitSet(renderTex))
                {
                    hasDiffuse = true;
                    SetDiffuseTexUniforms(shader, renderTex);
                }

                if (IsSpecularBitSet(renderTex))
                {
                    hasSpecular = true;
                    SetSpecularTexUniforms(shader, renderTex);
                }
            }

            shader.SetBoolToInt("hasDiffuse", hasDiffuse);
            shader.SetBoolToInt("hasBumpMap", hasBumpMap);
            shader.SetBoolToInt("hasSpecular", hasSpecular);
            shader.SetBoolToInt("hasSphere", hasSphere);
        }

        private static bool IsDiffuseBitSet(MeleeRenderTexture renderTex)
        {
            bool diffuseBit = (renderTex.Flag & (uint)TextureFlag.Diffuse) > 0;
            bool unk4Bit = (renderTex.Flag & (uint)TextureFlag.Unk4) > 0;
            return diffuseBit || unk4Bit;
        }

        private static bool IsSpecularBitSet(MeleeRenderTexture renderTex)
        {
            return (renderTex.Flag & (uint)TextureFlag.Specular) > 0;
        }

        private static bool IsSphereBitSet(MeleeRenderTexture renderTex)
        {
            return (renderTex.Flag & (uint)TextureFlag.Sphere) > 0;
        }

        private static void SetSphereTexUniforms(Shader shader, MeleeRenderTexture renderTex)
        {
            shader.SetVector2("sphereScale", new Vector2(renderTex.WScale, renderTex.HScale));
            shader.SetTexture("sphereTex", renderTex.texture.Id, TextureTarget.Texture2D, 1);
        }

        private static void SetBumpMapTexUniforms(Shader shader, MeleeRenderTexture renderTex)
        {
            shader.SetVector2("bumpMapTexScale", new Vector2(renderTex.WScale, renderTex.HScale));
            shader.SetInt("bumpMapWidth", renderTex.texture.Width);
            shader.SetInt("bumpMapHeight", renderTex.texture.Height);
            shader.SetTexture("bumpMapTex", renderTex.texture.Id, TextureTarget.Texture2D, 2);
        }

        private static void SetDiffuseTexUniforms(Shader shader, MeleeRenderTexture renderTex)
        {
            shader.SetVector2("diffuseScale", new Vector2(renderTex.WScale, renderTex.HScale));
            shader.SetTexture("diffuseTex", renderTex.texture.Id, TextureTarget.Texture2D, 0);
        }

        private static void SetSpecularTexUniforms(Shader shader, MeleeRenderTexture renderTex)
        {
            shader.SetVector2("specularScale", new Vector2(renderTex.WScale, renderTex.HScale));
            shader.SetTexture("specularTex", renderTex.texture.Id, TextureTarget.Texture2D, 3);
        }

        private static uint GetTextureType(MeleeRenderTexture renderTex)
        {
            return renderTex.Flag & 0xFF;
        }

        private static void DrawModelSelection(MeleeMesh mesh, Shader shader, Camera camera)
        {
            //This part needs to be reworked for proper outline. Currently would make model disappear

            mesh.Draw(shader, camera);

            GL.Enable(EnableCap.StencilTest);
            // use vertex color for wireframe color
            shader.SetInt("colorOverride", 1);
            GL.PolygonMode(MaterialFace.Back, PolygonMode.Line);
            GL.Enable(EnableCap.LineSmooth);
            GL.LineWidth(1.5f);

            mesh.Draw(shader, camera);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            shader.SetInt("colorOverride", 0);

            GL.Enable(EnableCap.DepthTest);
        }

        public void SetRgbaColor(Shader shader, string name, Color color)
        {
            shader.SetVector4(name, SFGraphics.Utils.ColorTools.Vector4FromColor(color));
        }

        public void RefreshRendering()
        {
            RefreshRenderTextures();
            RefreshRenderMeshes();
        }

        private void RefreshRenderMeshes()
        {
            renderMeshes.Clear();
            GXVertexDecompressor decom = new GXVertexDecompressor(GetDatFile().DatFile);

            List<VertexContainer<MeleeVertex>> vertexContainers = new List<VertexContainer<MeleeVertex>>();

            // Each display list can have a different primitive type, so we need to generate a lot of containers.
            foreach (DatPolygon polygon in DOBJ.Polygons)
            {
                foreach (GXDisplayList displayList in polygon.DisplayLists)
                {
                    AddVertexContainer(decom, vertexContainers, polygon, displayList);
                }
            }

            // Combine vertex containers with the same primitive type.
            // The optimization doesn't work properly for all primitive types yet.
            GroupContainersCreateRenderMeshes(vertexContainers);
        }

        private static void AddVertexContainer(GXVertexDecompressor decom, List<VertexContainer<MeleeVertex>> vertexContainers, DatPolygon polygon, GXDisplayList displayList)
        {
            List<MeleeVertex> vertices = new List<MeleeVertex>();
            List<int> vertexIndices = new List<int>();

            for (int i = 0; i < displayList.Indices.Length; i++)
            {
                vertexIndices.Add(i);
            }

            vertices.AddRange(ConvertVerts(decom.GetFormattedVertices(displayList, polygon)));

            PrimitiveType primitiveType = MeleeDatToOpenGL.GetGLPrimitiveType(displayList.PrimitiveType);
            VertexContainer<MeleeVertex> vertexContainer = new VertexContainer<MeleeVertex>(vertices, vertexIndices, primitiveType);
            vertexContainers.Add(vertexContainer);
        }

        private void GroupContainersCreateRenderMeshes(List<VertexContainer<MeleeVertex>> vertexContainers)
        {
            List<VertexContainer<MeleeVertex>> optimizedContainers = MeshBatchUtils.GroupContainersByPrimitiveType(vertexContainers);
            foreach (var container in optimizedContainers)
            {
                MeleeMesh meleeMesh = new MeleeMesh(container.vertices, container.vertexIndices);
                meleeMesh.PrimitiveType = container.primitiveType;
                renderMeshes.Add(meleeMesh);
            }
        }

        public void RefreshRenderTextures()
        {
            renderTextures.Clear();

            foreach (DatTexture t in DOBJ.Material.Textures)
            {
                MeleeRenderTexture tex = new MeleeRenderTexture(t);
                tex.Flag = t.UnkFlags;
                renderTextures.Add(tex);
            }
        }

        public static List<MeleeVertex> ConvertVerts(GXVertex[] Verts)
        {
            List<MeleeVertex> o = new List<MeleeVertex>();
            foreach (GXVertex v in Verts)
            {
                MeleeVertex vert = new MeleeVertex()
                {
                    Pos = new Vector3(v.Pos.X, v.Pos.Y, v.Pos.Z),
                    Nrm = new Vector3(v.Nrm.X, v.Nrm.Y, v.Nrm.Z),
                    Bit = new Vector3(v.Bit.X, v.Bit.Y, v.Bit.Z),
                    Tan = new Vector3(v.Tan.X, v.Tan.Y, v.Tan.Z),
                    Clr = new Vector4(v.CLR0.X, v.CLR0.Y, v.CLR0.Z, v.CLR0.W),
                    UV0 = new Vector2(v.TX0.X, v.TX0.Y),
                };

                if (v.N != null)
                {
                    if (v.N.Length > 0)
                    {
                        vert.Bone.X = v.N[0];
                        vert.Weight.X = v.W[0];
                    }
                    if (v.N.Length > 1)
                    {
                        vert.Bone.Y = v.N[1];
                        vert.Weight.Y = v.W[1];
                    }
                    if (v.N.Length > 2)
                    {
                        vert.Bone.Z = v.N[2];
                        vert.Weight.Z = v.W[2];
                    }
                    if (v.N.Length > 3)
                    {
                        vert.Bone.W = v.N[3];
                        vert.Weight.W = v.W[3];
                    }
                }
                o.Add(vert);
            }
            return o;
        }
    }
}