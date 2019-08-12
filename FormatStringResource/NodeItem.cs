using System.Xml.Linq;

namespace FormatStringResource
{
    internal class NodeItem
    {
        public string? Text { get; }
        public XElement Item { get; }

        public NodeItem(string text, XElement item)
            => (Text, Item) = (text, item);

        public void Deconstruct(out string? text, out XElement item)
            => (text, item) = (Text, Item);
    }
}