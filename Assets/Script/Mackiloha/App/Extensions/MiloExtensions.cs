using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Mackiloha;
using Mackiloha.App.Metadata;
using Mackiloha.IO;
using Mackiloha.Milo2;
using Mackiloha.Render;
using GLTFTools;

namespace Mackiloha.App.Extensions
{
    public static class MiloExtensions
    {
        public static int Size(this MiloObject entry) => entry is MiloObjectBytes ? (entry as MiloObjectBytes).Data.Length : -1;

        public static string Extension(this MiloObject entry)
        {
            if (entry == null || !((string)entry.Name).Contains('.')) return "";
            return Path.GetExtension(entry.Name); // Returns .cs
        }

        private static string MakeGenPath(string path, Platform platform)
        {
            var ext = (platform) switch
            {
                Platform.PS2 => "ps2",
                Platform.X360 => "xbox",
                _ => ""
            };

            var dir = Path.GetDirectoryName(path);
            var fileName = $"{Path.GetFileName(path)}_{ext}"; // TODO: Get platform extension from app state

            return Path.Combine(dir, "gen", fileName);
        }

        public static void ExportToGLTF(this MiloObjectDir milo, string path, AppState appState)
        {
            var serializer = appState.GetSerializer();
            var pathDirectory = Path.GetDirectoryName(path);

            var textures = milo.Entries
                .Where(x => "Tex".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase))
                .Select(y => serializer.ReadFromMiloObjectBytes<Tex>(y as MiloObjectBytes))
                .ToList();

            var extTexs = textures.Where(x => x.UseExternal).ToList();
            var miloDir = appState.GetWorkingDirectory().FullPath;

            // Update textures
            foreach (var texture in textures.Where(x => x.UseExternal && x.Bitmap == null)) // TODO: Figure out what UseExternal actually means
            {
                var texPath = Path.Combine(miloDir, MakeGenPath(texture.ExternalPath, appState.SystemInfo.Platform));
                var bitmap = serializer.ReadFromFile<HMXBitmap>(texPath);

                texture.Bitmap = bitmap;
                texture.UseExternal = false;
            }

            var views = milo.Entries
                .Where(x => "View".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase))
                .Select(y => serializer.ReadFromMiloObjectBytes<View>(y as MiloObjectBytes))
                .ToList();

