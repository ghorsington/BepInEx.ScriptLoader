using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace ScriptLoader
{
    internal static class Utilities
    {
        public static Dictionary<string, string> KnownPaths = new Dictionary<string, string>
        {
            ["BepInExRoot"] = Paths.BepInExRootPath,
            ["Scripts"] = Path.Combine(Paths.GameRootPath, "scripts"),
            ["Managed"] = Paths.ManagedPath
        };

        public static string Template(this string template, Dictionary<string, string> replacements)
        {
            var sb = new StringBuilder(template.Length);
            var sbTemplate = new StringBuilder();

            var insideTemplate = false;
            var bracedTemplate = false;
            for (var i = 0; i < template.Length; i++)
            {
                var c = template[i];
                switch (c)
                {
                    case '\\':
                        if (i + 1 < template.Length && template[i + 1] == '$')
                        {
                            sb.Append('$');
                            i++;
                            continue;
                        }

                        break;
                    case '$':
                        insideTemplate = true;
                        continue;
                    case '{':
                        if (insideTemplate)
                        {
                            bracedTemplate = true;
                            continue;
                        }

                        break;
                    case '}':
                        if (insideTemplate && sbTemplate.Length > 0)
                        {
                            if (replacements.TryGetValue(sbTemplate.ToString(), out var replacement))
                                sb.Append(replacement);
                            sbTemplate.Length = 0;
                            insideTemplate = false;
                            bracedTemplate = false;
                            continue;
                        }

                        break;
                }

                if (insideTemplate && !bracedTemplate && !char.IsDigit(c))
                {
                    if (replacements.TryGetValue(sbTemplate.ToString(), out var replacement))
                        sb.Append(replacement);
                    sbTemplate.Length = 0;
                    insideTemplate = false;
                }

                if (insideTemplate)
                    sbTemplate.Append(c);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }
    }
}