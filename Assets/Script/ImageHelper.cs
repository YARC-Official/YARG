using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TrombLoader.Helpers
{
    /// <summary>
    /// Utility class for loading Texture2D and Sprite objects.
    /// </summary>
    public static class ImageHelper
    {
        /// <summary>
        /// Load a texture, given an array of bytes.
        /// </summary>
        /// <param name="bytes">The bytes that make up the image.</param>
        /// <returns>A Texture2D, or null if the array is empty.</returns>
        public static Texture2D LoadTextureRaw(byte[] bytes)
        {
            if (bytes.Count() > 0)
            {
                Texture2D Tex2D = new Texture2D(2, 2);
                if (Tex2D.LoadImage(bytes))
                    return Tex2D;
            }
            return null;
        }

        /// <summary>
        /// Load a texture, given a file path.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <returns>A Texture2D, or null if the file cannot be found.</returns>
        public static Texture2D LoadTextureFromFile(string path)
        {
            if (File.Exists(path)) return LoadTextureRaw(File.ReadAllBytes(path));

            return null;
        }

        /// <summary>
        /// Load a texture from an embedded resource, given the path to the resource.
        /// </summary>
        /// <param name="resourcePath">Path to the image, decimal-delimited starting from the root namespace.</param>
        /// <returns>A Texture2D</returns>
        public static Texture2D LoadTextureFromResources(string resourcePath)
        {
            return LoadTextureRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath));
        }

        /// <summary>
        /// Load a Sprite, given an array of bytes.
        /// </summary>
        /// <param name="image">The bytes that make up the image.</param>
        /// <param name="pixelsPerUnit">The number of pixels per Unity unit.</param>
        /// <returns>A Sprite.</returns>
        public static Sprite LoadSpriteRaw(byte[] image, float pixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureRaw(image), pixelsPerUnit);
        }

        /// <summary>
        /// Load a Sprite, given a Texture2D.
        /// </summary>
        /// <param name="texture">Texture2D to create the Sprite from.</param>
        /// <param name="pixelsPerUnit">The number of pixels per Unity unit.</param>
        /// <returns>A Sprite, or null if the Texture2D is null.</returns>
        public static Sprite LoadSpriteFromTexture(Texture2D texture, float pixelsPerUnit = 100.0f)
        {
            if (texture) return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            return null;
        }

        /// <summary>
        /// Load a Sprite, given a file path.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="pixelsPerUnit">The number of pixels per Unity unit.</param>
        /// <returns>A Sprite, or null if the file cannot be found.</returns>
        public static Sprite LoadSpriteFromFile(string path, float pixelsPerUnit = 100.0f)
        {
            return LoadSpriteFromTexture(LoadTextureFromFile(path), pixelsPerUnit);
        }

        /// <summary>
        /// Load a texture from an embedded resource, given the path to the resource.
        /// </summary>
        /// <param name="resourcePath">Path to the image, decimal-delimited starting from the root namespace.</param>
        /// <param name="pixelsPerUnit">The number of pixels per Unity unit.</param>
        /// <returns>A Sprite.</returns>
        public static Sprite LoadSpriteFromResources(string resourcePath, float pixelsPerUnit = 100.0f)
        {
            return LoadSpriteRaw(GetResource(Assembly.GetCallingAssembly(), resourcePath), pixelsPerUnit);
        }

        private static byte[] GetResource(Assembly asm, string resourcePath)
        {
            Stream stream = asm.GetManifestResourceStream(resourcePath);
            byte[] data = new byte[stream.Length];
            stream.Read(data, 0, (int)stream.Length);
            return data;
        }
    }
}