            views.AddRange(milo.Entries
                .Where(x => "Group".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase))
                .Select(y => serializer.ReadFromMiloObjectBytes<Group>(y as MiloObjectBytes))
                .ToList());

            var meshes = milo.Entries
                .Where(x => "Mesh".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase))
                .Select(y => serializer.ReadFromMiloObjectBytes<Mackiloha.Render.Mesh>(y as MiloObjectBytes))
                .Where(z => !string.IsNullOrEmpty(z.Material)) // Don't care about bone meshes for now
                .ToList();

            var trans = milo.Entries
                .Where(x => "Trans".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase))
                .Select(y => serializer.ReadFromMiloObjectBytes<Mackiloha.Render.TransStandalone>(y as MiloObjectBytes))
                .ToList();

            var materials = milo.Entries
                .Where(x => "Mat".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase))
                .Select(y => serializer.ReadFromMiloObjectBytes<Mackiloha.Render.Mat>(y as MiloObjectBytes))
                //.Where(z => z.TextureEntries.Count > 0 && z.TextureEntries.Any(w => !string.IsNullOrEmpty(w.Texture))) // TODO: Idk?
                .ToList();

            var cams = milo.Entries
                .Where(x => "Cam".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase) && appState.SystemInfo.Version <= 10)
                .Select(y => serializer.ReadFromMiloObjectBytes<Cam>(y as MiloObjectBytes))
                .ToList();

            var environs = milo.Entries
                .Where(x => "Environ".Equals(x.Type, StringComparison.CurrentCultureIgnoreCase) && appState.SystemInfo.Version <= 10)
                .Select(y => serializer.ReadFromMiloObjectBytes<Environ>(y as MiloObjectBytes))
                .ToList();

            var miloEntries = textures
                .Union<MiloObject>(views)
                .Union(meshes)
                .Union(trans)
                .Union(materials)
                .Union(cams)
                .Union(environs)
                .ToList();

            var transEntries = miloEntries
                .Where(x => x is ITrans)
                .ToList();

            var drawEntries = miloEntries
                .Where(x => x is IDraw)
                .ToList();

            /* var transforms = milo.Entries
                .Where(x => x.Type.Equals("Trans", StringComparison.CurrentCultureIgnoreCase))
                .Select(y =>
                {
                    var trans = MiloOG.Trans.FromStream(new MemoryStream((y as MiloEntry).Data));
                    trans.Name = y.Name;
                    return trans;
                }).ToList(); */

            var scene = new GLTF()
            {
                Asset = new Asset()
                {
                    Generator = $"Mackiloha v{typeof(MiloExtensions).Assembly.GetName().Version}"
                },
                Images = textures.Select(x => new Image()
                {
                    Name = Path.GetFileNameWithoutExtension(x.Name) + ".png",
                    Uri = Path.GetFileNameWithoutExtension(x.Name) + ".png"
                }).ToArray(),
                Samplers = new Sampler[]
                {
                    new Sampler()
                    {
                        MagFilter = MagFilter.Linear,
                        MinFilter = MinFilter.Nearest,
                        WrapS = WrapMode.Repeat,
                        WrapT = WrapMode.Repeat
                    }
                },
                Scene = 0
            };

            var currentOffset = 0;
            scene.Textures = textures.Select(x => new Texture()
            {
                Name = x.Name,
                Sampler = 0,
                Source = currentOffset++
            }).ToArray();

            var keyIdxPairs = Enumerable.Range(0, textures.Count).ToDictionary(x => textures[x].Name);
            scene.Materials = materials.Select(x => new Material()
            {
                Name = x.Name,
                PbrMetallicRoughness = new PbrMetallicRoughness()
                {
                    BaseColorTexture =
                        // Verify material has texture and exists in milo
                        (x.TextureEntries.Any(w => !string.IsNullOrEmpty(w.Texture))
                            && keyIdxPairs.ContainsKey(x.TextureEntries.First(x => !string.IsNullOrEmpty(x.Texture)).Texture))
                            ? new BaseColorTexture()
                                {
                                    // TODO: Figure out how to map multiple textures to single material
                                    Index = keyIdxPairs[x.TextureEntries.First(y => !string.IsNullOrEmpty(y.Texture)).Texture]
                                }
                            : null,
                    BaseColorFactor = new Vector4<double>(x.BaseColor.R, x.BaseColor.G, x.BaseColor.B, x.BaseColor.A),
                    MetallicFactor = (x.TextureEntries.Any(w => !string.IsNullOrEmpty(w.Texture)) && x.TextureEntries.First(y => !string.IsNullOrEmpty(y.Texture)).Unknown2 == 2) ? 1 : 0,
                    RoughnessFactor = (x.TextureEntries.Any(w => !string.IsNullOrEmpty(w.Texture)) && x.TextureEntries.First(y => !string.IsNullOrEmpty(y.Texture)).Unknown2 == 2) ? 0 : 1
                },
                EmissiveFactor = new Vector3<double>(),
                AlphaMode = AlphaMode.Mask, // x.Blend == BlendFactor.One ? AlphaMode.Blend : AlphaMode.Opaque,
                DoubleSided = true
            }).ToArray();

            if (!Directory.Exists(pathDirectory))
            {
                // Create directory
                Directory.CreateDirectory(pathDirectory);
            }

            // Saves textures
            for (int i = 0; i < textures.Count; i++)
            {
                textures[i].Bitmap.SaveAs(serializer.Info,
                    Path.Combine(pathDirectory, scene.Images[i].Uri));
            }

            var accessors = new List<Accessor>();
            var sceneMeshes = new List<GLTFTools.Mesh>();
            
            int bufferSize12 = meshes.Select(x => x.Vertices.Count * 12 * 2).Sum(); // Verts + norms
            int bufferSize8 = meshes.Select(x => x.Vertices.Count * 8).Sum(); // UV
            int bufferSize4 = meshes.Select(x => x.Faces.Count * 6).Sum(); // Faces
            if (bufferSize4 % 4 != 0) bufferSize4 += 4 - (bufferSize4 % 4);

            scene.Buffers = new GLTFTools.Buffer[]
            {
                new GLTFTools.Buffer()
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    ByteLength = (bufferSize4 + bufferSize8 + bufferSize12),
                    Uri = Path.GetFileNameWithoutExtension(path) + ".bin"
                }
            };

            scene.BufferViews = new BufferView[]
            {
                new BufferView()
                {
                    Name = "vertsAndNorms",
                    ByteLength = bufferSize12,
                    ByteOffset = 0,
                    ByteStride = 12
                },
                new BufferView()
                {
                    Name = "uvs",
                    ByteLength = bufferSize8,
                    ByteOffset = bufferSize12,
                    ByteStride = 8
                },
                new BufferView()
                {
                    Name = "faces",
                    ByteLength = bufferSize4,
                    ByteOffset = bufferSize12 + bufferSize8,
                    ByteStride = null
                }
            };
            
            int buffer12Offset = scene.BufferViews[0].ByteOffset.Value;
            int buffer8Offset = scene.BufferViews[1].ByteOffset.Value;
            int buffer4Offset = scene.BufferViews[2].ByteOffset.Value;

            var bw = new BinaryWriter(new MemoryStream(new byte[bufferSize12 + bufferSize8 + bufferSize4]));
            Dictionary<string, int> meshIndex = new Dictionary<string, int>();
            currentOffset = 0;

            keyIdxPairs = Enumerable
                .Range(0, materials.Count)
                .ToDictionary(x => materials[x].Name);

            foreach (var mesh in meshes)
            {
                if (mesh.Vertices.Count <= 0 || mesh.Faces.Count <= 0) continue;
                meshIndex.Add(mesh.Name, currentOffset++);

                // Finds related material + texture
                var mat = materials.First(x => ((string)x.Name).Equals(mesh.Material, StringComparison.CurrentCultureIgnoreCase));

                sceneMeshes.Add(new GLTFTools.Mesh()
                {
                    Name = mesh.Name,
                    Primitives = new MeshPrimitive[]
                    {
                        new MeshPrimitive()
                        {
                            Attributes = new MeshPrimitiveAttributes()
                            {
                                Position = accessors.Count,
                                Normal = accessors.Count + 1,
                                TextureCoordinate0 = accessors.Count + 2
                            },
                            Indices = accessors.Count + 3,
                            Material = keyIdxPairs[mesh.Material],
                            Mode = RenderMode.Triangles
                        }
                    }
                });
                
                // Vertices
                accessors.Add(new Accessor()
                {
                    Name = mesh.Name + "_positions",
                    ComponentType = ComponentType.Float,
                    Count = mesh.Vertices.Count,
                    Min = new double[]
                    {
                        mesh.Vertices.Select(x => x.X).Min(),
                        mesh.Vertices.Select(x => x.Y).Min(),
                        mesh.Vertices.Select(x => x.Z).Min()
                    },
                    Max = new double[]
                    {
                        mesh.Vertices.Select(x => x.X).Max(),
                        mesh.Vertices.Select(x => x.Y).Max(),
                        mesh.Vertices.Select(x => x.Z).Max()
                    },
                    Type = GLType.Vector3,
                    BufferView = 0,
                    ByteOffset = buffer12Offset - scene.BufferViews[0].ByteOffset.Value
                });
                bw.BaseStream.Seek(buffer12Offset, SeekOrigin.Begin);
                foreach (var vert in mesh.Vertices)
                {
                    bw.Write(vert.X);
                    bw.Write(vert.Y);
                    bw.Write(vert.Z);
                }
                buffer12Offset = (int)bw.BaseStream.Position;

                // Normals
                accessors.Add(new Accessor()
                {
                    Name = mesh.Name + "_normals",
                    ComponentType = ComponentType.Float,
                    Count = mesh.Vertices.Count,
                    Min = new double[]
                    {
                        mesh.Vertices.Select(x => x.NormalX).Min(),
                        mesh.Vertices.Select(x => x.NormalY).Min(),
                        mesh.Vertices.Select(x => x.NormalZ).Min()
                    },
                    Max = new double[]
                    {
                        mesh.Vertices.Select(x => x.NormalX).Max(),
                        mesh.Vertices.Select(x => x.NormalY).Max(),
                        mesh.Vertices.Select(x => x.NormalZ).Max()
                    },
                    Type = GLType.Vector3,
                    BufferView = 0,
                    ByteOffset = buffer12Offset - scene.BufferViews[0].ByteOffset.Value
                });
                bw.BaseStream.Seek(buffer12Offset, SeekOrigin.Begin);
                foreach (var vert in mesh.Vertices)
                {
                    bw.Write(vert.NormalX);
                    bw.Write(vert.NormalY);
                    bw.Write(vert.NormalZ);
                }
                buffer12Offset = (int)bw.BaseStream.Position;

                // UV coordinates
                accessors.Add(new Accessor()
                {
                    Name = mesh.Name + "_texcoords",
                    ComponentType = ComponentType.Float,
                    Count = mesh.Vertices.Count,
                    Min = new double[]
                    {
                        mesh.Vertices.Select(x => x.U).Min(),
                        mesh.Vertices.Select(x => x.V).Min()
                    },
                    Max = new double[]
                    {
                        mesh.Vertices.Select(x => x.U).Max(),
                        mesh.Vertices.Select(x => x.V).Max()
                    },
                    Type = GLType.Vector2,
                    BufferView = 1,
                    ByteOffset = buffer8Offset - scene.BufferViews[1].ByteOffset.Value
                });
                bw.BaseStream.Seek(buffer8Offset, SeekOrigin.Begin);
                foreach (var vert in mesh.Vertices)
                {
                    bw.Write(vert.U);
                    bw.Write(vert.V);
                }
                buffer8Offset = (int)bw.BaseStream.Position;

                // Faces
                accessors.Add(new Accessor()
                {
                    Name = mesh.Name + "_indicies",
                    ComponentType = ComponentType.UnsignedShort,
                    Count = mesh.Faces.Count * 3,
                    Min = new double[]
                    {
                        mesh.Faces.SelectMany(x => new [] { x.V1, x.V2, x.V3 }).Min()
                    },
                    Max = new double[]
                    {
                        mesh.Faces.SelectMany(x => new [] { x.V1, x.V2, x.V3 }).Max()
                    },
                    Type = GLType.Scalar,
                    BufferView = 2,
                    ByteOffset = buffer4Offset - scene.BufferViews[2].ByteOffset.Value
                });
                bw.BaseStream.Seek(buffer4Offset, SeekOrigin.Begin);
                foreach (var face in mesh.Faces)
                {
                    bw.Write(face.V1);
                    bw.Write(face.V2);
                    bw.Write(face.V3);
                }
                buffer4Offset = (int)bw.BaseStream.Position;
            }

            scene.Accessors = accessors.ToArray();
            scene.Meshes = sceneMeshes.ToArray();

            var nodes = new List<Node>();
            var nodeIndex = new Dictionary<string, int>();

            /* // TODO: Make milo objects with transforms data
            MiloOG.AbstractEntry GetAbstractEntry(string name)
            {
                var entry = milo.Entries.FirstOrDefault(x => x.Name == name);
                if (entry == null) return null;

                switch (entry.Type)
                {
                    case "Mesh":
                        return meshes.First(y => y.Name == entry.Name);
                    case "Trans":
                        return transforms.First(y => y.Name == entry.Name);
                    case "View":
                        return views.First(y => y.Name == entry.Name);
                    default:
                        return null;
                }
            } */

            /* Matrix4<float>? GetTransform(string transform)
            {
                var transEntry = milo.Entries.FirstOrDefault(y => y.Name == transform);
                if (transEntry == null) return null;

                switch (transEntry.Type)
                {
                    case "Mesh":
                        var mesh = meshes.First(y => y.Name == transEntry.Name);
                        return mesh.Mat2.ToGLMatrix();
                    case "Trans":
                        var trans = transforms.First(y => y.Name == transEntry.Name);
                        return trans.Mat2.ToGLMatrix();
                    case "View":
                        var view2 = views.First(y => y.Name == transEntry.Name);
                        return view2.Mat2.ToGLMatrix();
                    default:
                        return null;
                }
            } */

            /* string GetTransformName(MiloOG.AbstractEntry entry)
            {
                switch (entry.Type)
                {
                    case "Mesh":
                        var mesh = meshes.First(y => y.Name == entry.Name);
                        return mesh.Transform;
                    case "Trans":
                        var trans = transforms.First(y => y.Name == entry.Name);
                        return trans.Name;
                    case "View":
                        var view = views.First(y => y.Name == entry.Name);
                        return view.Transform;
                    default:
                        return null;
                }
            } */

            var shadowRegex = new System.Text.RegularExpressions.Regex("shadow[^.]*.mesh$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var children = drawEntries
                .Where(w => w is ITrans) // Use trans collection?
                .Select(x => new
                {
                    Name = (string)x.Name,
                    Trans = (string)(x as ITrans).Transform
                })
                .Where(y => !string.IsNullOrEmpty(y.Trans))
                .GroupBy(z => z.Trans)
                .ToDictionary(g => g.Key, g => g.Select(w => w.Name)
                    .OrderBy(s => s)
                .Where(x => !shadowRegex.IsMatch(x)) // Removes shadow meshes for now
                .ToList());

            /* foreach (var entry in meshes.Union<MiloObject>(views)) // TODO: Union w/ trans
            {
                var transName = (entry as Mackiloha.Render.Interfaces.ITrans)
                    .Trans
                    .Transform;
                
                if (!children.ContainsKey(transName))
                    children.Add(transName, new List<string>(new string[] { entry.Name }));
                else if (!children[transName].Contains(entry.Name))
                    children[transName].Add(entry.Name);
            } */
            
            var rootIndex = new List<int>();
            foreach (var key in children.Keys)
            {
                rootIndex.Add(nodes.Count);
                var transEntry = transEntries.FirstOrDefault(x => x.Name == key) as ITrans;
                
                var node = new Node()
                {
                    Name = "Root_" + key,
                    Mesh = meshIndex.ContainsKey(key) ? (int?)meshIndex[key] : null,
                    Matrix = ToGLMatrix((transEntry != null) ? transEntry.Mat2 : Matrix4.Identity()),
                    Children = Enumerable.Range(nodes.Count + 1, children[key].Count).ToArray()
                };
                nodes.Add(node);

                foreach (var child in children[key])
                {
                    var subEntry = drawEntries.First(x => x.Name == child);

                    var subNode = new Node()
                    {
                        Name = subEntry.Name,
                        Mesh = meshIndex.ContainsKey(subEntry.Name) ? (int?)meshIndex[subEntry.Name] : null,
                        //Matrix = ToGLMatrix(subEntry.Mat1)
                    };
                    nodeIndex.Add(child, rootIndex.Last());
                    nodes.Add(subNode);
                }
            }
            
            int CreateNode(string name) // Returns index of node
            {
                if (nodeIndex.ContainsKey(name))
                    return nodeIndex[name];
                
                var entry = drawEntries.First(x => x.Name == name) as IDraw;
                var transformEntry = drawEntries.First(x => x.Name == (entry as ITrans).Transform);
                List<string> subNodes = entry.Drawables.Select(x => (string)x).ToList();

                var node = new Node()
                {
                    Name = name,
                    Mesh = meshIndex.ContainsKey(name) ? (int?)meshIndex[name] : null,
                    Matrix = ToGLMatrix((entry as ITrans).Mat1),
                    //Matrix = GetTransform(entry.Transform),
                    Children = (subNodes.Count > 0) ? subNodes.Select(x => CreateNode(x)).ToArray() : null
                };

                nodeIndex.Add(name, nodes.Count);
                nodes.Add(node);
                return nodeIndex[name];
            }
            
            // foreach (var n in meshes.Union<MiloOG.AbstractEntry>(views).Union<MiloOG.AbstractEntry>(transforms)) CreateNode(n.Name);
            // 
            // scene.Scene = 0;
            // scene.Scenes = new Scene[] { new Scene() { Nodes = Enumerable.Range(0, nodes.Count).ToArray() } };

            
            // foreach (var view in views) CreateNode(view.Name);
            // 
            // // Finds root node
            // var childrenNodes = nodes.SelectMany(x => x.Children ?? new int[0]).Distinct();
            // var parentNodes = Enumerable.Range(0, nodes.Count);
            // var rootIdx = parentNodes.Except(childrenNodes).Single();
            // 
            // scene.Scene = 0;
            // scene.Scenes = new Scene[] { new Scene() { Nodes = new int[] { rootIdx } } };

            List<string> GetAllSubs(MiloObject entry)
            {
                List<string> subsEntriesNames = (entry as IDraw).Drawables
                    .Select(x => (string)x)
                    .ToList();

                var subEntries = subsEntriesNames
                    .Select(x => drawEntries.FirstOrDefault(y => y.Name == x))
                    .Where(y => y != null)
                    .ToList();

                foreach (var subEntry in subEntries)
                    subsEntriesNames.AddRange(GetAllSubs(subEntry));

                return subsEntriesNames;
            }

            scene.Scene = 0;
            //scene.Scenes = new Scene[] { new Scene() { Nodes = rootIndex.ToArray() } };

            scene.Scenes = views
                .Select(x => new Scene()
                {
                    Nodes = GetAllSubs(x)
                        .Select(y => nodeIndex.ContainsKey(y) ? nodeIndex[y] : -1)
                        .Where(z => z != -1)
                        .Distinct()
                        .ToArray()
                })
                .OrderByDescending(x => x.Nodes.Length)
                .ToArray();

            if (scene.Scenes.Length <= 0)
            {
                // Create scene from root notes
                scene.Scenes = new[]
                {
                    new Scene()
                    {
                        Nodes = Enumerable
                            .Range(0, nodes.Count)
                            .Zip(nodes, (idx, node) => (idx, node))
                            .Where(x => x.node.Name.StartsWith("Root_"))
                            .Select(x => x.idx).ToArray()
                    }
                };
            }

            scene.Nodes = nodes.ToArray();
            
            using (var fs = File.OpenWrite(Path.Combine(pathDirectory, scene.Buffers[0].Uri)))
            {
                // Copies stream to file
                bw.BaseStream.Seek(0, SeekOrigin.Begin);
                bw.BaseStream.CopyTo(fs);
                bw.Dispose();
            }

            var json = scene.ToJson();
            File.WriteAllText(path, json);
        }

        public static Matrix4<float> ToGLMatrix(this Matrix4 miloMatrix) =>
            new Matrix4<float>()
            {
                // Swaps x and y values (columns 2 and 3)
                M11 = -miloMatrix.M11,
                M12 = miloMatrix.M13,
                M13 = miloMatrix.M12,
                M14 = miloMatrix.M14,

                M21 = -miloMatrix.M21,
                M22 = miloMatrix.M23,
                M23 = miloMatrix.M22,
                M24 = miloMatrix.M24,

                M31 = -miloMatrix.M31,
                M32 = miloMatrix.M33,
                M33 = miloMatrix.M32,
                M34 = miloMatrix.M34,

                M41 = -miloMatrix.M41,
                M42 = miloMatrix.M43,
                M43 = miloMatrix.M42,
                M44 = miloMatrix.M44
            };

        public static Matrix4<float> ToGLMatrix(this Matrix miloMatrix) =>
            new Matrix4<float>()
            {
                // Swaps x and y values (columns 2 and 3)
                M11 = -miloMatrix.M11,
                M12 = miloMatrix.M13,
                M13 = miloMatrix.M12,
                M14 = miloMatrix.M14,

                M21 = -miloMatrix.M21,
                M22 = miloMatrix.M23,
                M23 = miloMatrix.M22,
                M24 = miloMatrix.M24,

                M31 = -miloMatrix.M31,
                M32 = miloMatrix.M33,
                M33 = miloMatrix.M32,
                M34 = miloMatrix.M34,

                M41 = -miloMatrix.M41,
                M42 = miloMatrix.M43,
                M43 = miloMatrix.M42,
                M44 = miloMatrix.M44
            };

        public static Matrix4 MatrixFromStringHex(string hex, bool bigEndian = false)
        {
            var data = hex
                .Split(' ')
                .Select(x => Convert.ToByte(x, 16))
                .ToArray();

            if (bigEndian)
            {
                for (int i = 0; i < data.Length; i += 4)
                    Array.Reverse(data, i, 4);
            }

            return new Matrix4()
            {
                M11 = BitConverter.ToSingle(data,  0),
                M12 = BitConverter.ToSingle(data,  4),
                M13 = BitConverter.ToSingle(data,  8),

                M21 = BitConverter.ToSingle(data, 12),
                M22 = BitConverter.ToSingle(data, 16),
                M23 = BitConverter.ToSingle(data, 20),

                M31 = BitConverter.ToSingle(data, 24),
                M32 = BitConverter.ToSingle(data, 28),
                M33 = BitConverter.ToSingle(data, 32),

                M41 = BitConverter.ToSingle(data, 36),
                M42 = BitConverter.ToSingle(data, 40),
                M43 = BitConverter.ToSingle(data, 44),
                M44 = 1.0f
            };
        }

        public static void ExtractToDirectory(this MiloObjectDir milo, string path, bool convertTextures, AppState state)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            /*
            var entriesByType = milo.Entries
                .GroupBy(x => x.Type)
                .ToDictionary(x => Path.Combine(path, x.Key), y => y.ToList())
                .ToList();*/

            var miloEntries = new List<MiloObject>();

            if (milo.Extras.ContainsKey("DirectoryEntry"))
            {
                var dirEntry = milo.Extras["DirectoryEntry"] as MiloObjectBytes;
                if (dirEntry != null)
                {
                    if (string.IsNullOrWhiteSpace(dirEntry.Name) && !string.IsNullOrWhiteSpace(dirEntry.Type))
                    {
                        // TODO: Handle differently?
                        // Some directory names are empty for whatever reason
                        dirEntry.Name = Path.GetFileNameWithoutExtension(milo.Name);
                    }
                    miloEntries.Add(dirEntry);

                    // Create meta
                    var dirMeta = new DirectoryMeta()
                    {
                        Name = dirEntry.Name,
                        Type = dirEntry.Type
                    };

                    // Write meta
                    var metaPath = Path.Combine(path, "rnd.json");
                    var metaJson = JsonSerializer.Serialize(dirMeta, state.JsonSerializerOptions);
                    File.WriteAllText(metaPath, metaJson);
                }
            }
            miloEntries.AddRange(milo.Entries);

            // Sanitize paths
            foreach (var entry in miloEntries)
            {
                // TODO: Remove \t and other escape characters
                entry.Name = entry.Name.Trim();
            }

            var typeDirs = miloEntries
                .Select(x => Path.Combine(path, x.Type))
                .Distinct()
                .ToList();

            foreach (var dir in typeDirs)
            {
                if (Directory.Exists(dir))
                    continue;

                Directory.CreateDirectory(dir);
            }

            // Filter out textures if converting
            var entries = miloEntries
                .Where(x => !convertTextures || x.Type != "Tex")
                .ToList();

            foreach (var entry in entries)
            {
                // TODO: Sanitize file name
                var filePath = Path.Combine(path, entry.Type, entry.Name);
                if (entry is MiloObjectBytes miloBytes)
                {
                    File.WriteAllBytes(filePath, miloBytes.Data);
                }
            }

            if (!convertTextures)
                return;

            var serializer = state.GetSerializer();

            var textureEntries = miloEntries
                .Where(x => x.Type == "Tex")
                .Select(x => x is Tex ? x as Tex : serializer.ReadFromMiloObjectBytes<Tex>(x as MiloObjectBytes))
                .ToList();

            // Update textures
            foreach (var texture in textureEntries.Where(x => x.UseExternal))
            {
                if (texture?.Bitmap?.RawData?.Length > 0)
                {
                    // Use already embedded texture instead
                    texture.UseExternal = false;
                    continue;
                }

                try
                {
                    var texPath = Path.Combine(state.GetWorkingDirectory().FullPath, MakeGenPath(texture.ExternalPath, state.SystemInfo.Platform));
                    var bitmap = serializer.ReadFromFile<HMXBitmap>(texPath);

                    texture.Bitmap = bitmap;
                    texture.UseExternal = false;
                }
                catch
                {

                }
            }

            var defaultMeta = TexMeta.DefaultFor(state.SystemInfo.Platform);
            foreach (var texEntry in textureEntries)
            {
                var entryName = Path.GetFileNameWithoutExtension(texEntry.Name);

                // TODO: Skip?
                texEntry.UseExternal = false;
                if (texEntry.Bitmap is null) continue; // Skip for now

                if (texEntry.UseExternal)
                    throw new NotSupportedException("Can't extract external textures yet");

                // Saves png
                var pngName = $"{entryName}.png";
                var pngPath = Path.Combine(path, texEntry.Type, pngName);
                texEntry.Bitmap.SaveAs(state.SystemInfo, pngPath);

                // Saves metadata
                var metaName = $"{entryName}.meta.json";
                var metaPath = Path.Combine(path, texEntry.Type, metaName);

                var meta = new TexMeta()
                {
                    Encoding = (texEntry.Bitmap.Encoding, state.SystemInfo.Platform) switch
                    {
                        (var enc, _) when enc == 3 => TexEncoding.Bitmap,
                        (var enc, var plat) when enc == 8 && plat == Platform.XBOX => TexEncoding.Bitmap,
                        (var enc, _) when enc == 8 => TexEncoding.DXT1,
                        (var enc, _) when enc == 24 => TexEncoding.DXT5,
                        (var enc, _) when enc == 32 => TexEncoding.ATI2,
                        _ => (TexEncoding?)null
                    },
                    MipMaps = texEntry.Bitmap.MipMaps > 0
                };

                if ((meta.Encoding == null || meta.Encoding == defaultMeta.Encoding) && meta.MipMaps == defaultMeta.MipMaps)
                    continue;

                var metaJson = JsonSerializer.Serialize(meta, state.JsonSerializerOptions);
                File.WriteAllText(metaPath, metaJson);
            }
        }

        /*
        public static void WriteTree(this MiloObjectDir milo, string path)
        {
            using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                foreach (var view in milo.Entries.Where(x => x.Type == "View"))
                    WriteTree(milo, view.Name, sw, 0);
            }
        }

        public static void WriteTree2(this MiloFile milo, string path)
        {
            MiloOG.AbstractEntry GetOGEntry(string name)
            {
                var e = milo.Entries.First(x => x.Name == name) as MiloEntry;

                switch (e.Type)
                {
                    case "Mesh":
                        var mesh = MiloOG.Mesh.FromStream(new MemoryStream(e.Data));
                        mesh.Name = e.Name;
                        return mesh;
                    case "Trans":
                        var trans = MiloOG.Trans.FromStream(new MemoryStream(e.Data));
                        trans.Name = e.Name;
                        return trans;
                    case "View":
                        var view = MiloOG.View.FromStream(new MemoryStream(e.Data));
                        view.Name = e.Name;
                        return view;
                    default:
                        return null;
                }
            }

            string GetTransformName(string name)
            {
                var e = milo.Entries.First(x => x.Name == name) as MiloEntry;

                switch (e.Type)
                {
                    case "Mesh":
                        var mesh = MiloOG.Mesh.FromStream(new MemoryStream(e.Data));
                        mesh.Name = e.Name;
                        return mesh.Transform;
                    case "Trans":
                        var trans = MiloOG.Trans.FromStream(new MemoryStream(e.Data));
                        trans.Name = e.Name;
                        return trans.Name;
                    case "View":
                        var view = MiloOG.View.FromStream(new MemoryStream(e.Data));
                        view.Name = e.Name;
                        return view.Transform;
                    default:
                        return null;
                }
            }

            using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
            {
                var children = new Dictionary<string, List<string>>();
                
                foreach (var entry in milo.Entries)
                {
                    // if (!children.ContainsKey(entry.Name))
                    //     children.Add(entry.Name, new List<string>());

                    var trans = GetTransformName(entry.Name);
                    if (trans == null || trans == entry.Name) continue;

                    if (!children.ContainsKey(trans))
                        children.Add(trans, new List<string>(new string[] { entry.Name }));
                    else if (!children[trans].Contains(entry.Name))
                        children[trans].Add(entry.Name);

                    //WriteTree(milo, view.Name, sw, 0);
                }
            }
        }

        private static void WriteTree(MiloFile milo, string entry, StreamWriter sw, int depth, bool bone = false)
        {
            MiloOG.AbstractEntry GetOGEntry(string name)
            {
                var e = milo.Entries.First(x => x.Name == name) as MiloEntry;

                switch (e.Type)
                {
                    case "Mesh":
                        var mesh = MiloOG.Mesh.FromStream(new MemoryStream(e.Data));
                        mesh.Name = e.Name;
                        return mesh;
                    case "Trans":
                        var trans = MiloOG.Trans.FromStream(new MemoryStream(e.Data));
                        trans.Name = e.Name;
                        return trans;
                    case "View":
                        var view = MiloOG.View.FromStream(new MemoryStream(e.Data));
                        view.Name = e.Name;
                        return view;
                    default:
                        return null;
                }
            }

            dynamic transEntry = GetOGEntry(entry);
            List<string> subBones = transEntry.Meshes;
            List<string> subEntries = transEntry.Meshes;
            string type = bone ? "Bone" : "Mesh";

            sw.WriteLine($"{new string('\t', depth)}{type}: {transEntry.Name} ({transEntry.Transform})");

            foreach (var sub in subBones)
            {
                WriteTree(milo, sub, sw, depth + 1, true);
            }

            foreach (var sub in subEntries)
            {
                WriteTree(milo, sub, sw, depth + 1);
            }
        }*/
    }
}
