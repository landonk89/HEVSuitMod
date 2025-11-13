using UnityEngine;

namespace HEVSuitMod.Tools;

public static class TransformExtensions
{
    public static string GetRelativePath(this Transform t, Transform root)
    {
        if (t == root)
            return string.Empty;
        string path = t.name;
        Transform current = t.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}