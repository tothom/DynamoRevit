using System;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Dynamo.Applications.Models;
using Dynamo.Applications.Properties;
using Dynamo.Graph.Workspaces;
using Dynamo.Interfaces;
using Dynamo.ViewModels;
using Dynamo.Visualization;
using Dynamo.Wpf.ViewModels.Core;
using Dynamo.Wpf.ViewModels.Watch3D;
using RevitServices.Persistence;
using RevitServicesUI.Persistence;

namespace Dynamo.Applications.ViewModel
{
    public class DynamoRevitViewModel : DynamoViewModel
    {
        private RevitDynamoModel RevitModel => Model as RevitDynamoModel;

        private DynamoRevitViewModel(StartConfiguration startConfiguration) :
            base(startConfiguration)
        {
            var model = (RevitDynamoModel)Model;

            model.RevitDocumentChanged += model_RevitDocumentChanged;
            model.RevitContextAvailable += model_RevitContextAvailable;
            model.RevitContextUnavailable += model_RevitContextUnavailable;
            model.RevitDocumentLost += model_RevitDocumentLost;
            model.RevitViewChanged += model_RevitViewChanged;
            model.InvalidRevitDocumentActivated += model_InvalidRevitDocumentActivated;

            SubscribeApplicationEvents();

            if (RevitWatch3DViewModel.GetTransientDisplayMethod() == null) return;

            var watch3DParams = new Watch3DViewModelStartupParams(model);
            var watch3DVm = new RevitWatch3DViewModel(watch3DParams);
            RegisterWatch3DViewModel(watch3DVm, new DefaultRenderPackageFactory());

            InitializeDocumentManager();
        }

        new public static DynamoRevitViewModel Start(StartConfiguration startConfiguration)
        {
            if (startConfiguration.DynamoModel == null)
            {
                startConfiguration.DynamoModel = RevitDynamoModel.Start();
            }
            else
            {
                if (startConfiguration.DynamoModel.GetType() != typeof(RevitDynamoModel))
                    throw new Exception("An instance of RevitDynamoModel is required to construct a DynamoRevitViewModel.");
            }

            if (startConfiguration.Watch3DViewModel == null)
            {
                startConfiguration.Watch3DViewModel =
                    HelixWatch3DViewModel.TryCreateHelixWatch3DViewModel(
                        null,
                        new Watch3DViewModelStartupParams(startConfiguration.DynamoModel),
                        startConfiguration.DynamoModel.Logger);
            }

            if (startConfiguration.WatchHandler == null)
                startConfiguration.WatchHandler = new DefaultWatchHandler(startConfiguration.DynamoModel.PreferenceSettings);

            return new DynamoRevitViewModel(startConfiguration);
        }

        private bool hasRegisteredApplicationEvents;
        private void SubscribeApplicationEvents()
        {
            if (hasRegisteredApplicationEvents)
            {
                return;
            }

            DynamoRevitApp.EventHandlerProxy.DocumentClosing += OnApplicationDocumentClosing;
            DynamoRevitApp.EventHandlerProxy.DocumentClosed += OnApplicationDocumentClosed;
            DynamoRevitApp.EventHandlerProxy.DocumentOpened += OnApplicationDocumentOpened;
            DynamoRevitApp.UIEventHandlerProxy.ViewActivating += OnApplicationViewActivating;
            DynamoRevitApp.UIEventHandlerProxy.ViewActivated += OnApplicationViewActivated;

            hasRegisteredApplicationEvents = true;
        }

        private void UnsubscribeApplicationEvents()
        {
            if (!hasRegisteredApplicationEvents)
            {
                return;
            }

            DynamoRevitApp.EventHandlerProxy.DocumentClosing -= OnApplicationDocumentClosing;
            DynamoRevitApp.EventHandlerProxy.DocumentClosed -= OnApplicationDocumentClosed;
            DynamoRevitApp.EventHandlerProxy.DocumentOpened -= OnApplicationDocumentOpened;
            DynamoRevitApp.UIEventHandlerProxy.ViewActivating -= OnApplicationViewActivating;
            DynamoRevitApp.UIEventHandlerProxy.ViewActivated -= OnApplicationViewActivated;

            hasRegisteredApplicationEvents = false;
        }

        private void InitializeDocumentManager()
        {
            // Set the intitial document.
            var activeUIDocument = UIDocumentManager.Instance.CurrentUIApplication?.ActiveUIDocument;
            if (activeUIDocument != null)
            {
                UIDocumentManager.Instance.CurrentUIDocument = activeUIDocument;
                DocumentManager.Instance.HandleDocumentActivation(activeUIDocument.ActiveView);

                RevitModel?.OnRevitDocumentChanged();
            }
        }

