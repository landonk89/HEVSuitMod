using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HEVSuitMod;

// Some useful stuff goes here
public static class Utils
{
	//private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource($"{typeof(Utils).FullName}");

	// Simple tree node
	private class Node
	{
		public string Name { get; }
		public Dictionary<string, Node> Children { get; }

		public Node(string name)
		{
			Name = name;
			Children = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
		}

		public bool IsFile => Children.Count == 0;
	}

	/// <summary>
	/// Generate a tree of files similar to Windows TREE /F
	/// </summary>
	public static string FileTree(List<string> files)
	{
		var tree = new Node("ROOT");
		foreach (var file in files)
		{
			var parts = file.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
			Node current = tree;

			foreach (var part in parts)
			{
				if (!current.Children.ContainsKey(part))
					current.Children[part] = new Node(part);

				current = current.Children[part];
			}
		}

		var sb = new StringBuilder();
		var children = tree.Children.Values.ToList();

		for (int i = 0; i < children.Count; i++)
			BuildTreeRecursive(sb, children[i], "", i == children.Count - 1);

		return sb.ToString();

		void BuildTreeRecursive(StringBuilder sb, Node node, string prefix, bool isLast)
		{
			sb.Append(prefix);
			if (!string.IsNullOrEmpty(prefix))
				sb.Append(isLast ? "└── " : "├── ");
			sb.AppendLine(node.Name);

			string childPrefix = prefix + (isLast ? "    " : "│   ");

			// Files first
			var orderedChildren = node.Children.Values
				.OrderByDescending(c => c.IsFile)
				.ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
				.ToList();

			for (int i = 0; i < orderedChildren.Count; i++)
				BuildTreeRecursive(sb, orderedChildren[i], childPrefix, i == orderedChildren.Count - 1);
		}
	}

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
