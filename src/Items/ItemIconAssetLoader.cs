using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace ErenshorCraftingExpanded
{
    public sealed class ItemIconLoadStatus
    {
        public string ItemId = string.Empty;
        public string RelativePath = string.Empty;
        public bool Loaded;
        public string ResolvedPath = string.Empty;
        public string FailureReason = string.Empty;
    }

    internal static class ItemIconAssetLoader
    {
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags AllStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const string IconNamePrefix = "ErenshorCraftingExpanded.Icon::";
        private static readonly Dictionary<string, object> SpritesByPath = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, ItemIconLoadStatus> StatusByItem = new Dictionary<string, ItemIconLoadStatus>(StringComparer.Ordinal);

        internal static int LoadedCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<string, ItemIconLoadStatus> pair in StatusByItem) if (pair.Value != null && pair.Value.Loaded) count++;
                return count;
            }
        }

        internal static ItemIconLoadStatus Status(string itemId)
        {
            ItemIconLoadStatus status;
            return itemId != null && StatusByItem.TryGetValue(itemId, out status) ? status : null;
        }

        internal static bool TryApply(object item, string itemId, string relativePath, out string failure)
        {
            failure = string.Empty;
            ItemIconLoadStatus status = new ItemIconLoadStatus();
            status.ItemId = itemId ?? string.Empty;
            status.RelativePath = relativePath ?? string.Empty;
            StatusByItem[status.ItemId] = status;
            if (item == null || string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(relativePath))
            { status.FailureReason = "icon inputs unavailable"; failure = status.FailureReason; return false; }

            try
            {
                FieldInfo existingIconField = item.GetType().GetField("ItemIcon", AllInstance);
                object existingIcon = existingIconField == null ? null : existingIconField.GetValue(item);
                if (existingIcon != null && string.Equals(ReadObjectName(existingIcon), IconNamePrefix + itemId, StringComparison.Ordinal))
                {
                    status.Loaded = true;
                    status.ResolvedPath = ResolveAssetPath(relativePath);
                    return true;
                }
            }
            catch { }

            string path = ResolveAssetPath(relativePath);
            status.ResolvedPath = path ?? string.Empty;
            if (string.IsNullOrEmpty(path))
            { status.FailureReason = "icon asset not found; native donor icon retained"; failure = status.FailureReason; return false; }

            object sprite;
            if (!SpritesByPath.TryGetValue(path, out sprite) || sprite == null)
            {
                string loadFailure;
                sprite = LoadSprite(path, out loadFailure);
                if (sprite == null)
                { status.FailureReason = loadFailure + "; native donor icon retained"; failure = status.FailureReason; return false; }
                SpritesByPath[path] = sprite;
            }

            try
            {
                FieldInfo iconField = item.GetType().GetField("ItemIcon", AllInstance);
                if (iconField == null || !iconField.FieldType.IsInstanceOfType(sprite))
                { status.FailureReason = "live ItemIcon field is unavailable/incompatible; native donor icon retained"; failure = status.FailureReason; return false; }
                TrySetObjectName(sprite, IconNamePrefix + itemId);
                iconField.SetValue(item, sprite);
                if (!ReferenceEquals(iconField.GetValue(item), sprite))
                { status.FailureReason = "ItemIcon assignment did not stick; native donor icon retained"; failure = status.FailureReason; return false; }
                status.Loaded = true;
                return true;
            }
            catch (Exception ex)
            {
                status.FailureReason = "ItemIcon assignment failed: " + ex.GetType().Name + "; native donor icon retained";
                failure = status.FailureReason;
                return false;
            }
        }

        internal static string ResolveAssetPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath)) return string.Empty;
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] roots = new string[]
            {
                baseDir,
                Path.Combine(baseDir, "plugins", "ErenshorCraftingExpanded"),
                Path.Combine(baseDir, "plugins"),
                Path.Combine(baseDir, "Lunaris", "plugins", "ErenshorCraftingExpanded")
            };
            for (int i = 0; i < roots.Length; i++)
            {
                try
                {
                    string candidate = Path.GetFullPath(Path.Combine(roots[i], normalized));
                    string root = Path.GetFullPath(roots[i]);
                    if (!IsWithinRoot(candidate, root)) continue;
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return string.Empty;
        }

        private static object LoadSprite(string path, out string failure)
        {
            failure = string.Empty;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes == null || bytes.Length < 16) { failure = "icon file is empty"; return null; }
                Type textureType = FindType("UnityEngine.Texture2D");
                Type imageConversionType = FindType("UnityEngine.ImageConversion");
                Type spriteType = FindType("UnityEngine.Sprite");
                Type rectType = FindType("UnityEngine.Rect");
                Type vectorType = FindType("UnityEngine.Vector2");
                if (textureType == null || imageConversionType == null || spriteType == null || rectType == null || vectorType == null)
                { failure = "Unity image types are unavailable"; return null; }

                object texture = null;
                ConstructorInfo twoArg = textureType.GetConstructor(new Type[] { typeof(int), typeof(int) });
                if (twoArg != null) texture = twoArg.Invoke(new object[] { 2, 2 });
                if (texture == null) { failure = "Texture2D constructor unavailable"; return null; }

                MethodInfo load = FindLoadImage(imageConversionType, textureType);
                if (load == null) { failure = "Unity ImageConversion.LoadImage unavailable"; return null; }
                ParameterInfo[] loadParams = load.GetParameters();
                object loadedRaw = loadParams.Length == 3
                    ? load.Invoke(null, new object[] { texture, bytes, false })
                    : load.Invoke(null, new object[] { texture, bytes });
                if (loadedRaw is bool && !(bool)loadedRaw) { failure = "Unity rejected PNG data"; return null; }

                PropertyInfo widthProperty = textureType.GetProperty("width", AllInstance);
                PropertyInfo heightProperty = textureType.GetProperty("height", AllInstance);
                if (widthProperty == null || heightProperty == null) { failure = "Texture2D dimensions unavailable"; return null; }
                int width = (int)widthProperty.GetValue(texture, null);
                int height = (int)heightProperty.GetValue(texture, null);
                object rect = Activator.CreateInstance(rectType, new object[] { 0f, 0f, (float)width, (float)height });
                object pivot = Activator.CreateInstance(vectorType, new object[] { 0.5f, 0.5f });
                MethodInfo create = FindSpriteCreate(spriteType, textureType, rectType, vectorType);
                if (create == null) { failure = "Sprite.Create overload unavailable"; return null; }
                ParameterInfo[] createParams = create.GetParameters();
                object sprite = createParams.Length == 4
                    ? create.Invoke(null, new object[] { texture, rect, pivot, 100f })
                    : create.Invoke(null, new object[] { texture, rect, pivot });
                if (sprite == null) { failure = "Sprite.Create returned null"; return null; }
                return sprite;
            }
            catch (Exception ex) { failure = "icon decode failed: " + ex.GetType().Name; return null; }
        }

        private static MethodInfo FindLoadImage(Type imageConversionType, Type textureType)
        {
            MethodInfo[] methods = imageConversionType.GetMethods(AllStatic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "LoadImage", StringComparison.Ordinal)) continue;
                ParameterInfo[] p = method.GetParameters();
                if ((p.Length == 2 || p.Length == 3) && p[0].ParameterType == textureType && p[1].ParameterType == typeof(byte[])) return method;
            }
            return null;
        }

        private static MethodInfo FindSpriteCreate(Type spriteType, Type textureType, Type rectType, Type vectorType)
        {
            MethodInfo[] methods = spriteType.GetMethods(AllStatic);
            MethodInfo three = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Create", StringComparison.Ordinal)) continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length < 3 || p[0].ParameterType != textureType || p[1].ParameterType != rectType || p[2].ParameterType != vectorType) continue;
                if (p.Length == 4 && p[3].ParameterType == typeof(float)) return method;
                if (p.Length == 3) three = method;
            }
            return three;
        }

        private static bool IsWithinRoot(string candidate, string root)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(root)) return false;
            string normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(candidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)) return true;
            string prefix = normalizedRoot + Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadObjectName(object value)
        {
            if (value == null) return string.Empty;
            try
            {
                PropertyInfo nameProperty = value.GetType().GetProperty("name", AllInstance);
                object raw = nameProperty == null ? null : nameProperty.GetValue(value, null);
                return raw == null ? string.Empty : raw.ToString();
            }
            catch { return string.Empty; }
        }

        private static void TrySetObjectName(object value, string name)
        {
            if (value == null || string.IsNullOrEmpty(name)) return;
            try
            {
                PropertyInfo nameProperty = value.GetType().GetProperty("name", AllInstance);
                if (nameProperty != null && nameProperty.CanWrite) nameProperty.SetValue(value, name, null);
            }
            catch { }
        }

        private static Type FindType(string name)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                try { Type t = assemblies[i].GetType(name, false); if (t != null) return t; }
                catch { }
            }
            return null;
        }

        internal static string RunSelfTests()
        {
            if (!string.IsNullOrEmpty(ResolveAssetPath("../outside.png"))) return "FAIL icon traversal should not resolve";
            if (!string.IsNullOrEmpty(ResolveAssetPath("assets/icons/__crafting_missing_icon_test__.png"))) return "FAIL missing icon should resolve to fallback";
            return "PASS item icon asset loader policy";
        }
    }
}