        private void model_InvalidRevitDocumentActivated()
        {
            var hsvm = (HomeWorkspaceViewModel)HomeSpaceViewModel;
            hsvm.CurrentNotificationLevel = NotificationLevel.Error;
            hsvm.CurrentNotificationMessage = Resources.DocumentPointingWarning;
        }

        private void model_RevitViewChanged(View view)
        {
            var hsvm = (HomeWorkspaceViewModel)HomeSpaceViewModel;
            hsvm.CurrentNotificationLevel = NotificationLevel.Moderate;
            hsvm.CurrentNotificationMessage =
                String.Format(Resources.ActiveViewWarning, view.Name);
        }

        private void model_RevitDocumentLost()
        {
            var hsvm = (HomeWorkspaceViewModel)HomeSpaceViewModel;
            hsvm.CurrentNotificationLevel = NotificationLevel.Error;
            hsvm.CurrentNotificationMessage = Resources.DocumentLostWarning;
            CloseHomeWorkspaceCommand.Execute(null);
            ExitCommand.Execute(null);
        }

        private void model_RevitContextUnavailable()
        {
            var hsvm = (HomeWorkspaceViewModel)HomeSpaceViewModel;
            hsvm.CurrentNotificationLevel = NotificationLevel.Error;
            hsvm.CurrentNotificationMessage = Resources.RevitInvalidContextWarning;
        }

        private void model_RevitContextAvailable()
        {
            var hsvm = (HomeWorkspaceViewModel)HomeSpaceViewModel;
            hsvm.CurrentNotificationLevel = NotificationLevel.Moderate;
            hsvm.CurrentNotificationMessage = Resources.RevitValidContextMessage;
        }

        private void model_RevitDocumentChanged(object sender, EventArgs e)
        {
            var hsvm = (HomeWorkspaceViewModel)HomeSpaceViewModel;
            hsvm.CurrentNotificationLevel = NotificationLevel.Moderate;
            hsvm.CurrentNotificationMessage =
                String.Format(GetDocumentPointerMessage());
        }

        private static string GetDocumentPointerMessage()
        {
            var docPath = DocumentManager.Instance.CurrentDBDocument.PathName;
            var message = String.IsNullOrEmpty(docPath)
                ? Resources.NewDocument
                : Resources.Document + ": " + docPath;
            return String.Format(Resources.DocumentPointerMessage, message);
        } 

        protected override void UnsubscribeAllEvents()
        {
            var model = (RevitDynamoModel)Model;
            model.RevitDocumentChanged -= model_RevitDocumentChanged;
            model.RevitContextAvailable -= model_RevitContextAvailable;
            model.RevitContextUnavailable -= model_RevitContextUnavailable;
            model.RevitDocumentLost -= model_RevitDocumentLost;
            model.RevitViewChanged -= model_RevitViewChanged;
            model.InvalidRevitDocumentActivated -= model_InvalidRevitDocumentActivated;

            UnsubscribeApplicationEvents();

            // Always reset current UI document on shutdown
            UIDocumentManager.Instance.CurrentUIDocument = null;

            base.UnsubscribeAllEvents();
        }

        /// <summary>
        /// Handler for Revit's ViewActivating event.
        /// Addins are not available in some views in Revit, notably perspective views.
        /// This will present a warning that Dynamo is not available to run and disable the run button.
        /// This handler is called before the ViewActivated event registered on the RevitDynamoModel.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void OnApplicationViewActivating(object sender, ViewActivatingEventArgs e)
        {
            RevitModel?.SetRunEnabledBasedOnContext(e.NewActiveView, true);
        }

        /// <summary>
        /// Handler for Revit's ViewActivated event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationViewActivated(object sender, ViewActivatedEventArgs e)
        {
            HandleRevitViewActivated();
        }

        /// <summary>
        /// Handler Revit's ViewActivated event.
        /// It is called when a view is activated. It is called after the 
        /// ViewActivating event.
        /// </summary>
        internal void HandleRevitViewActivated()
        {
            // If there is no active document, then set it to whatever
            // document has just been activated
            if (UIDocumentManager.Instance.CurrentUIDocument == null)
            {
                UIDocumentManager.Instance.CurrentUIDocument =
                    UIDocumentManager.Instance.CurrentUIApplication.ActiveUIDocument;

                RevitModel?.OnRevitDocumentChanged();

                RevitDynamoModel.InitializeMaterials();

                foreach (HomeWorkspaceModel ws in RevitModel?.Workspaces.OfType<HomeWorkspaceModel>())
                {
                    ws.RunSettings.RunEnabled = true;
                }
            }
        }


