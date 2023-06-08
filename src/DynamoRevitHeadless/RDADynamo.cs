using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using Dynamo.Applications.Models;
using Dynamo.Graph.Workspaces;
using Dynamo.Models;
using Dynamo.Scheduler;
using DynamoInstallDetective;
using Greg.AuthProviders;
using RevitServices.Persistence;
using DBApp = DynamoRevitHeadless.DynamoRevitDBApp;

namespace Dynamo.Applications
{
    public class RDADynamo
    {
        private static List<Action> idleActions;
        private static bool handledCrash;
        private static List<Exception> preLoadExceptions;
        private Stopwatch startupTimer;

        /// <summary>
        /// Get or Set the current RevitDynamoModel available in Revit context
        /// </summary>
        public static RDADynamoModel RevitDynamoModel { get; set; }

        /// <summary>
        /// Get or Set the current DynamoViewModel available in Revit context
        /// </summary>
        //public static DynamoViewModel RevitDynamoViewModel { get; private set; }

        static RDADynamo()
        {
            idleActions = new List<Action>();
            RevitDynamoModel = null;
            handledCrash = false;
            preLoadExceptions = new List<Exception>();
        }

        private void AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            //push any exceptions generated before DynamoLoad to this list
            preLoadExceptions.AddRange(StartupUtils.CheckAssemblyForVersionMismatches(args.LoadedAssembly));
        }

        public bool PrepareModel(Autodesk.Revit.ApplicationServices.Application app)
        {
            startupTimer = Stopwatch.StartNew();

            InitializeCore();
            //subscribe to the assembly load
            AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoad;

            try
            {
                UpdateSystemPathForProcess();

                // create core data models
                RevitDynamoModel = InitializeCoreModel(app);
                RevitDynamoModel.UpdateManager.RegisterExternalApplicationProcessId(Process.GetCurrentProcess().Id);
                RevitDynamoModel.Logger.Log("SYSTEM", string.Format("Environment Path:{0}", Environment.GetEnvironmentVariable("PATH")));

                //unsubscribe to the assembly load
                AppDomain.CurrentDomain.AssemblyLoad -= AssemblyLoad;

                RevitDynamoModel.HandlePostInitialization();

                return true;
            }
            catch (Exception ex)
            {
                // notify instrumentation
                Logging.Analytics.TrackException(ex, true);

                //If for some reason Dynamo has crashed while startup make sure the Dynamo Model is properly shutdown.
                if (RevitDynamoModel != null)
                {
                    RevitDynamoModel.ShutDown(false);
                    RevitDynamoModel = null;
                }

                return false;
            }
        }

