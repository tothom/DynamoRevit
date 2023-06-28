using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using RevitServices.Elements;
using RevitServices.Persistence;
using RevitServices.Transactions;
using DesignAutomationFramework;

namespace DynamoRevitHeadless
{
    [Transaction(TransactionMode.Manual),
        Regeneration(RegenerationOption.Manual)]
    public class DynamoRevitDBApp : IExternalDBApplication
    {
        private static readonly string assemblyName = Assembly.GetExecutingAssembly().Location;
        public static ControlledApplication ControlledApplication;
        public static List<IUpdater> Updaters = new List<IUpdater>();
        public static string DynamoCorePath
        {
            get
            {
                if (string.IsNullOrEmpty(dynamopath))
                {
                    dynamopath = GetDynamoCorePath();
                }
                return dynamopath;
            }
        }

        /// <summary>
        /// Finds the Dynamo Core path by looking into registery or potentially a config file.
        /// </summary>
        /// <returns>The root folder path of Dynamo Core.</returns>
        internal static string GetDynamoCorePath()
        {
            var debug = @"E:\GitHub\Dynamo\bin\AnyCPU\Debug";
            if (Directory.Exists(debug))
                return debug;

            var dynamoRevitRootDirectory = Path.GetDirectoryName(Path.GetDirectoryName(assemblyName));
            return dynamoRevitRootDirectory;
            
            var dynamoRoot = GetDynamoRoot(dynamoRevitRootDirectory);

            var assembly = Assembly.LoadFrom(Path.Combine(dynamoRevitRootDirectory, "DynamoInstallDetective.dll"));
            var type = assembly.GetType("DynamoInstallDetective.DynamoProducts");

            var methodToInvoke = type.GetMethod("GetDynamoPath", BindingFlags.Public | BindingFlags.Static);
            if (methodToInvoke == null)
            {
                throw new MissingMethodException("Method 'DynamoInstallDetective.DynamoProducts.GetDynamoPath' not found");
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var methodParams = new object[] { version, dynamoRoot };
            return methodToInvoke.Invoke(null, methodParams) as string;
        }

        /// <summary>
        /// Gets Dynamo Root folder from the given DynamoRevit root.
        /// </summary>
        /// <param name="dynamoRevitRoot">The root folder of DynamoRevit binaries</param>
        /// <returns>The root folder path of Dynamo Core</returns>
        private static string GetDynamoRoot(string dynamoRevitRoot)
        {
            //TODO: use config file to setup Dynamo Path for debug builds.

            //When there is no config file, just replace DynamoRevit by Dynamo 
            //from the 'dynamoRevitRoot' folder.
            var parent = new DirectoryInfo(dynamoRevitRoot);
            var path = string.Empty;
            while (null != parent && parent.Name != @"DynamoRevit")
            {
                path = Path.Combine(parent.Name, path);
                parent = Directory.GetParent(parent.FullName);
            }

            return parent != null ? Path.Combine(Path.GetDirectoryName(parent.FullName), @"Dynamo", path) : dynamoRevitRoot;
        }

        private static string dynamopath;

        private ExternalDBApplicationResult loadDependentComponents()
        {
            //(Dimitar) I can't find this assembly anywhere. Is it still relevant?
            var dynamoRevitAditionsPath = Path.Combine(Path.GetDirectoryName(assemblyName), "DynamoRevitAdditions.dll");
            if (File.Exists(dynamoRevitAditionsPath))
            {
                try
                {
                    var dynamoRevitAditionsAss = Assembly.LoadFrom(dynamoRevitAditionsPath);
                    if (dynamoRevitAditionsAss != null)
                    {
                        var dynamoRevitAditionsLoader = dynamoRevitAditionsAss.CreateInstance("DynamoRevitAdditions.LoadManager");
                        if (dynamoRevitAditionsLoader != null)
                        {
                            dynamoRevitAditionsLoader.GetType().GetMethod("Initialize").Invoke(dynamoRevitAditionsLoader, null);
                            return ExternalDBApplicationResult.Succeeded;
                        }
                    }
                }
                catch (Exception ex)
                {
                    return ExternalDBApplicationResult.Failed;
                }

            }

            return ExternalDBApplicationResult.Failed;
        }

        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            Console.WriteLine("<<!>> Starting to load D4DA");
            try
            {
                if (!TryResolveDynamoCore(application))
                    return ExternalDBApplicationResult.Failed;

                ControlledApplication = application;

                SubscribeAssemblyEvents();

                TransactionManager.SetupManager(new AutomaticTransactionStrategy());
                ElementBinder.IsEnabled = true;

                RevitServicesUpdater.Initialize(Updaters);

                _ = loadDependentComponents(); //(Dimitar) should we fail to load here if the internal method returns Failed?

                DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;

                Console.WriteLine("<<!>> D4DA Loaded");

                return ExternalDBApplicationResult.Succeeded;
            }
            catch (Exception ex)
            {
                return ExternalDBApplicationResult.Failed;
            }
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            UnsubscribeAssemblyEvents();
            RevitServicesUpdater.DisposeInstance();

            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            Console.WriteLine("<<!>> DA event raised.");

            var root = Path.GetDirectoryName(e.DesignAutomationData.FilePath);
            var dynTempDir = Path.Combine(root, "dyn_tmp");
            Console.WriteLine($"<<!>> root folder is '{root}'");
            e.Succeeded = true;
            var app = e.DesignAutomationData.RevitApp;
            Console.WriteLine("<<!>> Preparing Dynamo model.");
            var rda = new Dynamo.Applications.RDADynamo();
            var loaded = rda.PrepareModel(app, dynTempDir, out var msg);
            Console.WriteLine(msg);
            if (loaded)
            {
                Console.WriteLine("<<!>> Starting graph execution.");
                rda.ExecuteWorkspace(Path.Combine(root, "graph.dyn"));
                Console.WriteLine("<<!>> Finished. Graph ran to completion.");
                
                //try to return the graph result
                var result = Path.Combine(root, "result.txt");
                var resultUpload = Path.Combine(Directory.GetCurrentDirectory(), "result.txt");
                if (File.Exists(result))
                {
                    File.Copy(result, resultUpload, true);
                }
                else
                {
                    File.WriteAllText("result.txt", "failed for unknown reasons");
                }
            }
            else
            {
                Console.WriteLine("<<!>> Could not prepare Dynamo model.");
                File.WriteAllText("result.txt", "failed..." + Environment.NewLine + msg);
            }
        }

