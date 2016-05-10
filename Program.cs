using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace FixProjectJsonWarnings
{
    class Program
    {
        private static readonly string[] _packOptionProps = new[] { "repository", "tags", "licenseUrl", };

        static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage FixProjectJsonWarnings [repo-root]");
                return 1;
            }
             
            var dir = args[0];
            foreach (var file in Directory.EnumerateFiles(dir, "project.json", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                var replaced = ModifyProject(content);

                if (replaced != content)
                {
                    File.WriteAllText(file, replaced);
                }
            }
            
            return 0;
        }

        private static string ModifyProject(string content)
        {
            var root = JObject.Parse(content);
            RenameCompilationOptions(root);
            foreach (var item in _packOptionProps)
            {
                MoveToPackOptions(root, item);
            }

            MovePackIncludeToPackOptions(root);
            MoveContent(root);
            MoveResourcesToBuild(root);

            return root.ToString();
        }

        private static void MovePackIncludeToPackOptions(JObject root)
        {
            var packInclude = root.Property("packInclude");
            if (packInclude != null)
            {
                var packOptions = GetOrAddProperty(root, "packOptions", packInclude);
                var files = GetOrAddProperty(packOptions, "files", null);
                files.Add(new JProperty("mappings", packInclude.Value));
                packInclude.Remove();
            }
        }

        private static void MoveResourcesToBuild(JObject root)
        {
            var resourceItems = new[]
            {
                Tuple.Create("resource", "include"),
                Tuple.Create("namedResource", "mappings"),
            };

            foreach (var item in resourceItems)
            {
                var property = root.Property(item.Item1);
                if (property != null)
                {
                    var build = GetOrAddProperty(root, "buildOptions", property);

                    var resource = (JObject)build["embed"];
                    if (resource == null)
                    {
                        resource = new JObject();
                        build["embed"] = resource;
                    }

                    resource[item.Item2] = property.Value;
                    property.Remove();
                }
            }
        }

        private static void MoveContent(JObject root)
        {
            var contentItems = new[]
            {
                Tuple.Create("content", "include"),
                Tuple.Create("contentExclude", "exclude"),
                Tuple.Create("contentFiles", "includeFiles")
            };

            foreach (var item in contentItems)
            {
                var property = root.Property(item.Item1);
                if (property != null)
                {
                    var publishInclude = GetOrAddProperty(root, "publishOptions", property);
                    publishInclude[item.Item2] = property.Value;
                    
                    var buildOptions = GetOrAddProperty(root, "buildOptions", property);
                    var copyToOutput = GetOrAddProperty(buildOptions, "copyToOutput", null);
                    copyToOutput[item.Item2] = property.Value;
                    
                    property.Remove();
                }
            }
        }

        private static void MoveToPackOptions(JObject root, string item)
        {
            var property = root.Property(item);
            if (property != null)
            {
                JObject packOptions = GetOrAddProperty(root, "packOptions", property);

                property.Remove();
                packOptions.Add(property);
            }
        }

        private static JObject GetOrAddProperty(JObject root, string propertyName, JProperty insertAfter)
        {
            var newProperty = (JObject)root[propertyName];
            if (newProperty == null)
            {
                newProperty = new JObject();
                var property = new JProperty(propertyName, newProperty);
                if (insertAfter != null)
                {
                    insertAfter.AddAfterSelf(property);
                }
                else
                {
                    root.Add(property);
                }
            }

            return newProperty;
        }

        private static void RenameCompilationOptions(JObject root)
        {
            var compilationOptions = root.Property("compilationOptions");
            if (compilationOptions != null)
            {
                compilationOptions.Replace(new JProperty("buildOptions", compilationOptions.Value));
            }

            foreach (var item in ((JObject)root["frameworks"]).Properties())
            {
                var tfmCompilationOptions = ((JObject)item.Value).Property("compilationOptions");
                if (tfmCompilationOptions != null)
                {
                    tfmCompilationOptions.Replace(new JProperty("buildOptions", tfmCompilationOptions.Value));
                }
            }
        }
    }
}
