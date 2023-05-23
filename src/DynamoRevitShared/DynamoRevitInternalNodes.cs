using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;

namespace Dynamo.Applications
{

    /// <summary>
    /// Defines parameters used for loading internal Dynamo Revit packages
    /// </summary>
    [Serializable()]
    public class InternalPackage
    {
        /// <summary>
        /// keeps the path to the node file
        /// </summary>
        public string NodePath { get; set; }

        /// <summary>
        /// keeps the path to the layoutSpecs.json file
        /// </summary>
        public string LayoutSpecsPath { get; set; }
    }

    internal static class DynamoRevitInternalNodes
    {
        private const string InternalNodesDir = "nodes";
        private static IEnumerable<string> GetAllInternalPackageFiles()
        {
            string currentAssemblyPath = Assembly.GetExecutingAssembly().Location;
            string currentAssemblyDir = Path.GetDirectoryName(currentAssemblyPath);

            string internalNodesDir = Path.Combine(currentAssemblyDir, InternalNodesDir);
            if (false == Directory.Exists(internalNodesDir))
            {
                return new List<string>();
            }

            string[] internalNodesFolders = Directory.GetDirectories(internalNodesDir);

            List<string> internalPackageFiles = new List<string>();
            foreach (string dir in internalNodesFolders)
            {
                string internalPackageFile = Path.Combine(dir, "internalPackage.xml");
                if (true == File.Exists(internalPackageFile))
                {
                    internalPackageFiles.Add(internalPackageFile);
                }
            }
            return internalPackageFiles;
        }
        private static IEnumerable<InternalPackage> ParseinternalPackageFiles(IEnumerable<string> internalPackageFiles)
        {
            List<InternalPackage> internalPackages = new List<InternalPackage>();

            foreach (string internalPackageFile in internalPackageFiles)
            {
                try
                {
                    string internalPackageDir = Path.GetDirectoryName(internalPackageFile);
                    using (StreamReader reader = new StreamReader(internalPackageFile))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(InternalPackage));
                        InternalPackage intPackage = serializer.Deserialize(reader) as InternalPackage;

                        // convert to absolute path, if needed
                        if (false == Path.IsPathRooted(intPackage.NodePath))
                        {
                            intPackage.NodePath = Path.Combine(internalPackageDir, intPackage.NodePath);
                        }

                        // convert to absolute path, if needed
                        if (false == Path.IsPathRooted(intPackage.LayoutSpecsPath))
                        {
                            intPackage.LayoutSpecsPath = Path.Combine(internalPackageDir, intPackage.LayoutSpecsPath);
                        }

                        internalPackages.Add(intPackage);
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine(string.Format("Exception while trying to parse internalPackage file {0}", internalPackageFile));
                }
            }

            return internalPackages;
        }
        internal static IEnumerable<string> GetNodesToPreload()
        {
            IEnumerable<string> internalPackageFiles = GetAllInternalPackageFiles();
            return ParseinternalPackageFiles(internalPackageFiles).Select(pkg => pkg.NodePath);
        }
        internal static IEnumerable<string> GetLayoutSpecsFiles()
        {
            IEnumerable<string> internalPackageFiles = GetAllInternalPackageFiles();
            return ParseinternalPackageFiles(internalPackageFiles).Select(pkg => pkg.LayoutSpecsPath);
        }
    }
}
