namespace Winspeqt.Models
{
    /// <summary>
    /// Represents a breadcrumb segment for the Large File Finder view.
    /// </summary>
    public class PathItem
    {
        /// <summary>
        /// Display label for the path segment.
        /// </summary>
        public string Name { get; }
        /// <summary>
        /// Full path this breadcrumb segment represents.
        /// </summary>
        public string Path { get; }
        /// <summary>
        /// Index of the segment within the breadcrumb list.
        /// </summary>
        public int Index { get; }
        /// <summary>
        /// Creates a new breadcrumb item and derives a friendly name from the path.
        /// </summary>
        public PathItem(string path, int index)
        {
            Path = path;
            string temp = System.IO.Path.GetFileName(path);
            Name = temp != "" ? temp : path[..^1];
            Index = index;
        }
    }
}
