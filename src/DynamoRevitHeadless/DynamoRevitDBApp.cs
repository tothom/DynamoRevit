using System;
using System.IO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using RevitServices.Elements;
using DesignAutomationFramework;
using RDADynamoHelper;

namespace DynamoRevitHeadless
{
    [Transaction(TransactionMode.Manual),
     Regeneration(RegenerationOption.Manual)]
    public class DynamoRevitDBApp : IExternalDBApplication
    {
        private string _dynamoWorkDirectory;
        private RDADynamoHelper.RDADynamoHelper DynamoHelper { get; set; }
        public ExternalDBApplicationResult OnStartup(ControlledApplication application)
        {
            try
            {
                _dynamoWorkDirectory = Path.Combine(Directory.GetCurrentDirectory(), "dyn_work");
                DynamoHelper = new RDADynamoHelper.RDADynamoHelper(application, _dynamoWorkDirectory);

                DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
                
                return ExternalDBApplicationResult.Succeeded;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return ExternalDBApplicationResult.Failed;
            }
        }
        
        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            // UnsubscribeAssemblyEvents();
            RevitServicesUpdater.DisposeInstance();

            return ExternalDBApplicationResult.Succeeded;
        }


        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            // DynamoHelper.OnRunDynamoModelReady += OnRunDynamoModelReady;

            string graphPath = Path.Combine(Directory.GetCurrentDirectory(), "CountWalls.dyn");

            DynamoHelper.OnGraphResultReady += ProcessResult;
            DynamoHelper.RunDynamoGraph(new RunGraphArgs() { GraphPath = graphPath });
            
            //if (loaded)
            //{
            //    Console.WriteLine("<<!>> Starting graph execution.");
            //    rda.ExecuteWorkspace(Path.Combine(root, "graph.dyn"));
            //    Console.WriteLine("<<!>> Finished. Graph ran to completion.");
                
            //    //try to return the graph result
            //    var result = Path.Combine(root, "result.txt");
            //    var resultUpload = Path.Combine(Directory.GetCurrentDirectory(), "result.txt");
            //    if (File.Exists(result))
            //    {
            //        File.Copy(result, resultUpload, true);
            //    }
            //    else
            //    {
            //        File.WriteAllText("result.txt", "failed for unknown reasons");
            //    }
        //    }
        //    else
        //    {


        //Console.WriteLine("<<!>> Could not prepare Dynamo model.");
        //        File.WriteAllText("result.txt", "failed..." + Environment.NewLine + msg);
        //    }
        }

        private void OnRunDynamoModelReady(DynamoModelArgs obj)
        {
            
        }

        private void ProcessResult(GraphResultArgs result)
        {
            File.WriteAllText("result.txt", result.Result);
        }
    }
}
