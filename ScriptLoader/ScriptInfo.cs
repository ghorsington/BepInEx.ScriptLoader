using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ScriptLoader
{
    internal class ScriptInfo
    {
        private static readonly Dictionary<string, Action<ScriptInfo, string>> CommandParsers =
            new Dictionary<string, Action<ScriptInfo, string>>
            {
                ["name"] = (si, c) => si.Name = c,
                ["author"] = (si, c) => si.Author = c,
                ["desc"] = (si, c) => si.Description = c,
                ["ref"] = (si, c) => si.References.Add(c.Template(Utilities.KnownPaths)),
                ["proc_filter"] = (si, c) => si.ProcessFilters.Add(c)
            };

        public string Author { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> References { get; set; } = new List<string>();
        public List<string> ProcessFilters { get; set; } = new List<string>();

        public static ScriptInfo FromTextFile(string path)
        {
            using (var tw = File.OpenText(path))
            {
                string line;
                var si = new ScriptInfo();
                while ((line = tw.ReadLine()) != null)
                {
                    line = line.Trim();

                    if (!line.StartsWith("//"))
                        return si;

                    line = line.Substring(2).Trim();
                    if (!line.StartsWith("#"))
                        continue;

                    var nextSpace = line.IndexOf(' ');
                    if (nextSpace < 0)
                        continue;

                    var command = line.Substring(1, nextSpace - 1);
                    var value = line.Substring(nextSpace).Trim();

                    if (CommandParsers.TryGetValue(command, out var parser))
                        parser(si, value);
                }

                return si;
            }
        }
    }
}