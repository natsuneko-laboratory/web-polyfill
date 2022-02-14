// ------------------------------------------------------------------------------------------
//  Copyright (c) Natsuneko. All rights reserved.
//  Licensed under the MIT License. See LICENSE in the project root for license information.
// ------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using JetBrains.Annotations;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace NatsunekoLaboratory.WebPolyfill
{
    // Polyfill for Media Query
    public class MediaQuery
    {
        private const string DynamicGeneratedStyleSheetName = "NatsunekoLaboratory.WebPolyfill.MediaQuery.Generated.uss";
        private static readonly Regex Comment = new Regex("\\/\\*[^*]*\\*+([^/][^*]*\\*+)*\\/", RegexOptions.Compiled);
        private static readonly Regex Media = new Regex("@media[^\\{]+\\{([^\\{\\}]*\\{[^\\}\\{]*\\})+", RegexOptions.Compiled);
        private static readonly Regex MaxWidth = new Regex("\\(\\s*max\\-width\\s*:\\s*(\\s*[0-9\\.]+)(px|rem)\\s*\\)", RegexOptions.Compiled);
        private static readonly Regex MinWidth = new Regex("\\(\\s*min\\-width\\s*:\\s*(\\s*[0-9\\.]+)(px|rem)\\s*\\)", RegexOptions.Compiled);
        private static readonly Regex MinMaxHxW = new Regex("\\(\\s*m(in|ax)\\-(height|width)\\s*:\\s*(\\s*[0-9\\.]+)(px|rem)\\s*\\)", RegexOptions.Compiled);
        private static readonly Regex Other = new Regex("\\([^\\)]*\\)", RegexOptions.Compiled);
        private static readonly Regex Styles = new Regex("@media *([^\\{]+)\\{([\\S\\s]+?)$", RegexOptions.Compiled);

        private readonly List<MediaQueryRule> _styles;

        private bool _isInitialized;
        private bool _isResized;
        private Vector2 _previous;

        public MediaQuery()
        {
            _styles = new List<MediaQueryRule>();
        }

        public void OnUpdate(EditorWindow window)
        {
            if (_isInitialized)
            {
                if (_previous != window.position.size)
                {
                    _previous = window.position.size;
                    _isResized = true;
                }
                else
                {
                    if (!_isResized)
                        return;

                    _isResized = false;
                    OnResized(window);
                }
            }
            else
            {
                _isInitialized = true;
                _previous = window.position.size;

                LoadStylesheets(window);
                ApplyMediaQueries(window);
            }
        }

        private void LoadStylesheets(EditorWindow window)
        {
            if (window.rootVisualElement.styleSheets.count <= 1)
                return; // ignores default stylesheet only

            for (var i = 1; i < window.rootVisualElement.styleSheets.count; i++)
            {
                var stylesheet = window.rootVisualElement.styleSheets[i];
                TranslateStyle(stylesheet);
            }
        }

        private void TranslateStyle(StyleSheet stylesheet)
        {
            var raw = ReadStylesheet(stylesheet);
            var css = Comment.Replace(raw, "");
            foreach (Match queryString in Media.Matches(css))
                if (Styles.IsMatch(queryString.ToString()))
                {
                    var match = Styles.Match(queryString.ToString());
                    foreach (var query in match.Groups[1].Value.Split(','))
                    {
                        if (Other.IsMatch(MinMaxHxW.Replace(query, "")))
                            continue;

                        _styles.Add(new MediaQueryRule
                        {
                            HasQuery = query.StartsWith("("),
                            MinWidth = ParseWidth(MinWidth, query),
                            MaxWidth = ParseWidth(MaxWidth, query),
                            Rule = match.Groups[2].Value
                        });
                    }
                }
        }

        private static string ReadStylesheet(StyleSheet stylesheet)
        {
            var path = AssetDatabase.GetAssetPath(stylesheet);
            using (var sr = new StreamReader(path))
                return sr.ReadToEnd();
        }

        [CanBeNull]
        private static string ParseWidth(Regex regex, string input)
        {
            if (!regex.IsMatch(input))
                return null;

            var match = regex.Match(input);
            return $"{match.Groups[1]}{match.Groups[2]}";
        }

        private void OnResized(EditorWindow window)
        {
            ApplyMediaQueries(window);
        }

        private void ApplyMediaQueries(EditorWindow window)
        {
            var rules = FindMatchingStyles(window);
            var root = window.rootVisualElement;
            ClearDynamicGeneratedStyleSheet(root);

            if (rules.Count == 0)
                return;

            var asset = ScriptableObject.CreateInstance<StyleSheet>();
            asset.hideFlags = HideFlags.NotEditable;
            asset.name = DynamicGeneratedStyleSheetName;

            var sb = new StringBuilder();
            foreach (var rule in rules)
                sb.AppendLine(rule.Rule);

            var t = typeof(AssetDatabase).Assembly.GetType("UnityEditor.StyleSheets.StyleSheetImporterImpl");
            var i = Activator.CreateInstance(t);
            var m = t.GetMethod("Import", BindingFlags.Public | BindingFlags.Instance);
            if (m == null)
                return;

            m.Invoke(i, new object[] { asset, sb.ToString() });

            root.styleSheets.Add(asset);
        }

        private static void ClearDynamicGeneratedStyleSheet(VisualElement element)
        {
            for (var i = 0; i < element.styleSheets.count; i++)
            {
                var stylesheet = element.styleSheets[i];
                if (stylesheet.name == DynamicGeneratedStyleSheetName)
                    element.styleSheets.Remove(stylesheet);
            }
        }

        private List<MediaQueryRule> FindMatchingStyles(EditorWindow window)
        {
            var width = window.position.size.x;

            return _styles.Where(w =>
            {
                var r = true;
                if (!string.IsNullOrEmpty(w.MinWidth))
                {
                    if (w.MinWidth.EndsWith("px"))
                        r &= width >= int.Parse(w.MinWidth.Substring(0, w.MinWidth.LastIndexOf("px", StringComparison.Ordinal)));
                    else if (w.MinWidth.EndsWith("rem"))
                        r &= width >= int.Parse(w.MinWidth.Substring(0, w.MinWidth.LastIndexOf("ren", StringComparison.Ordinal))) * 12f;
                }

                if (!string.IsNullOrEmpty(w.MaxWidth))
                {
                    if (w.MaxWidth.EndsWith("px"))
                        r &= width < int.Parse(w.MaxWidth.Substring(0, w.MaxWidth.LastIndexOf("px", StringComparison.Ordinal)));
                    else if (w.MaxWidth.EndsWith("rem"))
                        r &= width < int.Parse(w.MaxWidth.Substring(0, w.MaxWidth.LastIndexOf("ren", StringComparison.Ordinal))) * 12f;
                }

                return r;
            }).ToList();
        }

        private class MediaQueryRule
        {
            public bool HasQuery { get; set; }

            [CanBeNull]
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public string MinWidth { get; set; }

            [CanBeNull]
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public string MaxWidth { get; set; }

            public string Rule { get; set; }
        }
    }
}