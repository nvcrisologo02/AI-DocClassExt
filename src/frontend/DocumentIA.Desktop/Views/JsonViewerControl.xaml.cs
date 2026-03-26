using Newtonsoft.Json.Linq;
using System.Windows.Controls;

namespace DocumentIA.Desktop.Views
{
    public partial class JsonViewerControl : UserControl
    {
        public JsonViewerControl()
        {
            InitializeComponent();
        }

        public void DisplayJson(object jsonObject)
        {
            JsonTreeView.Items.Clear();

            if (jsonObject == null)
                return;

            try
            {
                JToken jtoken;
                
                if (jsonObject is string jsonString)
                {
                    jtoken = JToken.Parse(jsonString);
                }
                else if (jsonObject is JToken token)
                {
                    jtoken = token;
                }
                else
                {
                    jtoken = JToken.FromObject(jsonObject);
                }

                var rootItem = BuildTreeItem(jtoken, "root");
                if (rootItem != null)
                {
                    JsonTreeView.Items.Add(rootItem);
                }
            }
            catch { }
        }

        private TreeViewItem BuildTreeItem(JToken token, string name)
        {
            var item = new TreeViewItem
            {
                Header = FormatHeader(token, name),
                IsExpanded = false
            };

            if (token is JObject jObj)
            {
                foreach (var property in jObj.Properties())
                {
                    var child = BuildTreeItem(property.Value, property.Name);
                    if (child != null)
                        item.Items.Add(child);
                }
            }
            else if (token is JArray jArr)
            {
                for (int i = 0; i < jArr.Count; i++)
                {
                    var child = BuildTreeItem(jArr[i], "[" + i + "]");
                    if (child != null)
                        item.Items.Add(child);
                }
            }

            return item;
        }

        private string FormatHeader(JToken token, string name)
        {
            var typeName = GetTypeName(token.Type);
            var count = token is JArray arr ? arr.Count : 0;
            var displayValue = GetDisplayValue(token, name, count);
            return displayValue;
        }

        private string GetTypeName(JTokenType type)
        {
            return type switch
            {
                JTokenType.Object => "{ }",
                JTokenType.Array => "[ ]",
                JTokenType.String => "\" \"",
                JTokenType.Integer => "#",
                JTokenType.Float => "#.#",
                JTokenType.Boolean => "✓/✗",
                JTokenType.Null => "∅",
                _ => "?"
            };
        }

        private string GetDisplayValue(JToken token, string name, int count)
        {
            if (token.Type == JTokenType.Object)
                return name + ": {...}";
            else if (token.Type == JTokenType.Array)
                return name + ": [" + count + " items]";
            else if (token.Type == JTokenType.String)
            {
                var val = token.ToString();
                var trimmed = val.Length > 50 ? val.Substring(0, 47) + "..." : val;
                return name + ": \"" + trimmed + "\"";
            }
            else if (token.Type == JTokenType.Null)
                return name + ": null";
            else
                return name + ": " + token.ToString();
        }
    }
}