        //no change
        private void SubscribeAssemblyEvents()
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        private void UnsubscribeAssemblyEvents()
        {
            AppDomain.CurrentDomain.AssemblyResolve -= ResolveAssembly;
        }

        //Note no change
        /// <summary>
        /// Handler to the ApplicationDomain's AssemblyResolve event.
        /// If an assembly's location cannot be resolved, an exception is
        /// thrown. Failure to resolve an assembly will leave Dynamo in 
        /// a bad state, so we should throw an exception here which gets caught 
        /// by our unhandled exception handler and presents the crash dialogue.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
        {
            var assemblyPath = string.Empty;
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            try
            {
                assemblyPath = Path.Combine(DynamoCorePath, assemblyName);
                if (File.Exists(assemblyPath))
                {
                    return Assembly.LoadFrom(assemblyPath);
                }

                var assemblyLocation = Assembly.GetExecutingAssembly().Location;
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

                // Try "Dynamo 0.x\Revit_20xx" folder first...
                assemblyPath = Path.Combine(assemblyDirectory, assemblyName);
                if (!File.Exists(assemblyPath))
                {
                    // If assembly cannot be found, try in "Dynamo 0.x" folder.
                    var parentDirectory = Directory.GetParent(assemblyDirectory);
                    assemblyPath = Path.Combine(parentDirectory.FullName, assemblyName);
                }

                return (File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("The location of the assembly, {0} could not be resolved for loading.", assemblyPath), ex);
            }
        }

        //Note shared 95%
        /// <summary>
        /// Whether the DynamoCore and DynamoRevit are in Revit's internal Addin folder
        /// </summary>
        /// <param name="application"></param>
        /// <returns>True is that Dynamo and DynamoRevit are in Revit internal Addins folder</returns>
        private static bool IsRevitInternalAddin(ControlledApplication application)
        {
            if (application == null)
                return false;
            var revitVersion = application.VersionNumber;
            var dynamoRevitRoot = Path.GetDirectoryName(Path.GetDirectoryName(assemblyName));
            var RevitRoot = Path.GetDirectoryName(application.GetType().Assembly.Location);
            if (dynamoRevitRoot.StartsWith(RevitRoot))
            {
                if (File.Exists(Path.Combine(dynamoRevitRoot, "DynamoInstallDetective.dll")) && File.Exists(Path.Combine(dynamoRevitRoot, "DynamoCore.dll")))
                {
                    var version_DynamoInstallDetective = FileVersionInfo.GetVersionInfo(Path.Combine(dynamoRevitRoot, "DynamoInstallDetective.dll"));
                    var version_DynamoCore = FileVersionInfo.GetVersionInfo(Path.Combine(dynamoRevitRoot, "DynamoCore.dll"));
                    if (version_DynamoCore.FileMajorPart == version_DynamoInstallDetective.FileMajorPart &&
                        version_DynamoCore.FileMinorPart == version_DynamoInstallDetective.FileMinorPart &&
                        version_DynamoCore.FileBuildPart == version_DynamoInstallDetective.FileBuildPart
                        )
                        return true;
                }
            }
            return false;
        }

        private bool TryResolveDynamoCore(ControlledApplication application)
        {
            if (IsRevitInternalAddin(application))
            {
                dynamopath = Path.GetDirectoryName(Path.GetDirectoryName(assemblyName));
            }
            if (string.IsNullOrEmpty(DynamoCorePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(assemblyName);
                var shortversion = fvi.FileMajorPart + "." + fvi.FileMinorPart;

                return false;
            }
            return true;
        }
    }
}
