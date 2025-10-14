using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace HEVSuitMod
{
	// Some useful stuff goes here
	public static class Utils
	{
		/// <summary>
		/// Generate a tree of files similar to Windows TREE /F
		/// </summary>
		/// <param name="files"></param>
		/// <returns></returns>
		public static string FileTree(List<string> files)
		{
			var tree = new Node("ROOT");
			foreach (var file in files)
			{
				var parts = file.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
				Node current = tree;

				foreach (var part in parts)
				{
					if (!current.Children.ContainsKey(part))
						current.Children[part] = new Node(part);

					current = current.Children[part];
				}
			}

			var sb = new StringBuilder();
			foreach (var child in tree.Children.Values)
				BuildTreeRecursive(sb, child, "", isLast: child == tree.Children.Values.Last());

			return sb.ToString();
		}

		private static void BuildTreeRecursive(StringBuilder sb, Node node, string prefix, bool isLast)
		{
			sb.Append(prefix);
			sb.Append(prefix.IsNullOrEmpty() ? null : (isLast ? "└── " : "├── "));
			sb.AppendLine(node.Name);
			string childPrefix = prefix + (isLast ? "    " : "│   ");
			int i = 0;

			foreach (var child in node.Children.Values.OrderBy(c => c.Name))
				BuildTreeRecursive(sb, child, childPrefix, ++i == node.Children.Count);
		}

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
		}
	}
}