        /// <summary>
        /// Add the main exec path to the system PATH
        /// This is required to pickup certain dlls.
        /// </summary>
        private static void UpdateSystemPathForProcess()
        {
            var path =
                    Environment.GetEnvironmentVariable(
                        "Path",
                        EnvironmentVariableTarget.Process) + ";" + DBApp.DynamoCorePath;
            Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.Process);
        }

        #region Initialization
        public static string GetGeometryFactoryPath(string corePath, Version version)
        {
            var dynamoAsmPath = Path.Combine(corePath, "DynamoShapeManager.dll");
            var assembly = Assembly.LoadFrom(dynamoAsmPath);
            if (assembly == null)
                throw new FileNotFoundException("File not found", dynamoAsmPath);

            var utilities = assembly.GetType("DynamoShapeManager.Utilities");
            var getGeometryFactoryPath = utilities.GetMethod("GetGeometryFactoryPath2");

            return (getGeometryFactoryPath.Invoke(null,
                new object[] { corePath, version }) as string);
        }

        private static void PreloadDynamoCoreDlls()
        {
            // Assume Revit Install folder as look for root. Assembly name is compromised.
            var assemblyList = new[]
            {
                "SDA\\bin\\ICSharpCode.AvalonEdit.dll"
            };

            foreach (var assembly in assemblyList)
            {
                var assemblyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assembly);
                if (File.Exists(assemblyPath))
                    Assembly.LoadFrom(assemblyPath);
            }
        }

        private static RDADynamoModel InitializeCoreModel(Autodesk.Revit.ApplicationServices.Application app)
        {
            // Temporary fix to pre-load DLLs that were also referenced in Revit folder. 
            // To do: Need to align with Revit when provided a chance.
            PreloadDynamoCoreDlls();
            var corePath = DBApp.DynamoCorePath;
            var dynamoRevitExePath = Assembly.GetExecutingAssembly().Location;
            var dynamoRevitRoot = Path.GetDirectoryName(dynamoRevitExePath);// ...\Revit_xxxx\ folder

            var userDataFolder = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
                "Dynamo", "Dynamo Revit");
            var commonDataFolder = Path.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
                "Autodesk", "RVT " + app.VersionNumber, "Dynamo");

            // when Dynamo runs on top of Revit we must load the same version of ASM as revit
            // so tell Dynamo core we've loaded that version.
            var loadedLibGVersion = PreloadAsmFromRevit();

            DocumentManager.Instance.PrepareForAutomation(app);

            return RDADynamoModel.Start(
            new RDADynamoModel.RevitStartConfiguration()
            {
                DynamoCorePath = corePath,
                DynamoHostPath = dynamoRevitRoot,
                GeometryFactoryPath = GetGeometryFactoryPath(corePath, loadedLibGVersion),
                PathResolver = new RevitPathResolver(userDataFolder, commonDataFolder),
                Context = GetRevitContext(app),
                SchedulerThread = new RDASchedulerThread(),
                StartInTestMode = false,
                AuthProvider = new RevitOAuth2Provider(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)),
                UpdateManager = null,
                ProcessMode = TaskProcessMode.Synchronous
            });
        }

        private static string GetRevitContext(Autodesk.Revit.ApplicationServices.Application app)
        {
            var r = new Regex(@"\b(Autodesk |Structure |MEP |Architecture )\b");
            return r.Replace(app.VersionName, "");
        }

        internal static Version PreloadAsmFromRevit()
        {
            var asmLocation = AppDomain.CurrentDomain.BaseDirectory;
            Version libGVersion = findRevitASMVersion(asmLocation);
            var dynCorePath = DBApp.DynamoCorePath;
            // Get the corresponding libG preloader location for the target ASM loading version.
            // If there is exact match preloader version to the target ASM version, use it, 
            // otherwise use the closest below.
            var preloaderLocation = DynamoShapeManager.Utilities.GetLibGPreloaderLocation(libGVersion, dynCorePath);

            // [Tech Debt] (Will refactor the code later)
            // The LibG version maybe different in Dynamo and Revit, using the one which is in Dynamo.
            Version preLoadLibGVersion = PreloadLibGVersion(preloaderLocation);
            DynamoShapeManager.Utilities.PreloadAsmFromPath(preloaderLocation, asmLocation);
            return preLoadLibGVersion;
        }

        // [Tech Debt] (Will refactor the code later)
        /// <summary>
        /// Return the preload version of LibG.
        /// </summary>
        /// <param name="preloaderLocation"></param>
        /// <returns></returns>
        internal static Version PreloadLibGVersion(string preloaderLocation)
        {
            preloaderLocation = new DirectoryInfo(preloaderLocation).Name;
            var regExp = new Regex(@"^libg_(\d\d\d)_(\d)_(\d)$", RegexOptions.IgnoreCase);

            var match = regExp.Match(preloaderLocation);
            if (match.Groups.Count == 4)
            {
                return new Version(
                    Convert.ToInt32(match.Groups[1].Value),
                    Convert.ToInt32(match.Groups[2].Value),
                    Convert.ToInt32(match.Groups[3].Value));
            }

            return new Version();
        }

        /// <summary>
        /// Returns the version of ASM which is installed with Revit at the requested path.
        /// This version number can be used to load the appropriate libG version.
        /// </summary>
        /// <param name="asmLocation">path where asm dlls are located, this is usually the product(Revit) install path</param>
        /// <returns></returns>
        internal static Version findRevitASMVersion(string asmLocation)
        {
            var lookup = new InstalledProductLookUp("Revit", "ASMAHL*.dll");
            var product = lookup.GetProductFromInstallPath(asmLocation);
            var libGversion = new Version(product.VersionInfo.Item1, product.VersionInfo.Item2, product.VersionInfo.Item3);
            return libGversion;
        }

        private static bool initializedCore;
        private static void InitializeCore()
        {
            if (initializedCore) return;

            // Change the locale that LibG depends on.
            StringBuilder sb = new StringBuilder("LANGUAGE=");
            var revitLocale = System.Globalization.CultureInfo.CurrentUICulture.ToString();
            sb.Append(revitLocale.Replace("-", "_"));
            _putenv(sb.ToString());

            initializedCore = true;
        }

        #endregion

        #region Helpers
        internal void ExecuteWorkspace(string wsPath,
            List<Dictionary<string, string>> allNodesInfo = null)
        {
            RevitDynamoModel.OpenFileFromPath(wsPath, true);

            //If we have information about the nodes and their values we want to push those values after the file is opened.
            if (allNodesInfo != null)
            {
                try
                {
                    if (allNodesInfo != null)
                    {
                        foreach (var nodeInfo in allNodesInfo)
                        {
                            if (nodeInfo.ContainsKey(JournalNodeKeys.Id) &&
                                nodeInfo.ContainsKey(JournalNodeKeys.Name) &&
                                nodeInfo.ContainsKey(JournalNodeKeys.Value))
                            {
                                var modelCommand = new DynamoModel.UpdateModelValueCommand(nodeInfo[JournalNodeKeys.Id],
                                                                                            nodeInfo[JournalNodeKeys.Name],
                                                                                            nodeInfo[JournalNodeKeys.Value]);
                                RevitDynamoModel.ExecuteCommand(modelCommand);
                            }
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Exception while trying to update nodes with new values");
                }
            }

            var modelToRun = RevitDynamoModel.CurrentWorkspace as HomeWorkspaceModel;
            if (modelToRun != null)
            {
                modelToRun.Run();
                return;
            }

        }

        [DllImport("msvcrt.dll")]
        public static extern int _putenv(string env);

        #endregion
    }
}