        /// <summary>
        /// Handler Revit's DocumentOpened event.
        /// It is called when a document is opened, but NOT when a document is 
        /// created from a template.
        /// </summary>
        private void HandleApplicationDocumentOpened()
        {
            // If the current document is null, for instance if there are
            // no documents open, then set the current document, and 
            // present a message telling us where Dynamo is pointing.
            if (UIDocumentManager.Instance.CurrentUIDocument == null)
            {
                var activeUIDocument = UIDocumentManager.Instance.CurrentUIApplication.ActiveUIDocument;

                UIDocumentManager.Instance.CurrentUIDocument = activeUIDocument;
                if (activeUIDocument != null)
                    DocumentManager.Instance.HandleDocumentActivation(activeUIDocument.ActiveView);

                RevitModel?.OnRevitDocumentChanged();

                foreach (HomeWorkspaceModel ws in Workspaces.OfType<HomeWorkspaceModel>())
                {
                    ws.RunSettings.RunEnabled = true;
                }

                RevitModel?.ResetForNewDocument();
            }
        }


        /// <summary>
        ///     Flag for syncing up document switches between Application.DocumentClosing and
        ///     Application.DocumentClosed events.
        /// </summary>
        private bool updateCurrentUIDoc;

        /// <summary>
        /// Handler Revit's DocumentClosing event.
        /// It is called when a document is closing.
        /// </summary>
        private void HandleApplicationDocumentClosing(Document doc)
        {
            // ReSharper disable once PossibleUnintendedReferenceComparison
            if (DocumentManager.Instance.CurrentDBDocument.Equals(doc))
            {
                updateCurrentUIDoc = true;
            }
        }

        /// <summary>
        /// Handle Revit's DocumentClosed event.
        /// It is called when a document is closed.
        /// </summary>
        private void HandleApplicationDocumentClosed()
        {
            // If the active UI document is null, it means that all views have been 
            // closed from all document. Clear our reference, present a warning,
            // and disable running.
            if (UIDocumentManager.Instance.CurrentUIApplication.ActiveUIDocument == null)
            {
                UIDocumentManager.Instance.CurrentUIDocument = null;
                foreach (HomeWorkspaceModel ws in Workspaces.OfType<HomeWorkspaceModel>())
                {
                    ws.RunSettings.RunEnabled = false;
                }

                RevitModel?.OnRevitDocumentLost();
            }
            else
            {
                // If Dynamo's active UI document's document is the one that was just closed
                // then set Dynamo's active UI document to whatever revit says is active.
                if (updateCurrentUIDoc)
                {
                    updateCurrentUIDoc = false;
                    UIDocumentManager.Instance.CurrentUIDocument =
                        UIDocumentManager.Instance.CurrentUIApplication.ActiveUIDocument;

                    RevitModel?.OnRevitDocumentChanged();
                }
            }

            var uiDoc = DocumentManager.Instance.CurrentDBDocument;
            if (uiDoc != null)
            {
                RevitModel?.SetRunEnabledBasedOnContext(uiDoc.ActiveView);
            }
        }

        internal static void ShutdownRevitHost()
        {
            // this method cannot be called without Revit 2014
            var exitCommand = RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit);
            var uiApplication = UIDocumentManager.Instance.CurrentUIApplication;

            if ((uiApplication != null) && uiApplication.CanPostCommand(exitCommand))
                uiApplication.PostCommand(exitCommand);
            else
            {
                MessageBox.Show(
                    "A command in progress prevented Dynamo from " +
                        "closing revit. Dynamo update will be cancelled.");
            }
        }

        #region Application event handler
        /// <summary>
        /// Handler for Revit's DocumentOpened event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            HandleApplicationDocumentOpened();
        }

        /// <summary>
        /// Handler for Revit's DocumentClosing event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            // Invalidate the cached active document value if it is the closing document.
            var activeDocumentHashCode = DocumentManager.Instance.ActiveDocumentHashCode;
            if (e.Document != null && (e.Document.GetHashCode() == activeDocumentHashCode))
                DocumentManager.Instance.HandleDocumentActivation(null);

            HandleApplicationDocumentClosing(e.Document);
        }

        /// <summary>
        /// Handler for Revit's DocumentClosed event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationDocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            HandleApplicationDocumentClosed();
        }

        #endregion
    }
}
