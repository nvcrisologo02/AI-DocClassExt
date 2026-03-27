using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DocumentIA.Desktop.Views
{
    public partial class JsonViewerControl : UserControl
    {
        private JToken _currentToken;

        public JsonViewerControl()
        {
            InitializeComponent();
        }

        public void DisplayJson(object jsonObject)
        {
            JsonTreeView.Items.Clear();
            RawJsonTextBox.Text = string.Empty;
            SummaryText.Text = "Sin datos";
            FooterText.Text = "Listo";
            _currentToken = null;

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

                _currentToken = jtoken;
                RawJsonTextBox.Text = jtoken.ToString(Newtonsoft.Json.Formatting.Indented);
                SummaryText.Text = BuildSummary(jtoken);

                var rootItem = BuildTreeItem(jtoken, "root");
                if (rootItem != null)
                {
                    rootItem.IsExpanded = true;
                    JsonTreeView.Items.Add(rootItem);
                }

                FooterText.Text = "JSON cargado correctamente";
            }
            catch (Exception ex)
            {
                FooterText.Text = $"Error parseando JSON: {ex.Message}";
            }
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
            var key = string.IsNullOrWhiteSpace(name) ? "root" : name;

            if (token.Type == JTokenType.Object)
            {
                return $"{key}: {{...}}";
            }

            if (token.Type == JTokenType.Array)
            {
                return $"{key}: [{((JArray)token).Count} items]";
            }

            if (token.Type == JTokenType.String)
            {
                var val = token.ToString();
                var trimmed = val.Length > 80 ? val.Substring(0, 77) + "..." : val;
                return $"{key}: \"{trimmed}\"";
            }

            if (token.Type == JTokenType.Null)
            {
                return $"{key}: null";
            }

            return $"{key}: {token}";
        }

        private string BuildSummary(JToken token)
        {
            var nodeCount = CountNodes(token);
            var objectCount = CountByType(token, JTokenType.Object);
            var arrayCount = CountByType(token, JTokenType.Array);
            return $"{nodeCount} nodos | {objectCount} objetos | {arrayCount} arrays";
        }

        private int CountNodes(JToken token)
        {
            var count = 1;

            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    count += CountNodes(prop.Value);
                }
            }
            else if (token is JArray arr)
            {
                foreach (var child in arr)
                {
                    count += CountNodes(child);
                }
            }

            return count;
        }

        private int CountByType(JToken token, JTokenType type)
        {
            var count = token.Type == type ? 1 : 0;

            if (token is JObject obj)
            {
                foreach (var prop in obj.Properties())
                {
                    count += CountByType(prop.Value, type);
                }
            }
            else if (token is JArray arr)
            {
                foreach (var child in arr)
                {
                    count += CountByType(child, type);
                }
            }

            return count;
        }

        private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in JsonTreeView.Items)
            {
                if (item is TreeViewItem treeItem)
                {
                    SetExpandedRecursive(treeItem, true);
                }
            }
            FooterText.Text = "Árbol expandido";
        }

        private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in JsonTreeView.Items)
            {
                if (item is TreeViewItem treeItem)
                {
                    SetExpandedRecursive(treeItem, false);
                }
            }
            FooterText.Text = "Árbol colapsado";
        }

        private void CopyJsonButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentToken == null)
            {
                FooterText.Text = "No hay JSON para copiar";
                return;
            }

            Clipboard.SetText(_currentToken.ToString(Newtonsoft.Json.Formatting.Indented));
            FooterText.Text = "JSON copiado al portapapeles";
        }

        private void SetExpandedRecursive(TreeViewItem item, bool isExpanded)
        {
            item.IsExpanded = isExpanded;

            foreach (var child in item.Items)
            {
                if (child is TreeViewItem childItem)
                {
                    SetExpandedRecursive(childItem, isExpanded);
                }
            }
        }
    }
}
